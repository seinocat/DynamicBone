﻿using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public struct HeadInfo
    {
        public int m_Index;
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
        public int m_Offset;
        public float4x4 m_RootWorldToLocalMatrix;
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

        public float3 m_WorldPostion;
        public float3 m_LocalPosition;
        public quaternion m_LocalRotation;
        public float3 m_ParentScale;
    }
    
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
        public Transform m_Root;

        [LabelText("结束节点")]
        public Transform m_EndRoot;

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

        private float m_BoneTotalLength;
        private float m_Weight = 1.0f;
        private int m_ParticleCount;
        private bool m_Inited;

        public NativeArray<ParticleInfo> ParticleInfos;
        public Transform[] ParticleTransforms;
        public HeadInfo HeadInfo;

        private void Awake()
        {
            if (m_Root == null)
                return;

            ParticleInfos = new NativeArray<ParticleInfo>(DynamicBoneManager.MAX_PARTICLE_COUNT, Allocator.Persistent);
            ParticleTransforms = new Transform[DynamicBoneManager.MAX_PARTICLE_COUNT];

            SetupParticles();
            DynamicBoneManager.Instance.AddBone(this);
            m_Inited = true;
        }
        
        private void OnValidate()
        {
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
            m_ParticleCount = 0;
            m_BoneTotalLength = 0;
            
            HeadInfo = new HeadInfo();
            HeadInfo.m_ObjectScale = math.abs(transform.lossyScale.x);
            HeadInfo.m_ObjectPrevPosition = transform.position;
            HeadInfo.m_ObjectMove = float3.zero;
            HeadInfo.m_Weight = m_Weight;
            HeadInfo.m_Force = m_Force;
            HeadInfo.m_ParticleCount = 0;
            HeadInfo.m_RootWorldToLocalMatrix = m_Root.worldToLocalMatrix;

            AppendParticles(m_Root, -1, 0);
            UpdateParameters();
            HeadInfo.m_ParticleCount = m_ParticleCount;
        }
        
        private void AppendParticles(Transform b, int parentIndex, float boneLength)
        {
            var p = new ParticleInfo();
            p.m_Index = m_ParticleCount;
            p.m_ParentIndex = parentIndex;

            m_ParticleCount++;
            
            if (b != null)
            {
                p.m_WorldPostion = p.m_Position = p.m_PrevPosition = b.position;
                p.m_LocalPosition = p.m_InitLocalPosition = b.localPosition;
                p.m_LocalRotation = p.m_InitLocalRotation = b.localRotation;
                p.m_Rotation = b.rotation;
                p.m_ParentScale = b.parent.lossyScale;
            }

            if (parentIndex >= 0)
            {
                boneLength += math.distance(ParticleTransforms[parentIndex].position, p.m_WorldPostion);
                p.m_BoneLength = boneLength;
                m_BoneTotalLength = math.max(m_BoneTotalLength, boneLength);
            }

            int index = p.m_Index;
            ParticleInfos[index] = p;
            ParticleTransforms[index] = b;

            if (b != null && m_EndRoot != b)
            {
                for (int i = 0; i < b.childCount; i++)
                {
                    var child = b.GetChild(i);
                    AppendParticles(child, index, boneLength);
                }
            }
        }
        
        
        private void UpdateParameters()
        {
            HeadInfo.m_LocalGravity = math.normalize(math.mul(HeadInfo.m_RootWorldToLocalMatrix, new float4(m_Gravity, 0.0f)).xyz) * math.length(m_Gravity);

            for (int i = 0; i < m_ParticleCount; i++)
            {
                var p = ParticleInfos[i];
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

                ParticleInfos[i] = p;
            }
        }
        
        private void InitTransforms()
        {
            for (int i = 0; i < ParticleInfos.Length; i++)
            {
                var p = ParticleInfos[i];
                p.m_LocalPosition = p.m_InitLocalPosition;
                p.m_LocalRotation = p.m_InitLocalRotation;
                ParticleInfos[i] = p;
            }
        }
        
    }
}