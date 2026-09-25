using System;
using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>Sends the first queued cars to free pumps and lines up the rest.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(CarSpawnSystem))]
    public partial struct QueueSystem : ISystem
    {
        private struct QueuedCar : IComparable<QueuedCar>
        {
            public Entity Entity;
            public uint Order;

            public int CompareTo(QueuedCar other) => Order.CompareTo(other.Order);
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<StationUpgrades>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var queue = new NativeList<QueuedCar>(Allocator.Temp);
            foreach (var (car, entity) in SystemAPI.Query<RefRO<Car>>().WithEntityAccess())
            {
                if (car.ValueRO.State == CarState.Queued)
                    queue.Add(new QueuedCar { Entity = entity, Order = car.ValueRO.ArrivalOrder });
            }

            if (queue.Length == 0)
            {
                queue.Dispose();
                return;
            }

            queue.Sort();
            int head = 0;
            int openedPumps = SystemAPI.GetSingleton<StationUpgrades>().ExtraPump;

            foreach (var (pump, pumpEntity) in SystemAPI.Query<RefRW<Pump>>().WithEntityAccess())
            {
                if (head >= queue.Length)
                    break;

                if (pump.ValueRO.RequiredUpgradeLevel > openedPumps)
                    continue;

                var occupant = pump.ValueRO.Occupant;
                if (occupant != Entity.Null && SystemAPI.Exists(occupant))
                    continue;

                var carEntity = queue[head++].Entity;
                pump.ValueRW.Occupant = carEntity;

                var car = SystemAPI.GetComponentRW<Car>(carEntity);
                car.ValueRW.State = CarState.DrivingToPump;
                car.ValueRW.Pump = pumpEntity;

                var path = SystemAPI.GetBuffer<PathPoint>(carEntity);
                path.Clear();
                path.Add(new PathPoint { Position = pump.ValueRO.StopPosition });
            }

            var spawner = SystemAPI.GetSingleton<CarSpawner>();
            for (int i = head; i < queue.Length; i++)
            {
                var entity = queue[i].Entity;
                float3 slot = StationMath.QueueSlot(spawner.QueueHead, spawner.QueueDirection, spawner.QueueSpacing, i - head);
                var path = SystemAPI.GetBuffer<PathPoint>(entity);

                bool alreadyThere = path.Length == 0
                                    && math.distancesq(SystemAPI.GetComponent<LocalTransform>(entity).Position.xz, slot.xz) < 0.25f;
                bool alreadyHeading = path.Length == 1 && math.distancesq(path[0].Position, slot) < 0.01f;
                if (alreadyThere || alreadyHeading)
                    continue;

                path.Clear();
                path.Add(new PathPoint { Position = slot });
            }

            queue.Dispose();
        }
    }
}
