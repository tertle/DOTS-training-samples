using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class FarmerAuthoring : MonoBehaviour
{
    public float3 velocity;
    public float3 Target;

    // Start is called before the first frame update
}

public class FarmerComponentBaker : Baker<FarmerAuthoring>
{
    
    public override void Bake(FarmerAuthoring authoring)
    {
        AddComponent(new FarmerSpeed());
        AddComponent(new Farmer());
        AddComponent(new TargetPosition
        {
            Target = authoring.Target
        });
        AddComponent(new Velocity()
        {
            value = authoring.velocity
        });
    }
}
