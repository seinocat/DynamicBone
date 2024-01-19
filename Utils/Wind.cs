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

        private void Update()
        {
            var dirNormal = dir.normalized;
            var randomForce = force + Random.Range(-0.01f, 0.01f);
            DynamicBone.m_Force = dirNormal * randomForce;
        }
    }
}