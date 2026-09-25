using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>Shop and diner commands: prices, orders, promos, the supplier, ingredients.</summary>
    public partial class StationCommandSystem
    {
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
                HudModel.Notify(Loc.T("msg.noShop"));
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
                HudModel.Notify(Loc.F("msg.shelvesFull", GameTexts.ProductName(product)));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            float cost = math.round(count * shelf.BuyPrice * ShopMath.SupplierPriceFactor(shop.Supplier)
                                    * SkillMath.GoodsPriceFactor((SystemAPI.HasSingleton<OwnerSkillSet>() ? SystemAPI.GetSingleton<OwnerSkillSet>().Learned : 0)) * 100f) / 100f;
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;

            float time = shop.DeliveryTime * ShopMath.SupplierTimeFactor(shop.Supplier);
            var order = EntityManager.CreateEntity();
            EntityManager.AddComponentData(order, new ProductDelivery { Type = product, Count = count, TimeLeft = time, Supplier = shop.Supplier });
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.ProductsOrdered, default, count);
            HudModel.Notify(Loc.F("msg.productsOrdered", GameTexts.ProductName(product), count, time));
        }

        private void TogglePromo(ProductType product)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return;

            var shelves = SystemAPI.GetBuffer<ShopProduct>(SystemAPI.GetSingletonEntity<Shop>());
            var shelf = shelves[(int)product];
            shelf.Promo = !shelf.Promo;
            shelves[(int)product] = shelf;
            HudModel.Notify(Loc.F(shelf.Promo ? "msg.promoOn" : "msg.promoOff", GameTexts.ProductName(product)));
        }

        private void SetSupplier(SupplierKind supplier)
        {
            if (!SystemAPI.HasSingleton<Shop>())
                return;

            var shopEntity = SystemAPI.GetSingletonEntity<Shop>();
            var shop = SystemAPI.GetComponent<Shop>(shopEntity);
            shop.Supplier = supplier;
            SystemAPI.SetComponent(shopEntity, shop);
            HudModel.Notify(Loc.F("msg.supplier", Loc.T($"supplier.{supplier}")));
        }

        /// <summary>Ingredients for the diner go straight into the fridge.</summary>
        private void OrderIngredients(Entity station)
        {
            int level = SystemAPI.GetComponent<StationUpgrades>(station).Diner;
            if (!SystemAPI.HasSingleton<Diner>() || level <= 0)
            {
                HudModel.Notify(Loc.T("msg.noDiner"));
                return;
            }

            var dinerEntity = SystemAPI.GetSingletonEntity<Diner>();
            var diner = SystemAPI.GetComponent<Diner>(dinerEntity);
            int count = math.min(DinerMath.IngredientOrder, DinerMath.IngredientCapacity(level) - diner.Ingredients);
            if (count <= 0)
            {
                HudModel.Notify(Loc.T("msg.fridgeFull"));
                return;
            }

            float cost = count * DinerMath.IngredientPrice;
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", cost));
                return;
            }

            economy.ValueRW.Money -= cost;
            economy.ValueRW.DayExpenses += cost;
            diner.Ingredients += count;
            diner.WarnedEmpty = false;
            SystemAPI.SetComponent(dinerEntity, diner);
            HudModel.Notify(Loc.F("msg.ingredientsBought", count, cost));
        }
    }
}
