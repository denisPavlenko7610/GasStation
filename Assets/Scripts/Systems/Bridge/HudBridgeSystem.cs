using GasStation.Bridge;
using GasStation.Components;
using GasStation.Logic;
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
            if (SystemAPI.HasSingleton<StationLevel>())
                HudModel.Level = SystemAPI.GetSingleton<StationLevel>();
            if (SystemAPI.HasSingleton<WorldEvents>())
                HudModel.World = SystemAPI.GetSingleton<WorldEvents>();
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
                    case StationEventType.PumpBroken:
                        HudModel.Notify($"Колонка {stationEvent.Value:0} сломалась! Почините её (E)");
                        break;
                    case StationEventType.PumpRepaired:
                        HudModel.Notify($"Колонка {stationEvent.Value:0} как новая");
                        break;
                    case StationEventType.NotEnoughMoney:
                        HudModel.Notify($"Не хватает денег: нужно ${stationEvent.Value:0}");
                        break;
                    case StationEventType.LevelUp:
                        HudModel.Notify($"Уровень станции {stationEvent.Value:0}! Открыты новые улучшения и клиенты");
                        break;
                    case StationEventType.TipReceived:
                        HudModel.Notify($"Чаевые: ${stationEvent.Value:0}");
                        break;
                    case StationEventType.FuelStolen:
                        HudModel.Notify($"Вор уехал без оплаты! Потеряно ${stationEvent.Value:0}");
                        break;
                    case StationEventType.ThiefCaught:
                        HudModel.Notify("Вор пойман и заплатил!");
                        break;
                    case StationEventType.MarketChanged:
                        HudModel.Notify($"Цены на нефть: {stationEvent.Value:+0.0;-0.0;0}%. Проверьте свои цены");
                        break;
                    case StationEventType.Vandals:
                        HudModel.Notify("Ночью приходили вандалы и намусорили");
                        break;
                    case StationEventType.RushHourStarted:
                        HudModel.Notify("Час пик: туристы едут толпой!");
                        break;
                    case StationEventType.SandstormStarted:
                        HudModel.Notify("Песчаная буря: клиентов мало, мусора много");
                        break;
                    case StationEventType.InspectionPassed:
                        HudModel.Notify($"Проверка пройдена! Премия ${stationEvent.Value:0}");
                        break;
                    case StationEventType.InspectionFailed:
                        HudModel.Notify($"Проверка: грязно! Штраф ${stationEvent.Value:0}");
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

                if (HudModel.Hint != InteractionHint.CanStartFueling && pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity) &&
                    SystemAPI.GetComponent<Pump>(pumpEntity).Condition < ProgressMath.RepairThreshold)
                    HudModel.Hint = InteractionHint.Repair;

                bool fuelingAction = HudModel.Hint is InteractionHint.CanStartFueling or InteractionHint.Repair;
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
                Locked = pump.RequiredUpgradeLevel > HudModel.Upgrades.ExtraPump,
                Condition = pump.Condition
            };

            var occupant = pump.Occupant;
            if (occupant == Entity.Null || !SystemAPI.Exists(occupant) || !SystemAPI.HasComponent<Car>(occupant))
                return info;

            var car = SystemAPI.GetComponent<Car>(occupant);
            var patience = SystemAPI.GetComponent<Patience>(occupant);
            info.Occupied = true;
            info.Customer = car.Customer;
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
