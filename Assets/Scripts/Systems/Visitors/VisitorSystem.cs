using GasStation.Components;
using GasStation.Logic;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Once per game hour decides who comes besides ordinary traffic: regulars on their days and hours,
    /// and special guests (critic, tour bus, biker convoy, ambulance or police, tow truck). They are queued as
    /// SpawnRequests for CarSpawnSystem. Managed because RegularCatalog is managed data; it runs rarely.
    /// </summary>
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(WorldEventSystem))]
    [UpdateBefore(typeof(CarSpawnSystem))]
    public partial class VisitorSystem : SystemBase
    {
        private const int MaxPendingRequests = 12;

        protected override void OnCreate()
        {
            RequireForUpdate<Visitors>();
            RequireForUpdate<GameTime>();
            RequireForUpdate<CarSpawner>();
            RequireForUpdate<StationEvent>();
        }

        protected override void OnUpdate()
        {
            var time = SystemAPI.GetSingleton<GameTime>();
            int hour = (int)time.Hour;
            var visitors = SystemAPI.GetSingleton<Visitors>();
            if (hour == visitors.LastRolledHour)
                return;

            visitors.LastRolledHour = hour;
            int day = time.Day;
            int level = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1;
            var spawnerEntity = SystemAPI.GetSingletonEntity<CarSpawner>();
            if (!SystemAPI.HasBuffer<SpawnRequest>(spawnerEntity))
            {
                SystemAPI.SetSingleton(visitors);
                return;
            }

            var requests = SystemAPI.GetBuffer<SpawnRequest>(spawnerEntity);
            // The road is jammed (the car limit holds requests back): wait before inviting more guests.
            if (requests.Length > MaxPendingRequests)
            {
                SystemAPI.SetSingleton(visitors);
                return;
            }

            var events = SystemAPI.GetSingletonBuffer<StationEvent>();

            SendRegulars(ref visitors, requests, day, hour, level);
            SendSpecialGuests(ref visitors, requests, events, day, hour, level);

            SystemAPI.SetSingleton(visitors);
        }

        private void SendRegulars(ref Visitors visitors, DynamicBuffer<SpawnRequest> requests, int day, int hour, int level)
        {
            if (!SystemAPI.HasSingleton<RegularState>())
                return;

            var regulars = SystemAPI.GetSingletonBuffer<RegularState>();
            for (int i = 0; i < regulars.Length && i < RegularCatalog.Count; i++)
            {
                var regular = regulars[i];
                var info = RegularCatalog.Get(i);
                if (regular.Lost || regular.LastVisitDay == day || level < CustomerProfiles.Get(info.Type).MinStationLevel)
                    continue;
                if (!RegularCatalog.ComesOn(info, day) || !RegularCatalog.InWindow(info, hour))
                    continue;
                if (visitors.Random.NextFloat() >= VisitorMath.VisitChance(regular.Loyalty))
                    continue;

                regular.LastVisitDay = day;
                regulars[i] = regular;
                requests.Add(new SpawnRequest
                {
                    Customer = info.Type,
                    RegularId = (byte)(i + 1),
                    Delay = visitors.Random.NextFloat(0f, VisitorMath.RegularArrivalSpread),
                    HasHabits = true,
                    Fuel = info.Fuel,
                    LitersMultiplier = info.Liters * CustomerProfiles.Get(info.Type).LitersMultiplier,
                    WantsShop = info.Shop,
                    WantsWash = info.Wash
                });
            }
        }

        private void SendSpecialGuests(ref Visitors visitors, DynamicBuffer<SpawnRequest> requests, DynamicBuffer<StationEvent> events,
            int day, int hour, int level)
        {
            ref var random = ref visitors.Random;
            bool daytime = hour >= VisitorMath.DaytimeFirstHour && hour <= VisitorMath.DaytimeLastHour;

            // The critic is incognito: no announcement.
            if (level >= VisitorMath.CriticMinLevel && daytime && day - visitors.LastCriticDay >= VisitorMath.CriticEveryDays &&
                random.NextFloat() < VisitorMath.CriticChancePerHour)
            {
                visitors.LastCriticDay = day;
                requests.Add(new SpawnRequest { Customer = CustomerType.Critic, Delay = random.NextFloat(0f, VisitorMath.CriticArrivalSpread) });
            }

            if (level >= VisitorMath.BusMinLevel && SystemAPI.HasSingleton<Shop>() &&
                hour >= VisitorMath.BusFirstHour && hour <= VisitorMath.BusLastHour &&
                day - visitors.LastBusDay >= VisitorMath.BusEveryDays && random.NextFloat() < VisitorMath.BusChancePerHour)
            {
                visitors.LastBusDay = day;
                int passengers = random.NextInt(VisitorMath.BusMinPassengers, VisitorMath.BusMaxPassengers + 1);
                requests.Add(new SpawnRequest { Customer = CustomerType.TourBus, Passengers = (byte)passengers });
                StationEvent.Push(events, StationEventType.SpecialArrived, default, (float)CustomerType.TourBus, passengers);
                return;
            }

            if (level >= CustomerProfiles.Get(CustomerType.Biker).MinStationLevel &&
                hour >= VisitorMath.ConvoyFirstHour && hour <= VisitorMath.ConvoyLastHour &&
                random.NextFloat() < VisitorMath.ConvoyChancePerHour)
            {
                int convoy = random.NextInt(VisitorMath.ConvoyMin, VisitorMath.ConvoyMax + 1);
                for (int i = 0; i < convoy; i++)
                    requests.Add(new SpawnRequest { Customer = CustomerType.Biker, Delay = i == 0 ? 0f : VisitorMath.ConvoyInterval });
                StationEvent.Push(events, StationEventType.SpecialArrived, default, (float)CustomerType.Biker, convoy);
                return;
            }

            if (level >= CustomerProfiles.Get(CustomerType.Emergency).MinStationLevel &&
                random.NextFloat() < VisitorMath.EmergencyChancePerHour)
            {
                requests.Add(new SpawnRequest { Customer = CustomerType.Emergency });
                StationEvent.Push(events, StationEventType.SpecialArrived, default, (float)CustomerType.Emergency);
                return;
            }

            bool hasTires = SystemAPI.HasSingleton<StationUpgrades>() && SystemAPI.GetSingleton<StationUpgrades>().TireService > 0;
            if (hasTires && level >= CustomerProfiles.Get(CustomerType.TowTruck).MinStationLevel && daytime &&
                random.NextFloat() < VisitorMath.TowTruckChancePerHour)
            {
                requests.Add(new SpawnRequest { Customer = CustomerType.TowTruck });
                StationEvent.Push(events, StationEventType.SpecialArrived, default, (float)CustomerType.TowTruck);
            }
        }
    }
}
