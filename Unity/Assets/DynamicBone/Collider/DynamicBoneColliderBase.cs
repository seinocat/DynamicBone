using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public enum Bound
    {
        Outside,
        Inside
    }
    
    public class DynamicBoneColliderBase : MonoBehaviour
    {
        public float3 m_Center = float3.zero;

        public Bound m_Bound = Bound.Outside;

        public int PrepareFrame { get; set; }
        
        public virtual void Prepare(){}

        public virtual bool Collide(ref float3 pos, float radius)
        {
            return false;
        }
    }
}