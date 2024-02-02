using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class DynamicBoneJob : MonoBehaviour
    {
        [Title("全局设置")]
        [LabelText("重力")]
        public float3 m_Gravity = float3.zero;

        [LabelText("外力")] 
        public float3 m_Force;
        
        [Range(0, 1)]
        [LabelText("权重")]
        public float m_BlendWeight = 1.0f;

        [LabelText("根节点")]
        public List<Transform> m_Roots;
        
        [LabelText("结束节点")]
        public List<Transform> m_EndRoots;
        
        [Title("参数")]
        [Range(0, 1)]
        [LabelText("阻尼")]
        public float m_Damping = 0.1f;
        [LabelText("阻尼曲线")]
        public AnimationCurve m_DampingCurve;
        
        [Space]
        [Range(0, 1)]
        [LabelText("弹性")]
        public float m_Elasticity = 0.1f;
        [LabelText("弹性曲线")]
        public AnimationCurve m_ElasticityCurve;
        
        [Space]
        [Range(0, 1)]
        [LabelText("刚性")]
        public float m_Stiffness = 0.1f;
        [LabelText("刚性曲线")]
        public AnimationCurve m_StiffnessCurve;
        
        [Space]
        [Range(0, 1)]
        [LabelText("摩擦力")]
        public float m_Friction = 0.1f;
        [LabelText("摩擦力曲线")]
        public AnimationCurve m_FrictionCurve;
        
        [Space]
        [Range(0, 1)]
        [LabelText("惯性")]
        public float m_Inert = 0.5f;
        [LabelText("惯性曲线")]
        public AnimationCurve m_InertCurve;

        [Space]
        [LabelText("质点半径")] 
        public float m_Radius;
        [LabelText("半径曲线")] 
        public AnimationCurve m_RadiusCurve;

        private float m_BoneTotalLength;
        private float m_Weight = 1.0f;
        private int m_ParticleCount;
        private bool m_Inited;

        public List<HeadInfo> HeadInfos;
        public List<NativeArray<ParticleInfo>> ParticleInfos;
        public List<Transform[]> ParticleTransforms;

        private void Awake()
        {
            if (m_Roots == null)
                return;
            SetupParticles();
            DynamicBoneManager.Instance.AddBone(this);
            m_Inited = true;
        }

        private void OnDestroy()
        {
            DynamicBoneManager.Instance.RemoveBone(this);
        }

        private void OnValidate()
        {
            if (!m_Inited)
                return;
            
            m_Damping = Mathf.Clamp01(m_Damping);
            m_Elasticity =  Mathf.Clamp01(m_Elasticity);
            m_Stiffness =  Mathf.Clamp01(m_Stiffness);
            m_Inert =  Mathf.Clamp01(m_Inert);
            m_Friction = Mathf.Clamp01(m_Friction);
            m_Radius = Mathf.Abs(m_Radius);

            if (Application.isEditor && Application.isPlaying)
            {
                InitTransforms();
                UpdateParameters();
            }
        }
        
        private void SetupParticles()
        {
            HeadInfos = new List<HeadInfo>();
            ParticleInfos = new List<NativeArray<ParticleInfo>>();
            ParticleTransforms = new List<Transform[]>();
            AppendParticles();
            UpdateParameters();
        }

        private void AppendParticles()
        {
            for (int i = 0; i < m_Roots.Count; i++)
            {
                if (m_Roots[i] == null)
                    continue;
                var root = m_Roots[i];
                m_ParticleCount = 0;
                m_BoneTotalLength = 0;
            
                HeadInfo headInfo = new HeadInfo();
                headInfo.m_ObjectScale = math.abs(transform.lossyScale.x);
                headInfo.m_ObjectPrevPosition = transform.position;
                headInfo.m_ObjectMove = float3.zero;
                headInfo.m_Weight = m_Weight;
                headInfo.m_Force = m_Force;
                headInfo.m_ParticleCount = 0;
                headInfo.m_RootWorldToLocalMatrix = root.worldToLocalMatrix;
                
                NativeArray<ParticleInfo> particels = new NativeArray<ParticleInfo>(DynamicBoneManager.MAX_PARTICLE_COUNT, Allocator.Persistent);
                Transform[] transforms = new Transform[DynamicBoneManager.MAX_PARTICLE_COUNT];
                AppendParticle(ref particels, ref transforms, root, -1, 0);
                
                headInfo.m_ParticleCount = m_ParticleCount;
                
                ParticleInfos.Add(particels);
                ParticleTransforms.Add(transforms);
                HeadInfos.Add(headInfo);
            }
        }
        
        private void AppendParticle(ref NativeArray<ParticleInfo> particles, ref Transform[] transforms, Transform b, int parentIndex, float boneLength)
        {
            var p = new ParticleInfo();
            p.m_Index = m_ParticleCount;
            p.m_ParentIndex = parentIndex;

            m_ParticleCount++;
            
            if (b != null)
            {
                p.m_WorldPosition = p.m_Position = p.m_PrevPosition = b.position;
                p.m_LocalPosition = p.m_InitLocalPosition = b.localPosition;
                p.m_LocalRotation = p.m_InitLocalRotation = b.localRotation;
                p.m_Rotation = b.rotation;
                p.m_ParentScale = b.parent.lossyScale;
            }

            if (parentIndex >= 0)
            {
                boneLength += math.distance(transforms[parentIndex].position, p.m_WorldPosition);
                p.m_BoneLength = boneLength;
                m_BoneTotalLength = math.max(m_BoneTotalLength, boneLength);
            }

            int index = p.m_Index;
            particles[index] = p;
            transforms[index] = b;

            if (b != null && !m_EndRoots.Contains(b))
            {
                for (int i = 0; i < b.childCount; i++)
                {
                    var child = b.GetChild(i);
                    AppendParticle(ref particles, ref transforms, child, index, boneLength);
                }
            }
        }
        
        
        private void UpdateParameters()
        {
            for (int i = 0; i < HeadInfos.Count; i++)
            {
                var head = HeadInfos[i];
                head.m_LocalGravity = math.normalize(math.mul(head.m_RootWorldToLocalMatrix, new float4(m_Gravity, 0.0f)).xyz) * math.length(m_Gravity);
                var particleInfos = ParticleInfos[i];
                for (int j = 0; j < head.m_ParticleCount; j++)
                {
                    var p = particleInfos[j];
                    p.m_Damping = Mathf.Clamp01(this.m_Damping);
                    p.m_Elasticity =  Mathf.Clamp01(this.m_Elasticity);
                    p.m_Stiffness =  Mathf.Clamp01(this.m_Stiffness);
                    p.m_Inert =  Mathf.Clamp01(this.m_Inert);
                    p.m_Friction = Mathf.Clamp01(this.m_Friction);
                    p.m_Radius = Mathf.Abs(this.m_Radius);
                    
                    if (m_BoneTotalLength > 0)
                    {
                        float samplePos = p.m_BoneLength / m_BoneTotalLength;
                        if (m_DampingCurve != null && m_DampingCurve.keys.Length > 0)
                            p.m_Damping *= m_DampingCurve.Evaluate(samplePos);
                        if (m_ElasticityCurve != null && m_ElasticityCurve.keys.Length > 0)
                            p.m_Elasticity *= m_ElasticityCurve.Evaluate(samplePos);
                        if (m_StiffnessCurve != null && m_StiffnessCurve.keys.Length > 0)
                            p.m_Stiffness *= m_StiffnessCurve.Evaluate(samplePos);
                        if (m_InertCurve != null && m_InertCurve.keys.Length > 0)
                            p.m_Inert *= m_InertCurve.Evaluate(samplePos);
                        if (m_FrictionCurve != null && m_FrictionCurve.keys.Length > 0)
                            p.m_Friction *= m_FrictionCurve.Evaluate(samplePos);
                        if (m_RadiusCurve != null && m_RadiusCurve.keys.Length > 0)
                            p.m_Radius *= m_RadiusCurve.Evaluate(samplePos);
                    }
                    
                    p.m_Damping = Mathf.Clamp01(p.m_Damping);
                    p.m_Elasticity =  Mathf.Clamp01(p.m_Elasticity);
                    p.m_Stiffness =  Mathf.Clamp01(p.m_Stiffness);
                    p.m_Inert =  Mathf.Clamp01(p.m_Inert);
                    p.m_Friction = Mathf.Clamp01(p.m_Friction);
                    p.m_Radius = Mathf.Abs(p.m_Radius);

                    particleInfos[j] = p;
                }
            }
        }
        
        private void InitTransforms()
        {
            for (int i = 0; i < ParticleInfos.Count; i++)
            {
                var particle = ParticleInfos[i];
                for (int j = 0; j < particle.Length; j++)
                {
                    var p = particle[i];
                    p.m_LocalPosition = p.m_InitLocalPosition;
                    p.m_LocalRotation = p.m_InitLocalRotation;
                    particle[i] = p;
                }
            }
        }
        
    }
}