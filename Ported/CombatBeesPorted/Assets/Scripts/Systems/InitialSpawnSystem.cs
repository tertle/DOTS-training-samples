
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class InitialSpawnSystem : SystemBase
{
    private static readonly int HivePosition = Shader.PropertyToID("_HivePosition");
    private bool firstInitCreated = false;

    EndSimulationEntityCommandBufferSystem endSim;    

    protected override void OnCreate()
    {
        endSim = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var gameConfig = GetSingleton<GameConfiguration>();
        var commandBuffer = endSim.CreateCommandBuffer();
        var random = new Random((uint)(Time.ElapsedTime * 10000)+1);

        var distance = 10f;
        
        Shader.SetGlobalFloat(HivePosition,gameConfig.HivePosition);

        if (!firstInitCreated)
        {
            var initConfig = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(initConfig, new InitialSpawnConfiguration() { BeeCount = gameConfig.BeeCount, FoodCount = gameConfig.FoodCount });
            firstInitCreated = true;
        } else
        {
            Entities
          .ForEach((in Entity entity, in InitialSpawnConfiguration config) =>
          {
              for (int i = 0; i < gameConfig.FoodCount; i++)
              {
                  var foodEntity = commandBuffer.Instantiate(gameConfig.FoodPrefab);
                  commandBuffer.SetComponent(foodEntity, new Translation() { Value = (random.NextFloat3Direction() * distance * new float3(1, 0, 1)) + new float3(0, .25f, 0) });
                  commandBuffer.AddComponent(foodEntity, new Force() { });
                  commandBuffer.AddComponent(foodEntity, new Velocity() { });
                  commandBuffer.AddComponent(foodEntity, new Bounciness() { Value = 0.3f});
              }

              var TeamABeeSpawner = commandBuffer.CreateEntity();
              commandBuffer.AddComponent(TeamABeeSpawner, new BeeSpawnConfiguration() { Count = gameConfig.BeeCount });
              commandBuffer.AddComponent(TeamABeeSpawner, new TeamA());
              commandBuffer.AddComponent(TeamABeeSpawner, new Translation() { Value = new float3(-gameConfig.HivePosition, 0, 0) });
              var TeamBBeeSpawner = commandBuffer.CreateEntity();
              commandBuffer.AddComponent(TeamBBeeSpawner, new BeeSpawnConfiguration() { Count = gameConfig.BeeCount });
              commandBuffer.AddComponent(TeamBBeeSpawner, new TeamB());
              commandBuffer.AddComponent(TeamBBeeSpawner, new Translation() { Value = new float3(gameConfig.HivePosition, 0, 0) });

              commandBuffer.DestroyEntity(entity);
          }).Schedule();
        }
          
        endSim.AddJobHandleForProducer(Dependency);
    }
}