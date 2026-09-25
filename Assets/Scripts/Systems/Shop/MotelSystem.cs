using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// In the evening some travellers take a clean motel room, sleep until morning and pay on checkout,
    /// leaving the room dirty. The player cleans rooms with interact at the reception; janitors clean them too.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(ParkingSystem))]
    [UpdateBefore(typeof(TireServiceSystem))]
    public partial struct MotelSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Motel>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<StationSettings>();
            state.RequireForUpdate<CarSpawner>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float hour = SystemAPI.GetSingleton<GameTime>().Hour;
            int level = SystemAPI.GetSingleton<StationUpgrades>().Motel;
            var motelEntity = SystemAPI.GetSingletonEntity<Motel>();
            ref var motel = ref SystemAPI.GetComponentRW<Motel>(motelEntity).ValueRW;
            var rooms = SystemAPI.GetBuffer<MotelRoom>(motelEntity);
            int openRooms = FacilityMath.OpenRooms(level, rooms.Length);
            var exitRoute = SystemAPI.GetBuffer<ExitRoutePoint>(SystemAPI.GetSingletonEntity<CarSpawner>());
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            ref var economy = ref SystemAPI.GetSingletonRW<Economy>().ValueRW;

            CleanRooms(ref state, ref motel, rooms, events);

            foreach (var (car, path, transform, entity) in SystemAPI
                         .Query<RefRW<Car>, DynamicBuffer<PathPoint>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                switch (car.ValueRO.State)
                {
                    case CarState.ReadyToLeave:
                    {
                        if (level == 0 || !FacilityMath.WantsRoom(hour))
                            break;
                        if (motel.Random.NextFloat() >= CustomerProfiles.Get(car.ValueRO.Customer).MotelChance)
                        {
                            // Decided once: do not roll again next frame.
                            break;
                        }

                        int room = FindFreeRoom(ref state, rooms, openRooms);
                        if (room < 0)
                            break;

                        var pumpEntity = car.ValueRO.Pump;
                        if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                        {
                            var pump = SystemAPI.GetComponentRW<Pump>(pumpEntity);
                            if (pump.ValueRO.Occupant == entity)
                                pump.ValueRW.Occupant = Entity.Null;
                        }

                        var reserved = rooms[room];
                        reserved.Occupant = entity;
                        rooms[room] = reserved;

                        car.ValueRW.State = CarState.DrivingToMotel;
                        car.ValueRW.Pump = Entity.Null;
                        car.ValueRW.ParkingSpot = room;
                        car.ValueRW.ParkUntilHour = motel.Random.NextFloat(6f, 9f);
                        path.Clear();
                        path.Add(new PathPoint { Position = motel.Entry });
                        path.Add(new PathPoint { Position = reserved.Position });
                        StationEvent.Push(events, StationEventType.MotelCheckIn);
                        break;
                    }

                    case CarState.DrivingToMotel:
                        if (!path.IsEmpty)
                            break;

                        car.ValueRW.State = CarState.InMotel;
                        if (car.ValueRO.ParkingSpot < rooms.Length)
                            transform.ValueRW.Rotation = rooms[car.ValueRO.ParkingSpot].Rotation;
                        break;

                    case CarState.InMotel:
                    {
                        if (!FacilityMath.ShouldLeaveParking(hour, car.ValueRO.ParkUntilHour))
                            break;

                        float price = FacilityMath.RoomPrice(motel.RoomPrice, level);
                        economy.Money += price;
                        economy.DayIncome += price;
                        economy.Reputation = StationMath.ClampReputation(economy.Reputation + FacilityMath.MotelReputationBonus);
                        StationEvent.Push(events, StationEventType.MotelPaid, default, price);

                        int room = car.ValueRO.ParkingSpot;
                        if (room >= 0 && room < rooms.Length && rooms[room].Occupant == entity)
                        {
                            var freed = rooms[room];
                            freed.Occupant = Entity.Null;
                            freed.Dirty = true;
                            rooms[room] = freed;
                        }

                        CarRoutes.SendToExit(ref car.ValueRW, path, exitRoute);
                        break;
                    }
                }
            }
        }

        private void CleanRooms(ref SystemState state, ref Motel motel, DynamicBuffer<MotelRoom> rooms, DynamicBuffer<StationEvent> events)
        {
            int dirty = FirstDirtyRoom(rooms);
            if (dirty < 0)
            {
                motel.CleanTimer = 0f;
                return;
            }

            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var (interaction, transform) in SystemAPI
                         .Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                if (!interaction.ValueRO.InteractPressed ||
                    math.distancesq(transform.ValueRO.Position.xz, motel.Door.xz) > radius * radius)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                MarkClean(rooms, dirty, events);
                return;
            }

            float janitors = SystemAPI.HasSingleton<StaffPower>() ? SystemAPI.GetSingleton<StaffPower>().Janitor : 0f;
            if (janitors <= 0f)
                return;

            motel.CleanTimer += SystemAPI.Time.DeltaTime;
            if (motel.CleanTimer < FacilityMath.JanitorRoomInterval(janitors))
                return;

            motel.CleanTimer = 0f;
            MarkClean(rooms, dirty, events);
        }

        private static void MarkClean(DynamicBuffer<MotelRoom> rooms, int index, DynamicBuffer<StationEvent> events)
        {
            var room = rooms[index];
            room.Dirty = false;
            rooms[index] = room;
            StationEvent.Push(events, StationEventType.MotelRoomCleaned);
        }

        private static int FirstDirtyRoom(DynamicBuffer<MotelRoom> rooms)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i].Dirty)
                    return i;
            }

            return -1;
        }

        private int FindFreeRoom(ref SystemState state, DynamicBuffer<MotelRoom> rooms, int openRooms)
        {
            for (int i = 0; i < openRooms; i++)
            {
                var occupant = rooms[i].Occupant;
                bool free = occupant == Entity.Null || !SystemAPI.Exists(occupant);
                if (free && !rooms[i].Dirty)
                    return i;
            }

            return -1;
        }
    }
}
