﻿using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
struct BucketBrigadeConfig : IComponentData
{
    public float TemperatureIncreaseRate;
    public float Flashpoint;
    public int2 GridDimensions;
    public float CellSize;

    public float WaterSourceRefillRate;
    public float BucketCapacity;
    public float BucketRadius;
    public float AgentRadius;
    public float AgentSpeed;
    public int NumberOfBuckets;

    public int StartingFireCount;
    public float MaxFlameHeight;
    public int HeatRadius;

}
