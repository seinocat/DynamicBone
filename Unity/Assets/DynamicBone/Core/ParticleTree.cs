using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class ParticleTree
    {
        public Transform m_Root;
        public float3 m_LocalGravity;
        public float3 m_RestGravity;
        public float4x4 m_RootWorldToLocalMatrix;
        public float m_BoneTotalLength;
        public List<Particle> m_Particles = new();
    }
}