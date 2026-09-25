using GasStation.Bridge;
using GasStation.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>Applies commands queued by the UI: price changes and fuel orders.</summary>
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial class StationCommandSystem : SystemBase
    {
        private const float MinPrice = 0.05f;

        protected override void OnCreate()
        {
            RequireForUpdate<Economy>();
            RequireForUpdate<StationSettings>();
        }

        protected override void OnUpdate()
        {
            var station = SystemAPI.GetSingletonEntity<Economy>();
            while (StationCommands.TryDequeue(out var command))
            {
                switch (command.Type)
                {
                    case StationCommandType.ChangePrice:
                        ChangePrice(station, command.Fuel, command.Value);
                        break;
                    case StationCommandType.OrderFuel:
                        OrderFuel(station, command.Fuel, command.Value);
                        break;
                }
            }
        }

        private void ChangePrice(Entity station, FuelType fuel, float delta)
        {
            var stock = SystemAPI.GetBuffer<FuelStock>(station);
            var entry = stock[(int)fuel];
            entry.SellPrice = math.max(MinPrice, entry.SellPrice + delta);
            stock[(int)fuel] = entry;
        }

        private void OrderFuel(Entity station, FuelType fuel, float liters)
        {
            var stock = SystemAPI.GetBuffer<FuelStock>(station)[(int)fuel];

            float pending = 0f;
            foreach (var delivery in SystemAPI.Query<RefRO<FuelDelivery>>())
            {
                if (delivery.ValueRO.Type == fuel)
                    pending += delivery.ValueRO.Liters;
            }

            liters = math.min(liters, stock.Capacity - stock.Amount - pending);
            if (liters < 1f)
            {
                HudModel.Notify("Резервуар полон");
                return;
            }

            var economy = SystemAPI.GetSingletonRW<Economy>();
            float cost = liters * stock.BuyPrice;
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify($"Не хватает денег: нужно {cost:0}");
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;

            var settings = SystemAPI.GetSingleton<StationSettings>();
            var order = EntityManager.CreateEntity();
            EntityManager.AddComponentData(order, new FuelDelivery
            {
                Type = fuel,
                Liters = liters,
                TimeLeft = settings.FuelDeliveryTime
            });

            HudModel.Notify($"Заказано {liters:0} л, привезут через {settings.FuelDeliveryTime:0} с");
        }
    }
}
