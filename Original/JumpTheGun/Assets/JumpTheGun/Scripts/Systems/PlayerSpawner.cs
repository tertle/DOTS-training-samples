using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

[BurstCompile]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct PlayerSpawner : ISystem
{
    // Queries should not be created on the spot in OnUpdate, so they are cached in fields.
    EntityQuery playerQuery;
    EntityQuery boxQuery;
    ComponentDataFromEntity<Boxes> boxesFromEntity;
    ComponentDataFromEntity<PlayerComponent> pcFromEntity;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Config>();
        boxesFromEntity = state.GetComponentDataFromEntity<Boxes>(true);
        pcFromEntity= state.GetComponentDataFromEntity<PlayerComponent>(true);
        boxQuery = state.GetEntityQuery(typeof(Boxes));
        playerQuery = state.GetEntityQuery(typeof(PlayerComponent));
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<Config>();
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        var boxEntities = boxQuery.ToEntityArray(Unity.Collections.Allocator.TempJob);
        var players = CollectionHelper.CreateNativeArray<Entity>(1, Allocator.Temp);
        var queryMask = playerQuery.GetEntityQueryMask();

        ecb.Instantiate(config.playerPrefab, players);
        boxesFromEntity.Update(ref state);
        
        pcFromEntity.Update(ref state);

        foreach (var player in players)
        {
            UnityEngine.Debug.Log("This happens");
            //PlayerComponent pc = pcFromEntity[player];
            PlayerComponent pc = new PlayerComponent(); 
            UnityEngine.Debug.Log("This does not happen");
            ecb.SetComponentForLinkedEntityGroup(player, queryMask, PlayerProperties(boxesFromEntity, config, boxEntities, pc));
        }
        state.Enabled = false;
    }

    public static PlayerComponent PlayerProperties(ComponentDataFromEntity<Boxes> boxesFromEntity, Config config, NativeArray<Entity> boxes, PlayerComponent prefabData)
    {
        var pc = prefabData;
        foreach (Entity box in boxes){
            UnityEngine.Debug.Log("we have a box");
            pc.startBox = box;
            Boxes boxRef = boxesFromEntity[box]; 
            float3 pcPos = Spawn(boxRef.row,boxRef.column, pc , config, boxRef, boxesFromEntity, boxes);
            return pc;
        }
        return pc;
    }

    public static float3 Spawn(int col, int row, PlayerComponent playerComponent, 
    Config config, Boxes startBox, ComponentDataFromEntity<Boxes> boxesFromEntity, NativeArray<Entity> boxes){
        Boxes newStartBox; 
        Entity newStartBoxEntity = new Entity(); 
        
        foreach (var box in boxes) { 
            newStartBox = boxesFromEntity[box];
            if (newStartBox.row == row && newStartBox.column == col){
                newStartBoxEntity = box;
                break; 
            }
        }
        playerComponent.startBox = newStartBoxEntity;
		playerComponent.endBox = playerComponent.startBox;

        float top = startBox.top; 
        return TerrainAreaClusters.LocalPositionFromBox(col, row, config, top + playerComponent.yOffset);
    }
    
}