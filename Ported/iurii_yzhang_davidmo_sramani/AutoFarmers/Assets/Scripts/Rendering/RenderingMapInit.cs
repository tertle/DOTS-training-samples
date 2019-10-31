﻿using System;
using System.Collections;
using System.Collections.Generic;
using GameAI;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

public struct TilePositionRequest : IComponentData
{
    public int2 position;
}

public struct StonePositionRequest : IComponentData
{
    public int2 position;
    public int2 size;
}

public struct PlantPositionRequest : IComponentData
{
    public int2 position;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderingMapInit : JobComponentSystem
{
    struct ExecuteOnceTag : IComponentData {}
    private EntityQuery m_executeOnce;

    public Entity groundEntityPrefab;
    public Entity stoneEntityPrefab;
    public Entity plantEntityPrefab;
    public Entity farmerEntityPrefab;
    public Entity droneEntityPrefab;

    public void CreatePrefabEntity()
    {
        var ru = RenderingUnity.instance;
        
        Assert.IsNotNull(ru);
        
        farmerEntityPrefab = ru.CreateFarmer(EntityManager);
        Assert.IsTrue(farmerEntityPrefab != Entity.Null);

        droneEntityPrefab = ru.CreateDrone(EntityManager);
        Assert.IsTrue(droneEntityPrefab != Entity.Null);

        groundEntityPrefab = ru.CreateGround(EntityManager);
        stoneEntityPrefab = ru.CreateStone(EntityManager);
        plantEntityPrefab = ru.CreatePlant(EntityManager);
            
        Assert.IsTrue(groundEntityPrefab != Entity.Null);
        Assert.IsTrue(stoneEntityPrefab != Entity.Null);
        Assert.IsTrue(plantEntityPrefab != Entity.Null);
    }

    protected override void OnCreate()
    {
        var ru = RenderingUnity.instance;

        Enabled = ru != null;
        
        if (Enabled)
        {
            ResetExecuteOnceTag(EntityManager);
            CreatePrefabEntity();
        }

        var initialFarmerSpawnerEntity = EntityManager.CreateEntity(typeof(SpawnPointComponent), typeof(SpawnFarmerTagComponent), typeof(InitialSpawnerTagComponent));
        // TODO decide start position.
        EntityManager.SetComponentData<SpawnPointComponent>(initialFarmerSpawnerEntity, new SpawnPointComponent { MapSpawnPosition = new int2 (80, 80) });
        
        m_executeOnce = GetEntityQuery(ComponentType.ReadOnly<ExecuteOnceTag>());
        RequireForUpdate(m_executeOnce);
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var once = m_executeOnce.GetSingletonEntity();
        Assert.IsTrue(once != Entity.Null);
        EntityManager.DestroyEntity(once);

        Assert.IsTrue(groundEntityPrefab != Entity.Null);
        Assert.IsTrue(stoneEntityPrefab != Entity.Null);
        Assert.IsTrue(plantEntityPrefab != Entity.Null);

        var ecbSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        
        var ground = groundEntityPrefab;
        var stone = stoneEntityPrefab;
        var plant = plantEntityPrefab;

        var worldSizeHalf = World.GetOrCreateSystem<WorldCreatorSystem>().WorldSizeHalf;
        
        var ecb1 = ecbSystem.CreateCommandBuffer().ToConcurrent();
        var handle1 = Entities.WithoutBurst().ForEach((int nativeThreadIndex, ref TilePositionRequest req) =>
        {
            var wpos = RenderingUnity.Tile2WorldPosition(req.position, worldSizeHalf) - new float3(0, 0.5f, 0);
            var e = ecb1.Instantiate(nativeThreadIndex, ground);
            ecb1.SetComponent(nativeThreadIndex, e,
                new LocalToWorld {Value = float4x4.TRS(wpos, quaternion.identity, new float3(0.2f,1,0.2f))});

        }).Schedule(inputDeps);

        var halfSizeOffset = new float3(RenderingUnity.scale * 0.5f, 0, RenderingUnity.scale * 0.5f);
        
        var ecb2 = ecbSystem.CreateCommandBuffer().ToConcurrent();
        var handle2 = Entities.WithoutBurst()
            .ForEach((int nativeThreadIndex, ref StonePositionRequest req) =>
        {
            var wpos = RenderingUnity.Tile2WorldPosition(req.position, worldSizeHalf); // min pos
            var wsize = RenderingUnity.Tile2WorldSize(req.size) - 0.1f;
            var wposCenter = wpos + wsize * 0.5f - halfSizeOffset;
            
            var e = ecb2.Instantiate(nativeThreadIndex, stone);
            ecb2.SetComponent(nativeThreadIndex, e, new Translation {Value = wposCenter});
            ecb2.SetComponent(nativeThreadIndex, e, new NonUniformScale {Value = wsize});
            
        }).Schedule(inputDeps);

        var ecb3 = ecbSystem.CreateCommandBuffer().ToConcurrent();
        var handle3 = Entities.WithoutBurst().ForEach((int nativeThreadIndex, ref PlantPositionRequest req) =>
        {
            var wpos = RenderingUnity.Tile2WorldPosition(req.position, worldSizeHalf);
            var e = ecb3.Instantiate(nativeThreadIndex, plant);
            ecb3.SetComponent(nativeThreadIndex, e, new Translation {Value = wpos});
            
        }).Schedule(inputDeps);

        var handle = JobHandle.CombineDependencies(handle1, handle2, handle3);
        
        ecbSystem.AddJobHandleForProducer(handle1);
        ecbSystem.AddJobHandleForProducer(handle2);
        ecbSystem.AddJobHandleForProducer(handle3);

        return handle;
    }
    
    public static void ResetExecuteOnceTag(EntityManager mManager)
    {
        var q = mManager.CreateEntityQuery(ComponentType.ReadOnly<ExecuteOnceTag>());
        if (q.CalculateEntityCount() == 0)
        {
            var e = mManager.CreateEntity();
            mManager.AddComponent<ExecuteOnceTag>(e);
        }
    }
}
