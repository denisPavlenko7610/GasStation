using GasStation.Bridge;
using GasStation.Components;
using GasStation.Logic;
using GasStation.Save;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>Applies commands queued by the UI: prices, fuel orders, upgrades, save/load.</summary>
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    public partial class StationCommandSystem : SystemBase
    {
        private const float MinPrice = 0.05f;

        protected override void OnCreate()
        {
            RequireForUpdate<Economy>();
            RequireForUpdate<StationSettings>();
            RequireForUpdate<StationUpgrades>();
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
                    case StationCommandType.BuyUpgrade:
                        BuyUpgrade(station, command.Upgrade);
                        break;
                    case StationCommandType.SaveGame:
                        SaveService.Write(SaveService.Capture(EntityManager, station));
                        HudModel.Notify("Игра сохранена");
                        break;
                    case StationCommandType.LoadGame:
                        if (SaveService.TryRead(out var data))
                        {
                            SaveService.Apply(EntityManager, station, data);
                            HudModel.Notify("Игра загружена");
                        }
                        else
                        {
                            HudModel.Notify("Сохранение не найдено");
                        }
                        break;
                    case StationCommandType.ChangeProductPrice:
                        ChangeProductPrice(command.Product, command.Value);
                        break;
                    case StationCommandType.OrderProducts:
                        OrderProducts(station, command.Product, (int)command.Value);
                        break;
                    case StationCommandType.NewGame:
                        SaveService.Delete();
                        if (SaveService.Defaults != null)
                            SaveService.Apply(EntityManager, station, SaveService.Defaults);
                        HudModel.Notify("Новая игра");
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
                HudModel.Notify($"Не хватает денег: нужно ${cost:0}");
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

            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.FuelOrdered, fuel, liters);
            HudModel.Notify($"Заказано {liters:0} л {GameTexts.FuelName(fuel)}, привезут через {settings.FuelDeliveryTime:0} с");
        }

        private void ChangeProductPrice(ProductType product, float delta)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            var shelf = shelves[(int)product];
            shelf.SellPrice = math.max(MinPrice, shelf.SellPrice + delta);
            shelves[(int)product] = shelf;
        }

        private void OrderProducts(Entity station, ProductType product, int count)
        {
            if (!SystemAPI.HasSingleton<Shop>())
            {
                HudModel.Notify("На станции нет магазина");
                return;
            }

            var shop = SystemAPI.GetSingleton<Shop>();
            var shelf = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>())[(int)product];

            int pending = 0;
            foreach (var delivery in SystemAPI.Query<RefRO<ProductDelivery>>())
            {
                if (delivery.ValueRO.Type == product)
                    pending += delivery.ValueRO.Count;
            }

            count = math.min(count, shelf.Capacity - shelf.Stock - pending);
            if (count <= 0)
            {
                HudModel.Notify($"{GameTexts.ProductName(product)}: полки заполнены");
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float cost = count * shelf.BuyPrice;
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify($"Не хватает денег: нужно ${cost:0}");
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;

            var order = EntityManager.CreateEntity();
            EntityManager.AddComponentData(order, new ProductDelivery { Type = product, Count = count, TimeLeft = shop.DeliveryTime });
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.ProductsOrdered, default, count);
            HudModel.Notify($"Заказано: {GameTexts.ProductName(product)} × {count}, привезут через {shop.DeliveryTime:0} с");
        }

        private void BuyUpgrade(Entity station, UpgradeType type)
        {
            var upgrades = SystemAPI.GetComponentRW<StationUpgrades>(station);
            int level = upgrades.ValueRO.Get(type);
            if (!UpgradeMath.CanUpgrade(type, level))
            {
                HudModel.Notify($"{GameTexts.UpgradeName(type)}: максимальный уровень");
                return;
            }

            int requiredLevel = ProgressMath.RequiredLevel(type, level);
            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (stationLevel < requiredLevel)
            {
                HudModel.Notify($"{GameTexts.UpgradeName(type)}: нужен уровень станции {requiredLevel}");
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float cost = UpgradeMath.Cost(type, level);
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify($"Не хватает денег: нужно ${cost:0}");
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;
            upgrades.ValueRW.Set(type, level + 1);

            switch (type)
            {
                case UpgradeType.TankCapacity:
                    var stock = SystemAPI.GetBuffer<FuelStock>(station);
                    for (int i = 0; i < stock.Length; i++)
                    {
                        var entry = stock[i];
                        entry.Capacity += UpgradeMath.TankBonusPerLevel;
                        stock[i] = entry;
                    }
                    break;
                case UpgradeType.Attendant:
                    economy.ValueRW.DailyFixedCosts += UpgradeMath.AttendantSalaryPerLevel;
                    break;
                case UpgradeType.Janitor:
                    economy.ValueRW.DailyFixedCosts += UpgradeMath.JanitorSalaryPerLevel;
                    break;
                case UpgradeType.Mechanic:
                    economy.ValueRW.DailyFixedCosts += ProgressMath.MechanicSalaryPerLevel;
                    break;
            }

            HudModel.Notify($"Куплено: {GameTexts.UpgradeName(type)}, уровень {level + 1}");
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.UpgradeBought, default, cost);
        }
    }
}
