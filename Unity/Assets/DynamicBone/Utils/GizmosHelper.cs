using UnityEngine;

namespace Seino.DynamicBone
{
    public static class GizmosHelper
    {
        public static void DrawWireCylinder(Vector3 p0, Vector3 p1, float radius)
        {
            Vector3 up = (p1 - p0).normalized;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up);

            float height = Vector3.Distance(p0, p1);
            Gizmos.matrix = Matrix4x4.TRS(p0, rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.up * (height * 0.5f), new Vector3(radius * 2, height, radius * 2));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}