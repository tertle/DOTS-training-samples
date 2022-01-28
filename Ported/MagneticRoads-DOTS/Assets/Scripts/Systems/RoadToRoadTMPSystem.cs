using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial class RoadToRoadTMPSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Persistent);
        var random = new Random(1234);

        var singleton = GetSingletonEntity<SplineDefArrayElement>();
        var splinesArray = GetBuffer<SplineDefArrayElement>(singleton);
        var linkArray = GetBuffer<SplineLink>(singleton);
        var splineIdToRoadArray = GetBuffer<SplineIdToRoad>(singleton);

        Entities
            .WithAll<SplinePosition, SplineDef, RoadCompleted>()
            .ForEach((Entity entity, ref SplineDef SplineDef, ref SplinePosition splinePosition) =>
            {
                var roadQueue = GetBuffer<CarQueue>(splineIdToRoadArray[SplineDef.splineId].Value);
                roadQueue.RemoveAt(0);
                
                var neighbors = linkArray[SplineDef.splineId];
                var rand = random.NextInt(0, 1);
                var nextSplineId = rand == 0 || neighbors.Value.y < 0 ? neighbors.Value.x : neighbors.Value.y;
                SplineDef = splinesArray[nextSplineId].Value;
                
                roadQueue = GetBuffer<CarQueue>(splineIdToRoadArray[SplineDef.splineId].Value);
                roadQueue.Add(entity);
                
                splinePosition.position = 0f;
                ecb.RemoveComponent<RoadCompleted>(entity);
            }).Run();
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}