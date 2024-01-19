using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class Particle
    {
        public Transform m_Transform;
        public int m_ParentIndex;
        public int m_ChildCount;
        public float m_Damping;
        public float m_Elasticity;
        public float m_Stiffness;
        public float m_Inert;
        public float m_Friction;
        public float m_Radius;
        public float m_BoneLength;
        public bool m_IsCollide;

        public float3 m_Position;
        public float3 m_PrevPosition;
        public float3 m_InitLocalPosition;
        public quaternion m_InitLocalRotation;

        public float3 m_TransformPosition;
        public float3 m_TransformLocalPosition;
        public float4x4 m_TransformLocalToWorldMatrix;
    }
}