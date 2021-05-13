using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public class TrainAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public int trackIndex = 1;
    public float totalDistance;
    public float maxSpeed;
    public float targetDistance = 0;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new TrackIndex() {value = trackIndex });
        dstManager.AddComponentData(entity, new PlatformIndex());
        dstManager.AddComponentData(entity, new TrainWaitTimer());
        dstManager.AddComponentData(entity, new TrainCurrDistance());
        dstManager.AddComponentData(entity, new TrainTargetDistance());
        dstManager.AddComponentData(entity, new TrainCurrSpeed());
        dstManager.AddComponentData(entity, new TrainMaxSpeed() { value = maxSpeed });
        dstManager.AddComponentData(entity, new TrainState() { value = CurrTrainState.Waiting });
        dstManager.AddBuffer<DoorEntities>(entity);

        dstManager.SetComponentData(entity, new TrainTargetDistance(){value = targetDistance});
        
    }
}