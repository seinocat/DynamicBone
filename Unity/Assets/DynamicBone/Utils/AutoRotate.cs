using System;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class AutoRotate : MonoBehaviour
    {
        public Vector3 rotationAxis = Vector3.up; // 设置旋转轴
        public float rotationSpeed = 30f;
        
        private void Update()
        {
            transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
        }
    }
}