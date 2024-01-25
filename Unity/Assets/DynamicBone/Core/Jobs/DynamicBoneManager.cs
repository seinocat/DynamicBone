using System;
using System.Collections.Generic;
using Seino.Utils.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Seino.DynamicBone
{
    public class DynamicBoneManager : MonoSingleton<DynamicBoneManager>
    {
        public const int MAX_PARTICLE_COUNT = 20;

        private float m_UpdateRate = 60f;

        private List<DynamicBoneJob> m_JobBones;
        private NativeList<HeadInfo> m_HeadInfos;
        private NativeList<ParticleInfo> m_ParticleInfos;
        private TransformAccessArray m_HeadTransArray;
        private TransformAccessArray m_ParticleTransArray;

        private void Awake()
        {
            m_JobBones = new List<DynamicBoneJob>();
            m_HeadInfos = new NativeList<HeadInfo>(200, Allocator.Persistent);
            m_HeadTransArray = new TransformAccessArray(200, 64);
            m_ParticleInfos = new NativeList<ParticleInfo>(Allocator.Persistent);
            m_ParticleTransArray = new TransformAccessArray(200 * MAX_PARTICLE_COUNT, 64);
        }

        private void LateUpdate()
        {
            ExecuteJobs();
        }

        private void ExecuteJobs()
        {
            int jobBoneCount = m_HeadInfos.Length;
            int particleMaxCount = jobBoneCount * MAX_PARTICLE_COUNT;

            JobHandle BoneSetupJob = new BoneSetupJob
            {
                HeadArray = m_HeadInfos
            }.Schedule(m_HeadTransArray);

            JobHandle dependency = new PrepareParticleJob
            {
                HeadArray = m_HeadInfos,
                ParticleInfos = m_ParticleInfos
            }.Schedule(jobBoneCount, MAX_PARTICLE_COUNT, BoneSetupJob);

            dependency = new UpdateParticle1Job
            {
                HeadArray = m_HeadInfos,
                ParticleInfos = m_ParticleInfos
            }.Schedule(particleMaxCount, MAX_PARTICLE_COUNT, dependency);
            
            dependency = new UpdateParticle2Job
            {
                HeadArray = m_HeadInfos,
                ParticleInfos = m_ParticleInfos
            }.Schedule(particleMaxCount, MAX_PARTICLE_COUNT, dependency);
            
            dependency = new ApplyTransformJob
            {
                ParticleInfos = m_ParticleInfos
            }.Schedule(m_ParticleTransArray, dependency);
            
            dependency.Complete();
        }

        public void AddBone(DynamicBoneJob bone)
        {
            if (m_JobBones.Contains(bone))
                return;
            m_JobBones.Add(bone);
            bone.HeadInfo.m_Offset = m_ParticleInfos.Length;
            bone.HeadInfo.m_Index = m_HeadInfos.Length;
            
            m_HeadInfos.Add(bone.HeadInfo);
            m_ParticleInfos.AddRange(bone.ParticleInfos);
            m_HeadTransArray.Add(bone.transform);
            for (int i = 0; i < MAX_PARTICLE_COUNT; i++)
            {
                m_ParticleTransArray.Add(bone.ParticleTransforms[i]);
            }
        }

        #region Job

        [BurstCompile]
        private struct BoneSetupJob : IJobParallelForTransform
        {
            public NativeArray<HeadInfo> HeadArray;

            public void Execute(int index, TransformAccess transform)
            {
                HeadInfo info = HeadArray[index];
                info.m_ObjectPosition = transform.position;
                info.m_ObjectRotation = transform.rotation;
                HeadArray[index] = info;
            }
        }

        [BurstCompile]
        private struct PrepareParticleJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<HeadInfo> HeadArray;
            public NativeArray<ParticleInfo> ParticleInfos;

            public void Execute(int index)
            {
                HeadInfo info = HeadArray[index];
                info.m_ObjectMove = info.m_ObjectPosition - info.m_ObjectPrevPosition;
                info.m_ObjectPrevPosition = info.m_ObjectPosition;

                float3 objectPosition = info.m_ObjectPosition;
                quaternion objectRotation = info.m_ObjectRotation;

                for (int j = 0; j < info.m_ParticleCount; j++)
                {
                    int pIdx = info.m_Offset + j;
                    var p = ParticleInfos[pIdx];
                    float3 localPosition = p.m_LocalPosition * p.m_ParentScale;
                    quaternion localRotation = p.m_LocalRotation;
                    float3 worldPosition = objectPosition + math.mul(objectRotation, localPosition);
                    quaternion worldRotation = math.mul(objectRotation, localRotation);
                        
                    objectPosition = p.m_Position = worldPosition;
                    objectRotation = p.m_Rotation = worldRotation;

                    ParticleInfos[pIdx] = p;
                }
                    
                float3 force = info.m_Gravity;
                float3 fdir = math.normalizesafe(force);
                float3 pf = fdir * math.max(math.dot(force, fdir), 0);
                force -= pf;
                force = (force + info.m_Force) * info.m_ObjectScale;
                info.m_FinalForce = force;
                    
                HeadArray[index] = info;
            }
        }
        
        [BurstCompile]
        private struct UpdateParticle1Job : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<HeadInfo> HeadArray;
            public NativeArray<ParticleInfo> ParticleInfos;
            
            public void Execute(int index)
            {
                int startIndex = index / MAX_PARTICLE_COUNT;
                HeadInfo info = HeadArray[startIndex];
                
                int offset = index % MAX_PARTICLE_COUNT;
                if (offset >= info.m_ParticleCount) return;
                int pIdx = info.m_Offset + offset;
                var p = ParticleInfos[pIdx];

                if (p.m_ParentIndex >= 0)
                {
                    float3 v = p.m_WorldPostion - p.m_PrevPosition;
                    float3 rmove = info.m_ObjectMove * p.m_Inert;
                    p.m_PrevPosition = p.m_WorldPostion + rmove;
                    float damping = p.m_Damping;
                    if (p.m_IsCollide)
                    {
                        damping = Mathf.Clamp01(damping + p.m_Friction);
                        p.m_IsCollide = false;
                    }
                    
                    p.m_Position += v * (1 - damping) + info.m_FinalForce + rmove;
                    
                }else
                {
                    p.m_PrevPosition = p.m_WorldPostion;
                    p.m_WorldPostion = p.m_Position;
                }

                ParticleInfos[pIdx] = p;
            }
        }
        
        [BurstCompile]
        private struct UpdateParticle2Job : IJobParallelFor
        {
            [ReadOnly] 
            public NativeArray<HeadInfo> HeadArray;
            public NativeArray<ParticleInfo> ParticleInfos;
            
            public void Execute(int index)
            {
                if (index % MAX_PARTICLE_COUNT == 0) return;

                int headIndex = index / MAX_PARTICLE_COUNT;
                var info = HeadArray[headIndex];

                int offset = index % MAX_PARTICLE_COUNT;
                if (offset > info.m_ParticleCount) return;

                int pIdx = info.m_Offset + offset;
                var p = ParticleInfos[pIdx];
                var p0 = ParticleInfos[info.m_Offset + p.m_ParentIndex];

                float3 pos = p.m_Position;
                float3 parentPos = p0.m_Position;

                float restLen = math.distance(pos, parentPos);
                float stiffness = math.lerp(1.0f, p.m_Stiffness, info.m_Weight);
                if (stiffness > 0 || p.m_Elasticity > 0)
                {
                    var matrix = float4x4.TRS(p0.m_WorldPostion, p0.m_Rotation, p.m_ParentScale);
                    float3 restPos = math.mul(matrix, new float4(p.m_LocalPosition, 1.0f)).xyz;
                    float3 d = restPos - p.m_WorldPostion;
                    p.m_WorldPostion += d * p.m_Elasticity;

                    if (stiffness > 0)
                    {
                        d = restPos - p.m_WorldPostion;
                        float len = math.length(d);
                        float maxLen = restLen * (1 - stiffness) * 2;
                        if (len > maxLen)
                        {
                            p.m_WorldPostion += d * ((len - maxLen) / len);
                        }
                    }
                }
                
                float3 dd = p0.m_WorldPostion - p.m_WorldPostion;
                float leng = math.length(dd);
                if (leng > 0)
                {
                    p.m_WorldPostion += dd * ((leng - restLen) / leng);
                }

                ParticleInfos[pIdx] = p;
            }
        }
        
        [BurstCompile]
        private struct ApplyTransformJob : IJobParallelForTransform
        {
            public NativeArray<ParticleInfo> ParticleInfos;
            
            public void Execute(int index, TransformAccess transform)
            {
                var p = ParticleInfos[index];

                p.m_Position = p.m_WorldPostion;
                transform.position = p.m_Position;
                transform.rotation = p.m_Rotation;
                ParticleInfos[index] = p;
            }
        }

        #endregion
        
        
    }
}