﻿
using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Assets.Scripts.Jobs
{
    [BurstCompile]
    public struct PointDisplacementJob : IJobParallelFor
    {
        public NativeArray<VerletPoints> points;       

        public float invDamping;

        [ReadOnly] public TornadoParameters torandoParameters;
        [ReadOnly] public TornadoSettings torandoSettings;
        [ReadOnly] public PhysicsSettings physicSettings;
        [ReadOnly] public NativeArray<PhysicMaterial> physicMaterials;

        public float time;
        public Random random;



        public void Execute(int i)
        {
            VerletPoints point = points[i];
            if (point.anchored > 0) return;

            var mat = physicMaterials[point.materialID];

            //get the combined points by index
            //we check if it's the first one of the list
            float startX = point.currentPosition.x;
            float startY = point.currentPosition.y;
            float startZ = point.currentPosition.z;

            //gravity 
            point.oldPosition.y += physicSettings.gravityForce * mat.weight; 

            // tornado force
            float tdx = torandoParameters.eyePosition.x + (math.sin(point.currentPosition.y / 5f + time / 4f) * 3f) - point.currentPosition.x;
            float tdz = torandoParameters.eyePosition.z - point.currentPosition.z;
            float tornadoDist = math.sqrt(tdx * tdx + tdz * tdz);

            tdx /= tornadoDist;
            tdz /= tornadoDist;

            if (tornadoDist < torandoSettings.tornadoMaxForceDist)
            {
                float force = (1f - tornadoDist / torandoSettings.tornadoMaxForceDist);
                float yFader = math.clamp(1f - point.currentPosition.y / torandoSettings.tornadoHeight, 0f , 1f);
                force *= torandoParameters.tornadoFader * torandoSettings.tornadoForce * random.NextFloat(-.3f, 1.3f) ;
                float forceY = torandoSettings.tornadoUpForce;

                point.oldPosition.y -= (forceY * force) / mat.weight;

                float forceX = -tdz + tdx * (torandoSettings.tornadoInwardForce / mat.weight) * yFader;
                float forceZ = tdx + tdz * torandoSettings.tornadoInwardForce * yFader;

                point.oldPosition.x -= forceX * force / mat.weight;
                point.oldPosition.z -= forceZ * force / mat.weight;
            }

            point.currentPosition.x += (point.currentPosition.x - point.oldPosition.x) * invDamping;
            point.currentPosition.y += (point.currentPosition.y - point.oldPosition.y) * invDamping;
            point.currentPosition.z += (point.currentPosition.z - point.oldPosition.z) * invDamping;

            point.oldPosition.x = startX;
            point.oldPosition.y = startY;
            point.oldPosition.z = startZ;

            if (point.currentPosition.y < 0f)
            {
                point.currentPosition.y = 0f;
                point.oldPosition.y = -point.oldPosition.y;

                point.oldPosition.x += (point.currentPosition.x - point.oldPosition.x) * physicSettings.friction;
                point.oldPosition.z += (point.currentPosition.z - point.oldPosition.z) * physicSettings.friction;
            }

            points[i] = point;
        }
    }
}