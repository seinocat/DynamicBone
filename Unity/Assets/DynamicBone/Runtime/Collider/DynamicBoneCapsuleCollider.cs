using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public enum DirectionType
    {
        [InspectorName("X-Axis")]
        x,
        [InspectorName("Y-Axis")]
        y,
        [InspectorName("Z-Axis")]
        z
    }
    
    public class DynamicBoneCapsuleCollider : DynamicBoneColliderBase
    {
        public float Radius = 0.1f;
        public float Height = 0.3f;
        public DirectionType Direction = DirectionType.y;
        
        private float3 m_SphereCenter0;
        private float3 m_SphereCenter1;

        public override void Prepare()
        {
            var c0 = m_Center;
            var c1 = m_Center;
            var h = 0.5f * Height - Radius;
            
            switch(Direction)
            {
                case DirectionType.x:
                    c0.x += h;
                    c1.x -= h;
                    break;
                case DirectionType.y:
                    c0.y += h;
                    c1.y -= h;
                    break;
                case DirectionType.z:
                    c0.z += h;
                    c1.z -= h;
                    break;
            }
            
            m_SphereCenter0 = transform.TransformPoint(c0);
            m_SphereCenter1 = transform.TransformPoint(c1);
        }


        public override bool Collide(ref float3 pos, float radius)
        {
            return m_Bound switch
            {
                Bound.Outside => Outside(ref pos, radius, m_SphereCenter0, m_SphereCenter1, Radius),
                Bound.Inside => Inside(ref pos, radius, m_SphereCenter0, m_SphereCenter1, Radius),
                _ => Outside(ref pos, radius, m_SphereCenter0, m_SphereCenter1, Radius)
            };
        }


        private bool Outside(ref float3 particlePos, float particleRadius, float3 p0, float3 p1, float radius)
        {
            float3 d = p1 - p0;
            float t = math.dot(particlePos - p0, d);
            float d2 = math.lengthsq(d);
            float r = particleRadius + radius;

            if (t <= 0)
            {
                float3 dir = particlePos - p0;
                float dist2 = math.lengthsq(dir);
                if (dist2 > 0 && dist2 < r * r)
                {
                    dir = math.normalizesafe(dir);
                    particlePos = p0 + dir * r;
                    return true;
                }
            }
            else
            {
                if (t >= d2)
                {
                    float3 dir = particlePos - p1;
                    float dist2 = math.lengthsq(dir);
                    if (dist2 > 0 && dist2 < r * r)
                    {
                        dir = math.normalizesafe(dir);
                        particlePos = p0 + dir * r;
                        return true;
                    }
                }
                else
                {
                    float3 dir = particlePos - p0;
                    float3 dnormal = dir - d * (t / d2);
                    float dn2 = math.lengthsq(dnormal);
                    if (dn2 > 0 && dn2 < r * r)
                    {
                        float dn = math.sqrt(dn2);
                        particlePos += dnormal * ((r - dn) / dn);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Inside(ref float3 particlePos, float particleRadius, float3 p0, float3 p1, float radius)
        {
            return false;
        }
        

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Prepare();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m_SphereCenter0, Radius);
            Gizmos.DrawWireSphere(m_SphereCenter1, Radius);

            if (Direction is DirectionType.y or DirectionType.z)
            {
                Gizmos.DrawLine((Vector3)m_SphereCenter0 + Vector3.right * Radius, (Vector3)m_SphereCenter1 + Vector3.right * Radius);
                Gizmos.DrawLine((Vector3)m_SphereCenter0 - Vector3.right * Radius, (Vector3)m_SphereCenter1 - Vector3.right * Radius);
            }
            if (Direction is DirectionType.x or DirectionType.y)
            {
                Gizmos.DrawLine((Vector3)m_SphereCenter0 + Vector3.forward * Radius, (Vector3)m_SphereCenter1 + Vector3.forward * Radius);
                Gizmos.DrawLine((Vector3)m_SphereCenter0 - Vector3.forward * Radius, (Vector3)m_SphereCenter1 - Vector3.forward * Radius);
            }
            if(Direction is DirectionType.x or DirectionType.z)
            {
                Gizmos.DrawLine((Vector3)m_SphereCenter0 + Vector3.up * Radius, (Vector3)m_SphereCenter1 + Vector3.up * Radius);
                Gizmos.DrawLine((Vector3)m_SphereCenter0 - Vector3.up * Radius, (Vector3)m_SphereCenter1 - Vector3.up * Radius);
            }
            
        }
#endif
        

    }
}