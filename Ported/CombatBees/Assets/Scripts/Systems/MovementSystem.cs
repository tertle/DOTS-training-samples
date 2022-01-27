using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial class MovementSystem : SystemBase
{
    private EntityCommandBufferSystem sys;
    private EntityQuery foodQuery;

    protected override void OnCreate()
    {
        sys = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<Spawner>();
    }

    protected override void OnUpdate()
    {
        var deltaTime = UnityEngine.Time.deltaTime;
        var tNow = UnityEngine.Time.timeSinceLevelLoad;
        var spawner = GetSingleton<Spawner>();

        var randomSeed = (uint) math.max(1,
            DateTime.Now.Millisecond +
            DateTime.Now.Second +
            DateTime.Now.Minute +
            DateTime.Now.Day +
            DateTime.Now.Month +
            DateTime.Now.Year);

        var random = new Random(randomSeed);

        // Move: Food (move food before bees since bees need to follow food)
        //       - also store all the moved food positions for cross-reference further down 
        // TODO: Don't need this?
        // foodQuery = GetEntityQuery(typeof(FoodTag));
        var foodCount = foodQuery.CalculateEntityCount();
        var foodPositions = new NativeArray<float3>(foodCount, Allocator.TempJob);
        var foodEntities = new NativeArray<Entity>(foodCount, Allocator.TempJob);
        Entities
            .WithStoreEntityQueryInField(ref foodQuery)
            .WithAll<Food>()
            .ForEach((Entity e, int entityInQueryIndex, ref Translation translation, ref PP_Movement ppMovement) =>
            {
                translation.Value = ppMovement.Progress(deltaTime);
                foodPositions[entityInQueryIndex] = translation.Value;
                foodEntities[entityInQueryIndex] = e;
            }).WithName("GetFoodPositions")
            .ScheduleParallel();

        // TODO: Can we parallelize movement by scheduling the translation updates
        //       and distance checks separately?  Should let us ues ScheduleParallel()
        //       and WithNativeDisableParallelForRestriction(), yeah?

        //bits
        Entities
            .WithAll<BeeBitsTag>()
            .ForEach((Entity e, ref Translation translation) =>
            {
                // calculate bits falling movement - straight down for now
                if (translation.Value.y > 0)
                {
                    translation.Value = translation.Value * -9.8f * deltaTime;
                }
                else
                {
                    //destroy this entity and create/init a blood splat
                    /*
                    ecb.DestroyEntity(e);
                    var instance = ecb.Instantiate(spawner.BloodPrefab);

                    var trans = new Translation
                    {
                        Value = translation.Value
                    };

                    ecb.SetComponent(instance, translation);
                    */

                }
            }).Schedule();
        //sys.AddJobHandleForProducer(Dependency);

        // Scale: Blood
        // TODO: that^
        // Update: Bees
        // TODO: update bee "end position" to match its target food in "foodPositionData"

        // Move: anything that hasn't already moved
        Entities
            .WithNone<BeeBitsTag, BloodTag, Food>()
            .ForEach((Entity e, ref Translation translation, ref Rotation rotation, ref PP_Movement ppMovement) =>
            {
                // do bee movement
                translation.Value = ppMovement.Progress(deltaTime, MotionType.BeeBumble);

                float futureT = Mathf.Clamp(ppMovement.t + 0.01f, 0, 1f);

                float3 fwd = ppMovement.GetTransAtProgress(futureT, MotionType.BeeBumble) - translation.Value;

                var newRot = quaternion.identity;

                newRot = math.mul(newRot, quaternion.RotateZ(math.radians(90)));
                newRot = math.mul(newRot, quaternion.LookRotation(fwd, new float3(0, 1, 0)));


                rotation.Value = newRot;


            }).Schedule();

        var ecb = sys.CreateCommandBuffer();
        // Collision: Food
        Entities
            .ForEach((Entity e, int entityInQueryIndex, ref Translation translation, ref PP_Movement ppMovement, in Food food) =>
            {
                if (food.isBeeingCarried)
                {
                    translation.Value = ppMovement.Progress(deltaTime, MotionType.BeeBumble);
                }
                else
                {
                    translation.Value = ppMovement.Progress(deltaTime, MotionType.Linear);
                }

                if (math.abs(translation.Value.x) >= spawner.ArenaExtents.x)
                {
                    if (translation.Value.y <= 0.5f)
                    {
                        //destroy this entity and create/init a blood splat
                        ecb.DestroyEntity(e);

                        for (var i = 0; i < 3; i++)
                        {
                            var minBeeBounds = SpawnerSystem.GetBeeMinBounds(spawner);
                            var maxBeeBounds = SpawnerSystem.GetBeeMaxBounds(spawner, minBeeBounds);

                            var beeRandomY = SpawnerSystem.GetRandomBeeY(ref random, minBeeBounds, maxBeeBounds);
                            var beeRandomZ = SpawnerSystem.GetRandomBeeZ(ref random, minBeeBounds, maxBeeBounds);

                            if (translation.Value.x > 0)
                            {
                                // Yellow Bees
                                var beeRandomX = SpawnerSystem.GetRandomYellowBeeX(ref random, minBeeBounds, maxBeeBounds);

                                SpawnerSystem.BufferEntityInstantiation(spawner.YellowBeePrefab,
                                    new float3(beeRandomX, beeRandomY, beeRandomZ),
                                    ref ecb);
                            }
                            else
                            {
                                // Blue Bees
                                var beeRandomX = SpawnerSystem.GetRandomBlueBeeX(ref random, minBeeBounds, maxBeeBounds);

                                SpawnerSystem.BufferEntityInstantiation(spawner.BlueBeePrefab,
                                    new float3(beeRandomX, beeRandomY, beeRandomZ),
                                    ref ecb);
                            }
                        }
                    }
                }
                // Not in a goal: check for inter-food collisions for stacking
                // Note: this relies on food-count not having been changed during any code above this
                else
                {
                    var hasMoved = false;
                    for (var i = 0; i < foodCount; i++)
                    {
                        // Check if this food is being checked against itself
                        if (e.Index == foodEntities[i].Index &&
                            e.Version == foodEntities[i].Version)
                        {
                            if (hasMoved)
                            {
                                // If this object has already moved, then update its recorded position
                                // as well, for other objects to test against.
                                foodPositions[i] = translation.Value;
                            }
                            else
                            {
                                // If this object hasn't moved before having been determined to collide
                                // with anything else, then it either doesn't collide or is the first of
                                // any potential colliding objects, so it gets priority over this position.
                                break;
                            }
                        }
                        
                        // Check if radii overlap first
                        var planarDiffVector = new float2(
                            translation.Value.x - foodPositions[i].x,
                            translation.Value.z - foodPositions[i].z);
                        if (planarDiffVector.x * planarDiffVector.x + planarDiffVector.y * planarDiffVector.y < 1f)
                        {
                            var collisionHeightOverlap = 1f - (translation.Value.y - foodPositions[i].y);
                            
                            // If the height overlaps above or below, move it up enough to sit on top
                            if (collisionHeightOverlap > 0f &&
                                collisionHeightOverlap < 1f)
                            {
                                translation.Value.y += collisionHeightOverlap;
                                
                                // TODO: set the endposition on PP_Movement to the updated, un-clipped position
                                //          - or some other method of making it not try to move downwards anymore

                                hasMoved = true;
                            }
                        }
                        
                    }
                }
            }).WithDisposeOnCompletion(foodPositions)
            .WithDisposeOnCompletion(foodEntities)
            .Schedule();

        // Collision: Bee Bits
        Entities
            .WithAll<BeeBitsTag>()
            .ForEach((Entity e, ref Translation translation, ref Velocity velocity) =>
            {
                // Ground Collision
                if (translation.Value.y < 0)
                {
                    // TODO: Spawn blood
                }
            }).Schedule();

        sys.AddJobHandleForProducer(Dependency);
    }
}