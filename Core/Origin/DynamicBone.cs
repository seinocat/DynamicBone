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
        public float3 m_Gravity = float3.zero;

        [LabelText("外力")] 
        public float3 m_Force;
        [LabelText("受力曲线")]
        public AnimationCurve m_ForceCurve;
        
        [Range(0, 1)]
        [LabelText("权重")]
        public float m_BlendWeight = 1.0f;

        [LabelText("根节点")]
        public List<Transform> m_Roots;

        [LabelText("结束节点")]
        public List<Transform> m_EndRoot;
        
        [LabelText("碰撞")] 
        public List<DynamicBoneColliderBase> m_Colliders;

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
        
        private List<ParticleTree> m_ParticleTrees = new();
        private List<DynamicBoneColliderBase> m_EffectiveColliders;

        private float3 m_ObjectMove;
        private float3 m_ObjectPrevPosition;
        private float m_ObjectScale;

        private float m_Time;
        private float m_Weight = 1.0f;
        private int m_PreUpdateCount;
        private float m_DeltaTime;
        
        private static int s_UpdateCount;
        private static int s_PrepareFrame;
        
        public class ParticleTree
        {
            public Transform m_Root;
            public float3 m_LocalGravity;
            public float3 m_RestGravity;
            public float4x4 m_RootWorldToLocalMatrix;
            public float m_BoneTotalLength;
            public List<Particle> m_Particles = new();
        }
        
        public class Particle
        {
            public Transform m_Transform;
            public int m_ParentIndex; //父质点索引
            public int m_ChildCount; //子质点数量
            public float m_Damping; //阻尼系数
            public float m_Elasticity; //弹性系数
            public float m_Stiffness; //刚性系数
            public float m_Inert; //惯性系数
            public float m_Friction; //摩擦力
            public float m_Radius; //质点半径
            public float m_BoneLength; //骨骼长度
            public float m_Force;
            public bool m_IsCollide;

            public float3 m_Position; //实际坐标
            public float3 m_PrevPosition; //前一帧坐标
            public float3 m_InitLocalPosition; //初始本地坐标
            public quaternion m_InitLocalRotation; //初始本地旋转

            public float3 m_TransformPosition; //理想世界坐标
            public float3 m_TransformLocalPosition; //理想本地坐标
            public float4x4 m_TransformLocalToWorldMatrix;
        }
        
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

            if (b != null && !m_EndRoot.Contains(b))
            {
                for (int i = 0; i < b.childCount; i++)
                {
                    var child = b.GetChild(i);
                    AppendParticles(pt, child, index, boneLength);
                }
            }

            UpdateParameters();
        }

        public void UpdateParameters()
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
                p.m_Damping = Mathf.Clamp01(this.m_Damping);
                p.m_Elasticity =  Mathf.Clamp01(this.m_Elasticity);
                p.m_Stiffness =  Mathf.Clamp01(this.m_Stiffness);
                p.m_Inert =  Mathf.Clamp01(this.m_Inert);
                p.m_Friction = Mathf.Clamp01(this.m_Friction);
                p.m_Radius = Mathf.Abs(this.m_Radius);
                p.m_Force = 1;
                
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
                    if (m_FrictionCurve != null && m_FrictionCurve.keys.Length > 0)
                        p.m_Friction *= m_FrictionCurve.Evaluate(samplePos);
                    if (m_RadiusCurve != null && m_RadiusCurve.keys.Length > 0)
                        p.m_Radius *= m_RadiusCurve.Evaluate(samplePos);
                    if (m_ForceCurve != null && m_ForceCurve.keys.Length > 0)
                        p.m_Force *= m_ForceCurve.Evaluate(samplePos);
                }
                
                p.m_Damping = Mathf.Clamp01(p.m_Damping);
                p.m_Elasticity =  Mathf.Clamp01(p.m_Elasticity);
                p.m_Stiffness =  Mathf.Clamp01(p.m_Stiffness);
                p.m_Inert =  Mathf.Clamp01(p.m_Inert);
                p.m_Friction = Mathf.Clamp01(p.m_Friction);
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
                p.m_IsCollide = false;
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

            m_EffectiveColliders?.Clear();
            if (m_Colliders is {Count: > 0})
            {
                foreach (var collider in m_Colliders)
                {
                    if (collider != null && collider.enabled)
                    {
                        m_EffectiveColliders ??= new List<DynamicBoneColliderBase>();
                        m_EffectiveColliders.Add(collider);
                    }

                    if (collider.PrepareFrame != s_PrepareFrame)
                    {
                        collider.Prepare();
                        collider.PrepareFrame = s_PrepareFrame;
                    }
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
            float3 fdir = math.normalizesafe(m_Gravity);
            float3 pf = fdir * math.max(math.dot(pt.m_RestGravity, fdir), 0);
            force -= pf;
            float time = m_ObjectScale * timeVar;

            float3 objectMove = loopIndex == 0 ? m_ObjectMove : float3.zero;

            for (int i = 0; i <  pt.m_Particles.Count; i++)
            {
                var p = pt.m_Particles[i];
                float3 pforce = (force + p.m_Force * m_Force) * time;
                if (p.m_ParentIndex >= 0)
                {
                    float3 v = p.m_Position - p.m_PrevPosition;
                    float3 rmove = objectMove * p.m_Inert;
                    p.m_PrevPosition = p.m_Position + rmove;
                    float damping = p.m_Damping;
                    if (p.m_IsCollide)
                    {
                        damping = Mathf.Clamp01(damping + p.m_Friction);
                        p.m_IsCollide = false;
                    }
                    // 韦尔莱积分应用
                    // 公式：x(t + △t) = x(t) + x(t) - x(x - △t) + a(t) * △t * △t / 2
                    // f = ma,设 m=1 则 a=f，即不考虑物体质量对运动的影响
                    // 余项忽略，即x(t + △t) = x(t) + v + f
                    // v即当前帧与上一帧的位置差值，f为总合力（重力+外力）
                    // 根据v,阻尼,合力和惯性(rmove)计算实际位置
                    p.m_Position += v * (1 - damping) + pforce + rmove;
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
            for (var i = 1; i < pt.m_Particles.Count ; i++)
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

                if (m_EffectiveColliders?.Count > 0)
                {
                    float radius = p.m_Radius * m_ObjectScale;
                    for (int j = 0; j < m_EffectiveColliders.Count; j++)
                    {
                        p.m_IsCollide |= m_EffectiveColliders[j].Collide(ref p.m_Position, radius);
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
            for(int i = 1; i < pt.m_Particles.Count; i++)
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying && transform.hasChanged)
            {
                InitTransforms();
                SetupParticles();
                UpdateParameters();
            }
            
            Gizmos.color = Color.cyan;
            foreach (var pt in m_ParticleTrees)
            {
                foreach (var p in pt.m_Particles)
                {
                    //质点半径
                    Gizmos.DrawWireSphere(p.m_Position, p.m_Radius);
                    if (p.m_ParentIndex >= 0)
                    {
                        var p0 = pt.m_Particles[p.m_ParentIndex];
                        Gizmos.DrawLine(p0.m_Position, p.m_Position);
                    }
                }
            }
        }
#endif
    }
}