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
        private static float m_DeltaTime;
        private JobHandle dependency;
        private List<DynamicBoneJob> m_RemoveBones;

        private void Awake()
        {
            m_JobBones = new List<DynamicBoneJob>();
            m_RemoveBones = new List<DynamicBoneJob>();
            m_HeadInfos = new NativeList<HeadInfo>(200, Allocator.Persistent);
            m_HeadTransArray = new TransformAccessArray(200, 64);
            m_ParticleInfos = new NativeList<ParticleInfo>(Allocator.Persistent);
            m_ParticleTransArray = new TransformAccessArray(200 * MAX_PARTICLE_COUNT, 64);
        }

        private void OnDestroy()
        {
            dependency.Complete();
            if (m_HeadInfos.IsCreated) m_HeadInfos.Dispose();
            if (m_HeadTransArray.isCreated) m_HeadTransArray.Dispose();
            if (m_ParticleInfos.IsCreated) m_ParticleInfos.Dispose();
            if (m_ParticleTransArray.isCreated) m_ParticleTransArray.Dispose();
        }

        private void LateUpdate()
        {
            dependency.Complete();
            RemoveJobs();
            ExecuteJobs();
        }

        private void ExecuteJobs()
        {
            int jobBoneCount = m_HeadInfos.Length;
            int particleMaxCount = jobBoneCount * MAX_PARTICLE_COUNT;

            var setupJob = new BoneSetupJob
            {
                HeadArray = m_HeadInfos
            }.Schedule(m_HeadTransArray);
            
            var headJob = new PrepareHeadJob()
            {
                HeadArray = m_HeadInfos,
                DeltaTime = m_DeltaTime
            }.Schedule(jobBoneCount, MAX_PARTICLE_COUNT, setupJob);

            var particleJob = new PrepareParticleJob
            {
                HeadArray = m_HeadInfos,
                ParticleInfos = m_ParticleInfos,
            }.Schedule(particleMaxCount, MAX_PARTICLE_COUNT, headJob);
            
            dependency = new UpdateParticle1Job
            {
                HeadArray = m_HeadInfos,
                ParticleInfos = m_ParticleInfos
            }.Schedule(particleMaxCount, MAX_PARTICLE_COUNT, particleJob);
            
            dependency = new UpdateParticle2Job
            {
                HeadArray = m_HeadInfos,
                ParticleInfos = m_ParticleInfos
            }.Schedule(particleMaxCount, MAX_PARTICLE_COUNT, dependency);
            
            dependency = new ApplyTransformJob
            {
                ParticleInfos = m_ParticleInfos
            }.Schedule(m_ParticleTransArray, dependency);
        }

        public void AddBone(DynamicBoneJob bone)
        {
            if (m_JobBones.Contains(bone))
                return;
            m_JobBones.Add(bone);

            for (int i = 0; i < bone.HeadInfos.Count; i++)
            {
                var headInfo = bone.HeadInfos[i];
                headInfo.m_ParticleOffset = m_ParticleInfos.Length;
                headInfo.m_Index = m_HeadInfos.Length;
                bone.HeadInfos[i] = headInfo;
            
                m_HeadInfos.Add(headInfo);
                m_ParticleInfos.AddRange(bone.ParticleInfos[i]);
                m_HeadTransArray.Add(bone.transform);
                for (int j = 0; j < MAX_PARTICLE_COUNT; j++)
                {
                    m_ParticleTransArray.Add(bone.ParticleTransforms[i][j]);
                }
            }
        }

        public void RemoveBone(DynamicBoneJob bone)
        {
            if (!m_JobBones.Contains(bone))
                return;
            if (m_RemoveBones.Contains(bone))
                return;
            m_RemoveBones.Add(bone);
        }

        private void RemoveJobs()
        {
            int count = m_RemoveBones.Count;
            if (count == 0)
                return;
            
            for (int i = count - 1; i >= 0; i--)
            {
                var bone = m_RemoveBones[i];
                for (int j = 0; j < bone.HeadInfos.Count; j++)
                {
                    var headInfo = bone.HeadInfos[j];
                    int headIndex = headInfo.m_Index;
                    int particleIdx = headInfo.m_ParticleOffset;
                    
                    //移除head
                    m_HeadInfos.RemoveAtSwapBack(headIndex);
                    m_HeadTransArray.RemoveAtSwapBack(headIndex);
                    //移除particle
                    m_ParticleInfos.RemoveRangeSwapBack(particleIdx, MAX_PARTICLE_COUNT);
                    for (int k = particleIdx + MAX_PARTICLE_COUNT - 1; k >= particleIdx ; k--)
                    {
                        m_ParticleTransArray.RemoveAtSwapBack(k);
                    }
                }
                
                m_RemoveBones.RemoveAt(i);
                m_JobBones.Remove(bone);
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
        private struct PrepareHeadJob : IJobParallelFor
        {
            public NativeArray<HeadInfo> HeadArray;
            public float DeltaTime;

            public void Execute(int index)
            {
                //计算位移和合力部分
                HeadInfo info = HeadArray[index];
                info.m_ObjectMove = info.m_ObjectPosition - info.m_ObjectPrevPosition;
                info.m_ObjectPrevPosition = info.m_ObjectPosition;
                    
                float3 force = info.m_Gravity;
                float3 fdir = math.normalizesafe(force);
                float3 pf = fdir * math.max(math.dot(force, fdir), 0);
                force -= pf;
                force = (force + info.m_Force) * info.m_ObjectScale;
                info.m_FinalForce = force;
                info.m_DeltaTime = DeltaTime;
                    
                HeadArray[index] = info;
            }
        }
        
        [BurstCompile]
        private struct PrepareParticleJob : IJobParallelFor
        {
            public NativeArray<ParticleInfo> ParticleInfos;
            [ReadOnly]
            public NativeArray<HeadInfo> HeadArray;
            
            public void Execute(int index)
            {
                //计算质点的世界坐标
                int headIndex = index / MAX_PARTICLE_COUNT;
                var info = HeadArray[headIndex];
                var p = ParticleInfos[index];
                
                float3 objectPosition;
                quaternion objectRotation;

                if (index % MAX_PARTICLE_COUNT == 0)
                {
                    objectPosition = info.m_ObjectPosition;
                    objectRotation = info.m_ObjectRotation;
                }
                else
                {
                    var p0 = ParticleInfos[info.m_ParticleOffset + p.m_ParentIndex];
                    objectPosition = p0.m_Position;
                    objectRotation = p0.m_Rotation;
                }
                
                float3 localPosition = p.m_LocalPosition * p.m_ParentScale;
                quaternion localRotation = p.m_LocalRotation;
                float3 worldPosition = objectPosition + math.mul(objectRotation, localPosition);
                quaternion worldRotation = math.mul(objectRotation, localRotation);
                        
                p.m_Position = worldPosition;
                p.m_Rotation = worldRotation;
                    
                ParticleInfos[index] = p;
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
                int pIdx = info.m_ParticleOffset + offset;
                var p = ParticleInfos[pIdx];

                if (p.m_ParentIndex >= 0)
                {
                    float3 v = p.m_WorldPosition - p.m_PrevPosition;
                    float3 rmove = info.m_ObjectMove * p.m_Inert;
                    p.m_PrevPosition = p.m_WorldPosition + rmove;
                    float damping = p.m_Damping;
                    p.m_Position += v * (1 - damping) + info.m_FinalForce + rmove;
                    
                }else
                {
                    p.m_PrevPosition = p.m_WorldPosition;
                    p.m_WorldPosition = p.m_Position;
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

                int pIdx = info.m_ParticleOffset + offset;
                var p = ParticleInfos[pIdx];
                var p0 = ParticleInfos[info.m_ParticleOffset + p.m_ParentIndex];

                float3 pos = p.m_Position;
                float3 parentPos = p0.m_Position;

                float restLen = math.distance(pos, parentPos);
                float stiffness = math.lerp(1.0f, p.m_Stiffness, info.m_Weight);
                if (stiffness > 0 || p.m_Elasticity > 0)
                {
                    var matrix = float4x4.TRS(p0.m_WorldPosition, p0.m_Rotation, p.m_ParentScale);
                    float3 restPos = math.mul(matrix, new float4(p.m_LocalPosition, 1.0f)).xyz;
                    float3 d = restPos - p.m_WorldPosition;
                    p.m_WorldPosition += d * (p.m_Elasticity * 1);

                    if (stiffness > 0)
                    {
                        d = restPos - p.m_WorldPosition;
                        float len = math.length(d);
                        float maxLen = restLen * (1 - stiffness) * 2;
                        if (len > maxLen)
                        {
                            p.m_WorldPosition += d * ((len - maxLen) / len);
                        }
                    }
                }
                
                float3 dd = p0.m_WorldPosition - p.m_WorldPosition;
                float leng = math.length(dd);
                if (leng > 0)
                {
                    p.m_WorldPosition += dd * ((leng - restLen) / leng);
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

                p.m_Position = p.m_WorldPosition;
                transform.position = p.m_Position;
                transform.rotation = p.m_Rotation;
                ParticleInfos[index] = p;
            }
        }

        #endregion
        
    }
}