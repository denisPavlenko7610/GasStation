using GasStation.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    public static class TrashSpawning
    {
        public static void Spawn(EntityCommandBuffer ecb, Entity prefab, float scale, float3 position, float yaw)
        {
            var trash = ecb.Instantiate(prefab);
            ecb.AddComponent(trash, LocalTransform.FromPositionRotationScale(position, quaternion.RotateY(yaw), scale));
            ecb.AddComponent(trash, new Trash());
        }
    }
}
