using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Seino.DynamicBone
{
    public class Wind : MonoBehaviour
    {
        public Vector3 dir;
        public float force = 0.01f;
        public DynamicBone DynamicBone;
        public float min = 0f;
        public float max = 0.003f;

        private void Update()
        {
            var dirNormal = dir.normalized;
            var randomForce = force + Random.Range(min, max);
            DynamicBone.m_Force = dirNormal * randomForce;
        }
    }
}