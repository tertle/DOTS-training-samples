using System.Collections;
using System.Collections.Generic;
using dots_src.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

public partial class PlatformSpawnerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var splineDataArrayRef = GetSingleton<SplineDataReference>().BlobAssetReference;

        const float platformSize = 30.0f;
        float3 returnPlatformOffset = new float3(20, 0, -8);
        var settings = GetSingleton<Settings>().SettingsBlobRef;
        
        Entities.
            ForEach((Entity entity, in PlatformSpawner spawner) =>
            {
                ref var lineColors = ref settings.Value.LineColors ;
                ecb.DestroyEntity(entity);
                for (var lineId = 0; lineId < splineDataArrayRef.Value.splineBlobAssets.Length; lineId++)
                {
                    //Create the line entities
                    var lineInstance = ecb.Instantiate(spawner.LinePrefab);
                    ecb.SetName(lineInstance, $"Line {lineId}");
                    var entityBuffer = ecb.SetBuffer<EntityBufferElement>(lineInstance);
                    
                    ref var splineBlobAsset = ref splineDataArrayRef.Value.splineBlobAssets[lineId];
                    int nbPlatforms = splineBlobAsset.unitPointPlatformPositions.Length;
                    int halfPlatforms = nbPlatforms / 2;
                    NativeArray<Rotation> outBoundsRotations = new NativeArray<Rotation>(halfPlatforms, Allocator.Temp);
                    NativeArray<float3> outBoundsTranslations = new NativeArray<float3>(halfPlatforms, Allocator.Temp);
                    var lineColor = lineColors[lineId % lineColors.Length];
                    for (int i = 0; i < nbPlatforms; i++)
                    {
                        var platformInstance = ecb.Instantiate(spawner.PlatformPrefab);
                        Translation translation;
                        Rotation rotation = default;
                        if (i < halfPlatforms)
                        {
                            int centerPlatformIndex = (int)math.floor(splineBlobAsset.unitPointPlatformPositions[i] - splineBlobAsset.DistanceToPointUnitDistance(platformSize/2) );
                            var centerPos = splineBlobAsset.equalDistantPoints[centerPlatformIndex];
                            var centerNextPos = splineBlobAsset.equalDistantPoints[centerPlatformIndex + 1];
                            var curPos = splineBlobAsset.PointUnitPosToWorldPos(splineBlobAsset.unitPointPlatformPositions[i]).Item1;
                            (translation ,rotation) = GetStationTransform(curPos,
                                centerPos,
                                centerNextPos);
                            outBoundsRotations[i] = rotation;
                            outBoundsTranslations[i] = translation.Value;
                        }
                        else
                        {
                            var outBoundQuaternion = outBoundsRotations[nbPlatforms - i - 1].Value;
                            var outBoundTranslation = outBoundsTranslations[nbPlatforms - i - 1];
                            var returnTranslation = math.mul(outBoundQuaternion, returnPlatformOffset) + outBoundTranslation;
                            translation = new Translation() {Value = returnTranslation};
                            rotation = new Rotation() {Value = math.mul(quaternion.RotateY(math.PI), outBoundQuaternion)};
                        }

                        ecb.SetName(platformInstance, $"Platform {lineId}-{i}");
                        ecb.SetComponent(platformInstance, rotation);
                        ecb.SetComponent(platformInstance, translation);
                        ecb.AddComponent(platformInstance,
                           new URPMaterialPropertyBaseColor {Value = lineColor});
                        ecb.AddComponent(platformInstance, new Side{IsLeft = i < halfPlatforms});
     
                        entityBuffer.Add(platformInstance);
                    }

                    outBoundsRotations.Dispose();
                    outBoundsTranslations.Dispose();
                }
            }
        ).Run();
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    static (Translation, Rotation) GetStationTransform(float3 curPos, float3 centerPos, float3 centerNextPos)
    {
        var backTrackDir =  math.normalize(centerPos - centerNextPos);
        var forwardPlatformDir = math.cross(backTrackDir, math.up());
        var rotation = new Rotation {Value = quaternion.LookRotation(forwardPlatformDir, math.up())};

        var translation = new Translation {Value = curPos,};
        return (translation, rotation);
    }
}