﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class FireRenderSystem : SystemBase
{
    const float flashPoint = 0.2f;

    protected override void OnCreate()
    {
        GetEntityQuery(typeof(FireCell));
    }

    private static float Noise(float t, float amplitude, float frequency, float phase)
    {
        return amplitude * math.sin(frequency * t + phase);
    }

    protected override void OnUpdate()
    {
        var fireGridEntity = GetSingletonEntity<FireGridSettings>();
        var bufferRef = EntityManager.GetBuffer<FireCell>(fireGridEntity);
        float t = (float)Time.ElapsedTime;

        Entities
        .WithAll<FireRendererTag>()
        .WithReadOnly(bufferRef)
        .ForEach((int entityInQueryIndex, ref NonUniformScale scale, ref Translation translation, ref FireColor fireColor) =>
        {
            // Grab the target cell this renderer should be matching
            FireCell currentCell = bufferRef[entityInQueryIndex];

            float visualTemperature = currentCell.FireTemperature < flashPoint ? 0.001f : math.min(currentCell.FireTemperature, 1.0f);

            float noise = visualTemperature < flashPoint ? math.max(Noise(0, 0.02f, 2.0f, entityInQueryIndex * 0.8f), 0.001f) : 
                Noise(t, 0.02f, 2.0f, entityInQueryIndex * 0.8f) + 
                Noise(t, 0.03f, 3.0f, entityInQueryIndex * 0.9f) + 
                Noise(t, 0.04f, 4.0f, entityInQueryIndex * 1.0f);
            float actualTemperatureWithNoise = visualTemperature + noise;

            // Update its scale
            scale.Value.y = actualTemperatureWithNoise;
            // Update its translation
            translation.Value.y = scale.Value.y * 0.5f;
            // Update its color
            fireColor.Value = visualTemperature < flashPoint ? new float4(0.0f, 1.0f, 0.0f, 1.0f) : math.lerp(new float4(1.0f, 1.0f, 0.0f, 1.0f), new float4(1.0f, 0.0f, 0.0f, 1.0f), (visualTemperature  - flashPoint) / (1.0f - flashPoint));
        }).ScheduleParallel();
    }
}
