using System;
using Unity.Mathematics;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class AutoMove : MonoBehaviour
    {
        public float speed = 0.1f;
        public float dist = 1f;
        public float3 InitPos;
        private float time;

        private void Start()
        {
            InitPos = this.transform.position;
        }

        private void Update()
        {
            time += Time.deltaTime;
            this.transform.position = new float3(InitPos.xy, InitPos.z + dist * math.sin(speed * time));
        }
    }
}