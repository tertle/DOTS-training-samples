using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
partial struct SpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Config>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // The default world should not be used because the targeted EntityManager
        // may not be part of it.
        // var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var config = SystemAPI.GetSingleton<Config>();

        //Instantiate our two bee teams...
        state.EntityManager.Instantiate(config.BlueBeePrefab, config.TeamBlueBeeCount, Allocator.Temp);
        foreach (var transform in SystemAPI.Query<TransformAspect>().WithAll<BlueBee>())
        {
            transform.Position = new float3(45, 10, 0);
        }

        state.EntityManager.Instantiate(config.YellowBeePrefab, config.TeamYellowBeeCount, Allocator.Temp);
        foreach (var transform in SystemAPI.Query<TransformAspect>().WithAll<YellowBee>())
        {
            transform.Position = new float3(-45, 10, 0);
        }

        state.EntityManager.Instantiate(config.FoodResourcePrefab, config.FoodResourceCount, Allocator.Temp);
        // food resource field is 20 by 20, 10 in each direction
        // random number generator for both dimension from -10 to 10
        Random rand = new Random(123);
        foreach (var transform in SystemAPI.Query<TransformAspect>().WithAll<FoodResource>())
        {
            var position = new float3(rand.NextInt(-10, 11), 0, rand.NextInt(-10, 11));
            transform.Position = position;
        }

        // This system should only run once at startup. So it disables itself after one update.
        state.Enabled = false;
    }
}