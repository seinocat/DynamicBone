using Unity.Mathematics;

namespace Seino.DynamicBone
{
    public struct HeadInfo
    {
        public int m_Index;
        public long m_JobUid;
        public int m_JobIndex;
        public float m_DeltaTime;
        public float3 m_ObjectMove;
        public float3 m_ObjectPosition;
        public float3 m_ObjectPrevPosition;
        public quaternion m_ObjectRotation;
        public float3 m_Gravity;
        public float3 m_LocalGravity;
        public float3 m_Force;
        public float3 m_FinalForce;
        public float m_ObjectScale;
        public float m_Weight;
        public int m_ParticleCount;
        public int m_ParticleOffset;
        public float4x4 m_RootWorldToLocalMatrix;

        public bool IsNull;
    }
        
    public struct ParticleInfo
    {
        public int m_Index;
        public int m_ParentIndex;
        public float m_Damping;
        public float m_Elasticity;
        public float m_Stiffness;
        public float m_Inert;
        public float m_Friction;
        public float m_Radius;
        public float m_BoneLength;
        public bool m_IsCollide;

        public float3 m_Position;
        public quaternion m_Rotation;
        public float3 m_PrevPosition;
        public float3 m_InitLocalPosition;
        public quaternion m_InitLocalRotation;

        public float3 m_WorldPosition;
        public float3 m_LocalPosition;
        public quaternion m_LocalRotation;
        public float3 m_ParentScale;
    }
}