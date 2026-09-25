using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Components
{
    public struct CarSpawner : IComponentData
    {
        public float3 SpawnPosition;
        public quaternion SpawnRotation;

        public float3 QueueHead;
        /// <summary>Direction in which the queue grows, from its head to its tail.</summary>
        public float3 QueueDirection;
        public float QueueSpacing;

        public float BaseInterval;
        public float Timer;
        public int MaxCars;

        public float2 SpeedRange;
        public float2 PatienceRange;
        public float2 LitersRange;

        public uint NextArrivalOrder;
        public Random Random;
    }

    public struct CarPrefabElement : IBufferElementData
    {
        public Entity Prefab;
    }

    public struct EntryRoutePoint : IBufferElementData
    {
        public float3 Position;
    }

    public struct ExitRoutePoint : IBufferElementData
    {
        public float3 Position;
    }
}
