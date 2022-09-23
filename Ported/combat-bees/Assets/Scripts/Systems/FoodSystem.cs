﻿using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;


[BurstCompile]
public struct DetectFoodDeliveryJob : IJobChunk
{
    public EntityCommandBuffer.ParallelWriter ECB;

    [ReadOnly] public ComponentTypeHandle<LocalToWorldTransform> TransformHandle;
    [ReadOnly] public EntityTypeHandle EntityHandle;
    [ReadOnly] public AABB FieldArea;

    public Entity Prefab; 
    public int Faction; 
    public int Count; 

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        //var config = SystemAPI.GetSingleton<BeeConfig>();
        var chunkEntityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.ChunkEntityCount);
        var transforms = chunk.GetNativeArray(TransformHandle);
        var entities = chunk.GetNativeArray(EntityHandle);

        float k_eps = 1e-2f;
        while (chunkEntityEnumerator.NextEntityIndex(out var i))
        {
            var tmComp = transforms[i];
            var faction = Faction;
            // check if food has reached the ground.
            // if yes, remove it and issue a bee spawn request

            bool onGround = tmComp.Value.Position.y < FieldArea.Min.y + k_eps;

            if (onGround)
            {
                SpawningRequestSystem.CreateSpawningRequest(ECB, unfilteredChunkIndex, new SpawningRequestSystem.SpawningRequestInfo
                {
                    Prefab = Prefab,
                    // TOdo : This might not work, need to review to see 
                    Faction = faction,
                    Aabb = new AABB{ Center = tmComp.Value.Position, Extents = new float3(0.1f, 0.1f, 0.1f)},
                    Color = new float4(1.0f, 0.0f, 0.0f, 1.0f),
                    Count = Count
                });

                ECB.DestroyEntity(i, entities[i]);
            }
        }
    }
}
    
[BurstCompile]
partial struct FoodSystem : ISystem
{
    private EntityQuery FoodQuery;
    public ComponentTypeHandle<LocalToWorldTransform> TransformHandle;
    public SharedComponentTypeHandle<Faction> FactionHandle;
    public EntityTypeHandle EntityHandle;

    public void OnCreate(ref SystemState state)
    {
        FoodQuery = SystemAPI.QueryBuilder().WithAll<Food, Faction, LocalToWorldTransform>().Build();
        TransformHandle = state.GetComponentTypeHandle<LocalToWorldTransform>();
        FactionHandle = state.GetSharedComponentTypeHandle<Faction>();

        state.RequireForUpdate<BeeConfig>();
    }

    public void OnDestroy(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BeeConfig>();
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();

        TransformHandle.Update(ref state);
        EntityHandle.Update(ref state);

        var combinedHandle = new JobHandle(); 
        for (int i = (int) Factions.Team1; i < (int) Factions.NumFactions; i++)
        {
            var faction = new Faction { Value = i };
            FoodQuery.SetSharedComponentFilter( faction );
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged); 

            var job = new DetectFoodDeliveryJob()
            {
                Faction = faction.Value,
                ECB = ecb.AsParallelWriter(),
                TransformHandle = TransformHandle,
                EntityHandle = EntityHandle,
                FieldArea = config.fieldArea,
                Prefab = config.bee,
                Count = config.FoodBeeSpawnCount
            };

            var jobHandle = job.ScheduleParallel(FoodQuery, state.Dependency);
            combinedHandle = JobHandle.CombineDependencies(jobHandle, combinedHandle);
        }

        state.Dependency = combinedHandle;
    }
}