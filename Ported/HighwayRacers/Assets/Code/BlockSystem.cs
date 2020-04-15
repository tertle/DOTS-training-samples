﻿using Unity.Collections;
using Unity.Entities;

[UpdateAfter(typeof(PercentCompleteSystem))]
public class BlockSystem : SystemBase
{
    private EntityQuery agentQuery;

    private EntityCommandBufferSystem entityCommandBufferSystem;

    protected override void OnCreate()
    {
        agentQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<MinimumDistance>(),
                ComponentType.ReadOnly<PercentComplete>(),
                ComponentType.ReadOnly<LaneAssignment>(),
                ComponentType.ReadOnly<Speed>(),
            }
        });

        entityCommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        // // Allocate an array of all lane assignments in the world.
        // var laneAssignments =
        //     agentQuery.ToComponentDataArrayAsync<LaneAssignment>(Allocator.TempJob, out var laneAssignmentsHandle);
        //
        // // Allocate an array of all percents in the world.
        // var percentCompletes =
        //     agentQuery.ToComponentDataArrayAsync<PercentComplete>(Allocator.TempJob, out var percentCompletesHandle);
        //
        // // Allocate an array of all speeds in the world.
        // var speeds =
        //     agentQuery.ToComponentDataArrayAsync<Speed>(Allocator.TempJob, out var speedHandle);
        //
        // var agentEntities = agentQuery.ToEntityArrayAsync(Allocator.TempJob, out var agentEntitiesHandle);
        //
        // var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
        //
        // Entities
        //     // Used to help mark the lambda in the profiler.
        //     .WithName("block_system")
        //     // Only get entities that have a BlockSpeed component.
        //     .WithNone<BlockSpeed>()
        //     .WithAll<MinimumDistance, PercentComplete, PercentComplete>()
        //     // Dealocate the arrays when this job completes.
        //     .WithDeallocateOnJobCompletion(laneAssignments)
        //     .WithDeallocateOnJobCompletion(percentCompletes)
        //     .WithDeallocateOnJobCompletion(speeds)
        //     .WithDeallocateOnJobCompletion(agentEntities)
        //     // Iterate through each entity with the following:
        //     .ForEach((int entityInQueryIndex, Entity entity, in MinimumDistance minimumDistance,
        //     in PercentComplete percentComplete, in LaneAssignment currentLane) =>
        //     {
        //         for (int i = 0; i < laneAssignments.Length; ++i)
        //         {
        //             if (entity.Index != agentEntities[i].Index)
        //             {
        //                 continue;
        //             }
        //
        //             // Ignore agents that are not in the same lane.
        //             if (currentLane.Value != laneAssignments[i].Value)
        //             {
        //                 continue;
        //             }
        //
        //             // Ignore agents that are behind this agent.
        //             if (percentCompletes[i].Value < 0)
        //             {
        //                 continue;
        //             }
        //
        //             // Get the distance of between this agent and the other agent, with agents in front yielding a positive value.
        //             float distance = percentCompletes[i].Value - percentComplete.Value;
        //
        //             // If the car is not behind the agent, and the distance is within the minimum distance between two agents, then:
        //             if (distance < minimumDistance.Value)
        //             {
        //                 // Add a BlockSpeed component with the speed of the car that is ahead of it.
        //                 entityCommandBuffer.AddComponent(entity, new BlockSpeed { Value = speeds[i].Value });
        //             }
        //         }
        //     })
        //     .ScheduleParallel();
        //
        // entityCommandBuffer.Playback(EntityManager);
    }
}