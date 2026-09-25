using GasStation.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// Sends a truck for every fuel and goods delivery that is about to arrive. The truck drives in, unloads
    /// (then the delivery completes) and drives away.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(StationCommandSystem))]
    [UpdateBefore(typeof(FuelDeliverySystem))]
    public partial struct DeliveryTruckSystem : ISystem
    {
        private struct PendingDelivery
        {
            public Entity Delivery;
            public bool Fuel;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeliveryTrucks>();
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<DeliveryTrucks>();
            var spawnerEntity = SystemAPI.GetSingletonEntity<CarSpawner>();

            SendTrucks(ref state, settings, spawnerEntity);
            DriveTrucks(ref state, settings, spawnerEntity);
        }

        private void SendTrucks(ref SystemState state, DeliveryTrucks settings, Entity spawnerEntity)
        {
            var pending = new NativeList<PendingDelivery>(Allocator.Temp);
            if (settings.FuelTruckPrefab != Entity.Null)
            {
                foreach (var (delivery, entity) in SystemAPI.Query<RefRO<FuelDelivery>>().WithEntityAccess())
                {
                    if (NeedsTruck(delivery.ValueRO.Truck, delivery.ValueRO.TimeLeft, settings.LeadTime, ref state))
                        pending.Add(new PendingDelivery { Delivery = entity, Fuel = true });
                }
            }

            if (settings.CargoTruckPrefab != Entity.Null)
            {
                foreach (var (delivery, entity) in SystemAPI.Query<RefRO<ProductDelivery>>().WithEntityAccess())
                {
                    if (NeedsTruck(delivery.ValueRO.Truck, delivery.ValueRO.TimeLeft, settings.LeadTime, ref state))
                        pending.Add(new PendingDelivery { Delivery = entity, Fuel = false });
                }
            }

            if (pending.Length > 0)
            {
                var spawner = SystemAPI.GetComponent<CarSpawner>(spawnerEntity);
                var entry = SystemAPI.GetBuffer<EntryRoutePoint>(spawnerEntity).ToNativeArray(Allocator.Temp);
                var entityManager = state.EntityManager;

                foreach (var item in pending)
                {
                    var prefab = item.Fuel ? settings.FuelTruckPrefab : settings.CargoTruckPrefab;
                    float scale = entityManager.HasComponent<LocalTransform>(prefab)
                        ? entityManager.GetComponentData<LocalTransform>(prefab).Scale
                        : 1f;

                    var truck = entityManager.Instantiate(prefab);
                    entityManager.AddComponentData(truck, LocalTransform.FromPositionRotationScale(spawner.SpawnPosition, spawner.SpawnRotation, scale));
                    entityManager.AddComponentData(truck, new DeliveryTruck { Delivery = item.Delivery, Fuel = item.Fuel, State = TruckState.Arriving });
                    entityManager.AddComponentData(truck, new CarMovement { Speed = settings.Speed, TurnSpeed = 3f });

                    var path = entityManager.AddBuffer<PathPoint>(truck);
                    for (int i = 0; i < entry.Length; i++)
                        path.Add(new PathPoint { Position = entry[i].Position });
                    path.Add(new PathPoint { Position = item.Fuel ? settings.FuelUnload : settings.CargoUnload });

                    if (item.Fuel)
                    {
                        var delivery = entityManager.GetComponentData<FuelDelivery>(item.Delivery);
                        delivery.Truck = truck;
                        entityManager.SetComponentData(item.Delivery, delivery);
                    }
                    else
                    {
                        var delivery = entityManager.GetComponentData<ProductDelivery>(item.Delivery);
                        delivery.Truck = truck;
                        entityManager.SetComponentData(item.Delivery, delivery);
                    }
                }

                entry.Dispose();
            }

            pending.Dispose();
        }

        private void DriveTrucks(ref SystemState state, DeliveryTrucks settings, Entity spawnerEntity)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(spawnerEntity);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (truck, path, transform, entity) in SystemAPI
                         .Query<RefRW<DeliveryTruck>, DynamicBuffer<PathPoint>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                switch (truck.ValueRO.State)
                {
                    case TruckState.Arriving:
                        if (!path.IsEmpty)
                            break;

                        truck.ValueRW.State = TruckState.Unloading;
                        truck.ValueRW.Timer = settings.UnloadTime;
                        transform.ValueRW.Rotation = truck.ValueRO.Fuel ? settings.FuelUnloadRotation : settings.CargoUnloadRotation;
                        StationEvent.Push(events, StationEventType.TruckArrived, default, truck.ValueRO.Fuel ? 1f : 0f);
                        break;

                    case TruckState.Unloading:
                        truck.ValueRW.Timer -= deltaTime;
                        if (truck.ValueRO.Timer > 0f)
                            break;

                        CompleteDelivery(ref state, truck.ValueRO);
                        truck.ValueRW.State = TruckState.Leaving;
                        path.Clear();
                        for (int i = 0; i < exitRoute.Length; i++)
                            path.Add(new PathPoint { Position = exitRoute[i].Position });
                        break;

                    case TruckState.Leaving:
                        if (path.IsEmpty)
                            ecb.DestroyEntity(entity);
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>Lets the delivery finish on the next update of its delivery system.</summary>
        private void CompleteDelivery(ref SystemState state, DeliveryTruck truck)
        {
            var delivery = truck.Delivery;
            if (delivery == Entity.Null || !SystemAPI.Exists(delivery))
                return;

            if (truck.Fuel && SystemAPI.HasComponent<FuelDelivery>(delivery))
            {
                var fuel = SystemAPI.GetComponentRW<FuelDelivery>(delivery);
                fuel.ValueRW.Truck = Entity.Null;
                fuel.ValueRW.TimeLeft = 0f;
            }
            else if (!truck.Fuel && SystemAPI.HasComponent<ProductDelivery>(delivery))
            {
                var goods = SystemAPI.GetComponentRW<ProductDelivery>(delivery);
                goods.ValueRW.Truck = Entity.Null;
                goods.ValueRW.TimeLeft = 0f;
            }
        }

        private bool NeedsTruck(Entity truck, float timeLeft, float leadTime, ref SystemState state) =>
            timeLeft > 0f && timeLeft <= leadTime && (truck == Entity.Null || !SystemAPI.Exists(truck));
    }
}
