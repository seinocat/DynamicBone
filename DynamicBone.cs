using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class DynamicBone : MonoBehaviour
    {
        [Title("全局设置")]
        [LabelText("帧率")]
        public float m_UpdateRate = 60;
        
        [LabelText("重力")]
        public float3 m_Gravity = new(0, -0.002f, 0);

        [LabelText("外力")] 
        public float3 m_Force;
        
        [Range(0, 1)]
        [LabelText("权重")]
        public float m_BlendWeight = 1.0f;

        [LabelText("根节点")]
        public List<Transform> m_Roots;

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
        [LabelText("惯性")]
        public float m_Inert = 0.5f;
        [LabelText("惯性曲线")]
        public AnimationCurve m_InertCurve;

        [Space]
        [LabelText("质点半径")] 
        public float m_Radius;
        [LabelText("半径曲线")] 
        public AnimationCurve m_RadiusCurve;
        
        private List<ParticleTree> m_ParticleTrees = new();

        private float3 m_ObjectMove;
        private float3 m_ObjectPrevPosition;
        private float m_ObjectScale;

        private float m_Time;
        private float m_Weight = 1.0f;
        private int m_PreUpdateCount;
        private float m_DeltaTime;
        
        private static int s_UpdateCount;
        private static int s_PrepareFrame;

        private void Start()
        {
            SetupParticles();
        }

        private void Update()
        {
            PreUdpate();
            ++s_UpdateCount;
        }

        private void LateUpdate()
        {
            if (m_PreUpdateCount == 0) return;

            if (s_UpdateCount > 0)
            {
                s_UpdateCount = 0;
                ++s_PrepareFrame;
            }
            
            SetWeight(m_BlendWeight);
            if (m_Weight > 0)
            {
                Prepare();
                UpdateParticles();
                ApplyParticles();
            }

            m_PreUpdateCount = 0;
        }

        private void OnValidate()
        {
            UpdateParameters();
        }

        #region setup

        private void SetupParticles()
        {
            m_ParticleTrees.Clear();

            if (m_Roots is { Count: > 0 })
            {
                foreach (var root in m_Roots)
                {
                    if (root == null) continue;
                    if (m_ParticleTrees.Exists(x=>x.m_Root == root)) continue;
                    
                    AppendParticleTree(root);
                }
            }

            m_ObjectScale = math.abs(transform.lossyScale.x);
            m_ObjectPrevPosition = transform.position;
            m_ObjectMove = float3.zero;

            foreach (var pt in m_ParticleTrees)
            {
                AppendParticles(pt, pt.m_Root, -1, 0);
            }
        }

        private void AppendParticleTree(Transform root)
        {
            var pt = new ParticleTree();
            pt.m_Root = root;
            pt.m_RootWorldToLocalMatrix = root.worldToLocalMatrix;
            m_ParticleTrees.Add(pt);
        }

        private void AppendParticles(ParticleTree pt, Transform b, int parentIndex, float boneLength)
        {
            var p = new Particle();
            p.m_Transform = b;
            p.m_ParentIndex = parentIndex;

            if (b != null)
            {
                p.m_Position = p.m_PrevPosition = b.position;
                p.m_InitLocalPosition = b.localPosition;
                p.m_InitLocalRotation = b.localRotation;
            }

            if (parentIndex >= 0)
            {
                boneLength += (pt.m_Particles[parentIndex].m_Transform.position - p.m_Transform.position).magnitude;
                p.m_BoneLength = boneLength;
                pt.m_BoneTotalLength = math.max(pt.m_BoneTotalLength, boneLength);
                ++pt.m_Particles[parentIndex].m_ChildCount;
            }

            int index = pt.m_Particles.Count;
            pt.m_Particles.Add(p);

            if (b != null)
            {
                for (int i = 0; i < b.childCount; i++)
                {
                    var child = b.GetChild(i);
                    AppendParticles(pt, child, index, boneLength);
                }
            }

            UpdateParameters();
        }

        private void UpdateParameters()
        {
            SetWeight(m_BlendWeight);

            foreach (var pt in m_ParticleTrees)
            {
                UpdateParameters(pt);
            }
        }

        private void UpdateParameters(ParticleTree pt)
        {
            pt.m_LocalGravity = math.normalize(math.mul(pt.m_RootWorldToLocalMatrix, new float4(m_Gravity, 1.0f)).xyz) * math.length(m_Gravity);

            foreach (var p in pt.m_Particles)
            {
                if (pt.m_BoneTotalLength > 0)
                {
                    float samplePos = p.m_BoneLength / pt.m_BoneTotalLength;
                    if (m_DampingCurve != null && m_DampingCurve.keys.Length > 0)
                        p.m_Damping *= m_DampingCurve.Evaluate(samplePos);
                    if (m_ElasticityCurve != null && m_ElasticityCurve.keys.Length > 0)
                        p.m_Elasticity *= m_ElasticityCurve.Evaluate(samplePos);
                    if (m_StiffnessCurve != null && m_StiffnessCurve.keys.Length > 0)
                        p.m_Stiffness *= m_StiffnessCurve.Evaluate(samplePos);
                    if (m_InertCurve != null && m_InertCurve.keys.Length > 0)
                        p.m_Inert *= m_InertCurve.Evaluate(samplePos);
                    if (m_RadiusCurve != null && m_RadiusCurve.keys.Length > 0)
                        p.m_Radius *= m_RadiusCurve.Evaluate(samplePos);
                }
                
                p.m_Damping = Mathf.Clamp01(this.m_Damping);
                p.m_Elasticity =  Mathf.Clamp01(this.m_Elasticity);
                p.m_Stiffness =  Mathf.Clamp01(this.m_Stiffness);
                p.m_Inert =  Mathf.Clamp01(this.m_Inert);
                p.m_Radius = Mathf.Abs(p.m_Radius);
            }
        }

        private void SetWeight(float weight)
        {
            if (math.abs(m_Weight - weight) > 0.001)
            {
                if (weight == 0)
                {
                    InitTransforms();
                }else if (m_Weight == 0)
                {
                    ResetParticlesPosition();
                }

                m_Weight = m_BlendWeight = weight;
            }
        }

        private void InitTransforms()
        {
            foreach (var pt in m_ParticleTrees)
            {
                InitTransforms(pt);
            }
        }

        private void InitTransforms(ParticleTree pt)
        {
            foreach (var p in pt.m_Particles)
            {
                p.m_Transform.localPosition = p.m_InitLocalPosition;
                p.m_Transform.localRotation = p.m_InitLocalRotation;
            }
        }

        private void ResetParticlesPosition()
        {
            foreach (var pt in m_ParticleTrees)
            {
                ResetParticlesPosition(pt);
            }

            m_ObjectPrevPosition = transform.position;
        }

        private void ResetParticlesPosition(ParticleTree pt)
        {
            foreach (var p in pt.m_Particles)
            {
                p.m_Position = p.m_PrevPosition = p.m_Transform.position;
            }
        }

        #endregion

        #region update

        private void PreUdpate()
        {
            if (m_Weight > 0)
            {
                InitTransforms();
            }
            ++m_PreUpdateCount;
        }

        private void Prepare()
        {
            m_DeltaTime = Time.deltaTime;
            m_ObjectScale = math.abs(transform.lossyScale.x);
            m_ObjectMove = (float3)transform.position - m_ObjectPrevPosition;
            m_ObjectPrevPosition = transform.position;

            foreach (var pt in m_ParticleTrees)
            {
                pt.m_RestGravity = pt.m_Root.TransformDirection(pt.m_LocalGravity);
                foreach (var p in pt.m_Particles)
                {
                    p.m_TransformPosition = p.m_Transform.position;
                    p.m_TransformLocalPosition = p.m_Transform.localPosition;
                    p.m_TransformLocalToWorldMatrix = p.m_Transform.localToWorldMatrix;
                }
            }
        }

        private void UpdateParticles()
        {
            int loop = 1;
            float timeVar = 1;
            float dt = m_DeltaTime;

            if (m_UpdateRate > 0)
            {
                timeVar = dt * m_UpdateRate;
            }

            if (loop > 0)
            {
                for (int i = 0; i < loop; i++)
                {
                    UpdateParticles1(timeVar, i);
                    UpdateParticles2(timeVar);
                }
            }
            else
            {
                SkipUpdateParticle();
            }
        }

        private void UpdateParticles1(float timeVar, int loop)
        {
            foreach (var pt in m_ParticleTrees)
            {
                UpdateParticles1(pt, timeVar, loop);
            }
        }

        private void UpdateParticles1(ParticleTree pt, float timeVar, int loopIndex)
        {
            float3 force = m_Gravity;
            float3 fdir = math.normalize(m_Gravity);
            if (math.all(math.isnan(fdir))) fdir = float3.zero;
            float3 pf = fdir * math.max(math.dot(pt.m_RestGravity, fdir), 0);
            force -= pf;
            force = (force + m_Force) * (m_ObjectScale * timeVar);

            float3 objectMove = loopIndex == 0 ? m_ObjectMove : float3.zero;

            foreach (var p in pt.m_Particles)
            {
                if (p.m_ParentIndex >= 0)
                {
                    float3 v = p.m_Position - p.m_PrevPosition;
                    float3 rmove = objectMove * p.m_Inert;
                    p.m_PrevPosition = p.m_Position + rmove;
                    p.m_Position += v * (1 - p.m_Damping) + force + rmove;
                }
                else
                {
                    p.m_PrevPosition = p.m_Position;
                    p.m_Position = p.m_TransformPosition;
                }
            }
        }
        
        private void UpdateParticles2(float timeVar)
        {
            foreach (var pt in m_ParticleTrees)
            {
                UpdateParticles2(pt, timeVar);
            }
        }

        private void UpdateParticles2(ParticleTree pt, float timeVar)
        {
            for (var i = 1; i < pt.m_Particles.Count ; ++i)
            {
                var p = pt.m_Particles[i];
                var p0 = pt.m_Particles[p.m_ParentIndex];
                float restLen = math.length(p0.m_TransformPosition - p.m_TransformPosition);

                float stiffness = math.lerp(1.0f, p.m_Stiffness, m_Weight);
                if (stiffness > 0 || p.m_Elasticity > 0)
                {
                    var matrix = p0.m_TransformLocalToWorldMatrix;
                    matrix.c3 = new float4(p0.m_Position, 1.0f);
                    float3 restPos = math.mul(matrix, new float4(p.m_TransformLocalPosition, 1.0f)).xyz;
                    float3 d = restPos - p.m_Position;
                    p.m_Position += d * (p.m_Elasticity * timeVar);

                    if (stiffness > 0)
                    {
                        d = restPos - p.m_Position;
                        float len = math.length(d);
                        float maxLen = restLen * (1 - stiffness) * 2;
                        if (len > maxLen)
                        {
                            p.m_Position += d * ((len - maxLen) / len);
                        }
                    }
                }

                float3 dd = p0.m_Position - p.m_Position;
                float leng = math.length(dd);
                if (leng > 0)
                {
                    p.m_Position += dd * ((leng - restLen) / leng);
                }
            }
        }

        private void SkipUpdateParticle()
        {
            foreach (var pt in m_ParticleTrees)
            {
                SkipUpdateParticle(pt);
            }
        }

        private void SkipUpdateParticle(ParticleTree pt)
        {
            
        }

        private void ApplyParticles()
        {
            foreach (var pt in m_ParticleTrees)
            {
                ApplyParticles(pt);
            }
        }

        private void ApplyParticles(ParticleTree pt)
        {
            for(int i = 1; i < pt.m_Particles.Count; ++i)
            {
                var p = pt.m_Particles[i];
                var p0 = pt.m_Particles[p.m_ParentIndex];

                if (p0.m_ChildCount <= 1)
                {
                    float3 localPos = p.m_Transform.localPosition;
                    float3 v0 = p0.m_Transform.TransformDirection(localPos);
                    float3 v1 = p.m_Position - p0.m_Position;
                    quaternion rot = Quaternion.FromToRotation(v0, v1);
                    p0.m_Transform.rotation = rot * p0.m_Transform.rotation;
                }

                p.m_Transform.position = p.m_Position;
            }
        }

        #endregion
    }
}