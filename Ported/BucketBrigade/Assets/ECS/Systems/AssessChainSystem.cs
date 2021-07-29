using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class AssessChainSystem : SystemBase
{
    private int chainIndex;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GameConfigComponent>();
        RequireSingletonForUpdate<HeatMapElement>();
        chainIndex = 0;
    }

    protected override void OnUpdate()
    {
        var config = GetSingleton<GameConfigComponent>();
        var grid = config.SimulationSize;
        var flashPoint = config.FlashPoint;

        var query = GetEntityQuery(typeof(WaterTagComponent), typeof(Translation), typeof(WaterVolumeComponent));
        var waterTranslations = query.ToComponentDataArray<Translation>(Allocator.TempJob);
        var waterVolumes = query.ToComponentDataArray<WaterVolumeComponent>(Allocator.TempJob);
        var waterEntities = query.ToEntityArray(Allocator.TempJob);

        var heatMapEntity = GetSingletonEntity<HeatMapElement>();
        var heatMap = GetBuffer<HeatMapElement>(heatMapEntity);
        var currentChainIndex = chainIndex++;
        chainIndex %= config.ChainSize;
            
        Entities
            .WithDisposeOnCompletion(waterTranslations)
            .WithDisposeOnCompletion(waterVolumes)
            .WithDisposeOnCompletion(waterEntities)
            .ForEach((int entityInQueryIndex, in BotsChainComponent chain, in DynamicBuffer<BotChainElementData> chainBuffer) =>
            {
                if (entityInQueryIndex != currentChainIndex)
                    return;
                    
                var scooper = chain.scooper;
                var scooperPos = GetComponent<Translation>(scooper).Value;
                var thrower = chain.thrower;
                
                Entity water = GetComponent<TargetWater>(scooper).water;
                
                // Find closest water if current water source is not valid
                if (water == Entity.Null || GetComponent<WaterVolumeComponent>(water).Volume <= 0.0f)
                {
                    var minDistance = float.MaxValue;
                    for (int i = 0; i < waterTranslations.Length; ++i)
                    {
                        var waterVolume = waterVolumes[i];
                        if (waterVolume.Volume <= 0.0f)
                            continue;

                        var waterTrans = waterTranslations[i];
                        var distance = math.lengthsq(scooperPos - waterTrans.Value);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            water = waterEntities[i];
                        }
                    }
                }

                if (water == Entity.Null)
                    return;

                var waterPos = GetComponent<Translation>(water).Value;

                // Find closest fire cell
                var bestFirePos = float3.zero;
                var minFireDistance = float.MaxValue;
                var bestFireIndex = -1;
                for (int i = 0; i < heatMap.Length; ++i)
                {
                    if (heatMap[i].temperature < flashPoint)
                        continue;

                    int col = i % grid;
                    int row = i / grid;
                    var firePos = new float3(col, 0.0f, row);
                    var distance = math.lengthsq(waterPos - firePos);
                    if (distance < minFireDistance)
                    {
                        minFireDistance = distance;
                        bestFirePos = firePos;
                        bestFireIndex = i;
                    }
                }
                
                SetComponent(scooper, new TargetWater() {water = water});
                SetComponent(scooper, new BotDropOffLocation() {Value = waterPos.xz});

                SetComponent(thrower, new TargetLocationComponent() {location = bestFirePos.xz});
                SetComponent(thrower, new HeatMapIndex() { index = bestFireIndex });

                for (int i = 0; i < chainBuffer.Length; ++i)
                {
                    var passerFull = chainBuffer[i].passerFull;
                    var pickUpPosFull = GetChainPosition(i, chainBuffer.Length, waterPos.xz, bestFirePos.xz);
                    var dropOffPosFull = GetChainPosition(i + 1, chainBuffer.Length, waterPos.xz, bestFirePos.xz);
                    SetComponent(passerFull, new BotPickUpLocation() {Value = pickUpPosFull});
                    SetComponent(passerFull, new BotDropOffLocation() {Value = dropOffPosFull});

                    if (i == chainBuffer.Length - 1)
                    {
                        SetComponent(thrower, new BotPickUpLocation() {Value = dropOffPosFull});
                        SetComponent(thrower, new BotDropOffLocation() {Value = dropOffPosFull});
                    }

                    var passerEmpty = chainBuffer[i].passerEmpty;
                    var pickUpPosEmpty = GetChainPosition(i, chainBuffer.Length, bestFirePos.xz, waterPos.xz);
                    var dropOffPosEmpty = GetChainPosition(i + 1, chainBuffer.Length, bestFirePos.xz, waterPos.xz);
                    SetComponent(passerEmpty, new BotPickUpLocation() {Value = pickUpPosEmpty});
                    SetComponent(passerEmpty, new BotDropOffLocation() {Value = dropOffPosEmpty});
                }
            }).Schedule();
    }

    static float2 GetChainPosition(int _index, int _chainLength, float2 _startPos, float2 _endPos)
    {
        // adds two to pad between the SCOOPER AND THROWER
        float progress = (float) _index / _chainLength;
        float curveOffset = math.sin(progress * math.PI);

        // get Vec2 data
        float2 heading = _startPos - _endPos;
        float distance = math.length(heading);
        float2 direction = heading / distance;
        float2 perpendicular = new float2(direction.y, -direction.x);

        //Debug.Log("chain progress: " + progress + ",  curveOffset: " + curveOffset);
        return math.lerp(_startPos, _endPos, (float) _index / (float) _chainLength) + perpendicular * curveOffset;
    }
}