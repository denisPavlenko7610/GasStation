using GasStation.Bridge;
using GasStation.Components;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Copies simulation state and events into HudModel for the MonoBehaviour UI and audio.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HudBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            HudModel.Events.Clear();
            HudModel.HasStation = SystemAPI.HasSingleton<Economy>() && SystemAPI.HasSingleton<GameTime>();
            if (!HudModel.HasStation)
                return;

            var time = SystemAPI.GetSingleton<GameTime>();
            HudModel.Hour = time.Hour;
            HudModel.Day = time.Day;
            HudModel.Economy = SystemAPI.GetSingleton<Economy>();
            HudModel.LastReport = SystemAPI.GetSingleton<DayReport>();
            HudModel.Upgrades = SystemAPI.GetSingleton<StationUpgrades>();
            if (SystemAPI.HasSingleton<StationCleanliness>())
                HudModel.Cleanliness = SystemAPI.GetSingleton<StationCleanliness>();

            DrainEvents();
            CopyFuel();
            CopyCars();
            CopyPumps();
            CopyHint();
        }

        private void DrainEvents()
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                var stationEvent = events[i];
                HudModel.Events.Add(stationEvent);

                string fuel = GameTexts.FuelName(stationEvent.Fuel);
                switch (stationEvent.Type)
                {
                    case StationEventType.CustomerLeftAngry:
                        HudModel.Notify("Клиент уехал недовольным");
                        break;
                    case StationEventType.FuelRanOut:
                        HudModel.Notify($"Закончилось топливо: {fuel}! Закажите бензовоз (O)");
                        break;
                    case StationEventType.FuelDelivered:
                        HudModel.Notify($"Бензовоз привёз {stationEvent.Value:0} л {fuel}");
                        break;
                }
            }

            events.Clear();
        }

        private void CopyFuel()
        {
            var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                HudModel.Fuel[i] = i < stock.Length ? stock[i] : default;
                HudModel.PendingDelivery[i] = 0f;
            }

            foreach (var delivery in SystemAPI.Query<RefRO<FuelDelivery>>())
                HudModel.PendingDelivery[(int)delivery.ValueRO.Type] += delivery.ValueRO.Liters;
        }

        private void CopyCars()
        {
            int queueLength = 0;
            int carsOnSite = 0;
            bool anyFueling = false;
            foreach (var car in SystemAPI.Query<RefRO<Car>>())
            {
                var state = car.ValueRO.State;
                if (state == CarState.Queued || state == CarState.Arriving)
                    queueLength++;
                if (state != CarState.Leaving)
                    carsOnSite++;
                anyFueling |= state == CarState.Fueling;
            }

            HudModel.QueueLength = queueLength;
            HudModel.CarsOnSite = carsOnSite;
            HudModel.AnyFueling = anyFueling;
        }

        private void CopyPumps()
        {
            HudModel.Pumps.Clear();
            foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
                HudModel.Pumps.Add(Describe(pump.ValueRO));
            HudModel.Pumps.Sort((a, b) => a.Number.CompareTo(b.Number));
        }

        private void CopyHint()
        {
            HudModel.Hint = InteractionHint.None;
            foreach (var interaction in SystemAPI.Query<RefRO<PlayerInteraction>>().WithAll<PlayerTag>())
            {
                var pumpEntity = interaction.ValueRO.NearbyPump;
                if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                    HudModel.Hint = HintFor(Describe(SystemAPI.GetComponent<Pump>(pumpEntity)));

                bool fuelingAction = HudModel.Hint is InteractionHint.CanStartFueling;
                var trash = interaction.ValueRO.NearbyTrash;
                if (!fuelingAction && trash != Entity.Null && SystemAPI.Exists(trash))
                    HudModel.Hint = InteractionHint.Trash;
            }
        }

        private PumpInfo Describe(Pump pump)
        {
            var info = new PumpInfo
            {
                Number = pump.Number,
                Locked = pump.RequiredUpgradeLevel > HudModel.Upgrades.ExtraPump
            };

            var occupant = pump.Occupant;
            if (occupant == Entity.Null || !SystemAPI.Exists(occupant) || !SystemAPI.HasComponent<Car>(occupant))
                return info;

            var car = SystemAPI.GetComponent<Car>(occupant);
            var patience = SystemAPI.GetComponent<Patience>(occupant);
            info.Occupied = true;
            info.CarState = car.State;
            info.FuelType = car.FuelType;
            info.RequestedLiters = car.RequestedLiters;
            info.ReceivedLiters = car.ReceivedLiters;
            info.PatienceRatio = patience.Max > 0f ? patience.Current / patience.Max : 0f;
            return info;
        }

        private static InteractionHint HintFor(PumpInfo pump)
        {
            if (!pump.Occupied)
                return InteractionHint.PumpFree;

            return pump.CarState switch
            {
                CarState.WaitingForService => InteractionHint.CanStartFueling,
                CarState.Fueling => InteractionHint.Fueling,
                _ => InteractionHint.CarArriving
            };
        }
    }
}
