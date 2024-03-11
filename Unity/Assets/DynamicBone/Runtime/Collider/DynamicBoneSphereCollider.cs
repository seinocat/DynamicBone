using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class DynamicBoneSphereCollider : DynamicBoneColliderBase
    {
        public float m_Radius = 0.5f;
        private float3 m_WorldCenter;
        private float m_ScaleRadius;
        
        public override void Prepare()
        {
            float scale = math.abs(transform.lossyScale.x);
            m_WorldCenter = transform.TransformPoint(this.m_Center);
            m_ScaleRadius = scale * m_Radius;
        }

        public override bool Collide(ref float3 pos, float radius)
        {
            return this.m_Bound switch
            {
                Bound.Outside => Outside(ref pos, radius, m_WorldCenter, m_ScaleRadius),
                Bound.Inside => Inside(ref pos, radius, m_WorldCenter, m_ScaleRadius),
                _ => Outside(ref pos, radius, m_WorldCenter, m_ScaleRadius)
            };
        }

        private bool Outside(ref float3 particlePos, float particleRadius, float3 centerPos, float radius)
        {
            float r = particleRadius + radius;
            float3 dir = particlePos - centerPos;
            float dist2 = math.lengthsq(dir);
            if (dist2 > 0 && dist2 < r * r)
            {
                dir = math.normalizesafe(dir);
                particlePos = centerPos + dir * r;
                return true;
            }
            return false;
        }

        private bool Inside(ref float3 particlePos, float particleRadius, float3 centerPos, float radius)
        {
            float r = radius - particleRadius;
            float3 dir = particlePos - centerPos;
            float dist2 = math.lengthsq(dir);
            if (dist2 > 0 && dist2 > r * r)
            {
                dir = math.normalize(dir);
                if (math.any(math.isnan(dir)))
                    dir = float3.zero;
                particlePos = centerPos + dir * r;
                return true;
            }
            return false;
        }


#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Prepare();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m_WorldCenter, m_ScaleRadius);
        }
#endif
        
        
    }
}