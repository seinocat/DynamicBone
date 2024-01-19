using Unity.Mathematics;

namespace Seino.DynamicBone
{
    public static class Utils
    {
        public static float3 LocalToWorldPosition(float3 parentPosition,quaternion  parentRotation, float3 targetLocalPosition)
        {
            return parentPosition + math.mul(parentRotation, targetLocalPosition);
        }

        public static quaternion LocalToWorldRotation(quaternion  parentRotation, quaternion targetLocalRotation)
        {
            return math.mul(parentRotation, targetLocalRotation);
        }
    }
}