using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Mathf = UnityEngine.Mathf;
using Unity.Collections;

[UpdateBefore(typeof(ParticleSystemFixed))]
[UpdateAfter(typeof(Systems.TargetSystem))]
public partial class BeeMovementSystem : SystemBase
{
    static readonly float flightJitter = 200f;
    static readonly float damping = 0.1f;
    static readonly float speedStretch = 0.2f;
    static readonly float teamAttraction = 5f;
    static readonly float teamRepulsion = 4f;

    static readonly float chaseForce = 50f;
    static readonly float attackDistance = 4f;
    static readonly float attackForce = 500f;
    static readonly float hitDistance = 0.5f;

    EntityQuery[] teamTargets;
    EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    protected override void OnCreate()
    {
        teamTargets = new EntityQuery[2]
        {
            EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Translation>(), ComponentType.ReadOnly<TeamShared>()),
            EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Translation>(), ComponentType.ReadOnly<TeamShared>())
        };
        teamTargets[0].SetSharedComponentFilter(new TeamShared { TeamId = 0 });
        teamTargets[1].SetSharedComponentFilter(new TeamShared { TeamId = 1 }); 
        
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var particles = GetSingleton<ParticleSettings>();

        var deltaTime = Time.DeltaTime;
        var random = Random.CreateFromIndex(GlobalSystemVersion);

        var team0 = teamTargets[0].ToComponentDataArray<Translation>(Allocator.TempJob);
        var team1 = teamTargets[1].ToComponentDataArray<Translation>(Allocator.TempJob);

        var ecb = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

        // Run attraction/repulsion gather. Uses read access of Translation from input arrays and only writes attraction data.
        // Could we run this on a lower cadence?
        Entities
            .WithReadOnly(team0)
            .WithDisposeOnCompletion(team0)
            .WithReadOnly(team1)
            .WithDisposeOnCompletion(team1)
            .ForEach((ref AttractionRepulsion attractionRepulsion, in Team team) =>
            {
                // Not unsetting any of these at the moment!
                if (team.TeamId == 0 )
                {                    
                    if (team0.Length != 0)
                        attractionRepulsion.AttractionPos = team0[random.NextInt(team0.Length)].Value;
                    if (team1.Length != 0)
                        attractionRepulsion.RepulsionPos = team1[random.NextInt(team1.Length)].Value;
                }
                else
                {
                    if (team1.Length != 0)
                        attractionRepulsion.AttractionPos = team1[random.NextInt(team1.Length)].Value;
                    if (team0.Length != 0)
                        attractionRepulsion.RepulsionPos = team0[random.NextInt(team0.Length)].Value;
                }
            }).ScheduleParallel();

        Entities
            .ForEach((int entityInQueryIndex, ref Translation translation, ref NonUniformScale scale, ref BeeMovement bee, ref TargetType targetType, in TargetEntity targetEntity, in AttractionRepulsion attraction, in CachedTargetPosition targetPos) =>
            {
                if (!UpdateBee(ref translation, ref scale, ref bee, ref random, attraction, targetType.Value, targetPos.Value, deltaTime))
                {
                    ParticleSystem.SpawnParticle(ecb, entityInQueryIndex, particles.Particle, random, targetPos.Value, ParticleComponent.ParticleType.Blood, bee.Velocity * .35f, 2f, 6);
                    ecb.DestroyEntity(entityInQueryIndex, targetEntity.Value);
                    targetType.Value = TargetType.Type.None;

                }
            }).ScheduleParallel();

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);

    }

    private static bool UpdateBee(ref Translation translation,
        ref NonUniformScale scale,
        ref BeeMovement bee,
        ref Random random,
        AttractionRepulsion attraction,
        TargetType.Type targetType,
        float3 targetPos,
        float deltaTime)
    {
        var velocity = bee.Velocity;
        var position = translation.Value;
        UpdateJitterAndTeamVelocity(ref random, ref velocity, in position, in attraction, deltaTime);

        if (targetType == TargetType.Type.Enemy)
        {
            var delta = targetPos - position;
            float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
            if (sqrDist > attackDistance * attackDistance)
            {
                velocity += delta * (chaseForce * deltaTime / Mathf.Sqrt(sqrDist));
            }
            else
            {
               // bee.isAttacking = true;
                velocity += delta * (attackForce * deltaTime / Mathf.Sqrt(sqrDist));
                if (sqrDist < hitDistance * hitDistance)
                {
                    return false;
                }
            }
        }

        position += velocity * deltaTime;
        UpdateBorders(ref velocity, ref position);
        bee.Velocity = velocity;
        translation.Value = position;
        UpdateScale(ref scale, in bee, in velocity);
        return true;
    }

    private static void UpdateJitterAndTeamVelocity(ref Random random, ref float3 velocity, in float3 position, in AttractionRepulsion attraction, float deltaTime)
    {
        velocity += random.NextFloat3Direction() * (flightJitter * deltaTime);
        velocity *= 1f - damping;

        var delta = attraction.AttractionPos - position;
        float dist = Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
        if (dist > 0f)
        {
            velocity += delta * (teamAttraction * deltaTime / dist);
        }

        delta = attraction.RepulsionPos - position;
        dist = Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
        if (dist > 0f)
        {
            velocity -= delta * (teamRepulsion * deltaTime / dist);
        }
    }

    private static void UpdateBorders(ref float3 velocity, ref float3 position)
    {
        if (Mathf.Abs(position.x) > PlayField.size.x * .5f)
        {
            position.x = PlayField.size.x * .5f * Mathf.Sign(position.x);
            velocity.x *= -0.5f;
            velocity.y *= .8f;
            velocity.z *= .8f;
        }
        if (Mathf.Abs(position.z) > PlayField.size.z * .5f)
        {
            position.z = PlayField.size.z * .5f * Mathf.Sign(position.z);
            velocity.z *= -0.5f;
            velocity.x *= .8f;
            velocity.y *= .8f;
        }
        if (Mathf.Abs(position.y) > PlayField.size.y * .5f)
        {
            position.y = PlayField.size.y * .5f * Mathf.Sign(position.y);
            velocity.y *= -0.5f;
            velocity.z *= .8f;
            velocity.x *= .8f;
        }
    }

    private static void UpdateScale(ref NonUniformScale scale, in BeeMovement bee, in float3 velocity)
    {
        var size = bee.Size;
        var scl = new float3(size, size, size);
        float stretch = Mathf.Max(1f, math.distance(velocity, float3.zero) * speedStretch);
        scl.z *= stretch;
        scl.x /= (stretch - 1f) / 5f + 1f;
        scl.y /= (stretch - 1f) / 5f + 1f;
        scale.Value = scl;
    }
}