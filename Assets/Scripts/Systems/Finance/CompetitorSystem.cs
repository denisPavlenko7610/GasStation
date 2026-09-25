using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// The PetroMax station across the road. Opens on day 3, reacts every morning to how many drivers we took
    /// (cuts prices, raises them or runs a promo) and keeps <see cref="Competitor.OurShare"/> up to date for
    /// <see cref="CarSpawnSystem"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(MarketSystem))]
    [UpdateBefore(typeof(CarSpawnSystem))]
    public partial struct CompetitorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Competitor>();
            state.RequireForUpdate<Economy>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<FuelStock>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var rival = ref SystemAPI.GetSingletonRW<Competitor>().ValueRW;
            if (rival.BoughtOut)
            {
                rival.OurShare = 1f;
                return;
            }

            var stock = SystemAPI.GetSingletonBuffer<FuelStock>(true);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            int day = SystemAPI.GetSingleton<GameTime>().Day;

            if (!rival.Active)
            {
                if (day < rival.OpensOnDay)
                    return;

                rival.Active = true;
                for (int i = 0; i < stock.Length; i++)
                    rival.SetPrice((FuelType)i, CompetitionMath.NextPrice(stock[i].MarketPrice * 0.97f, stock[i].MarketPrice, CompetitorMove.Hold));
                StationEvent.Push(events, StationEventType.CompetitorOpened);
            }

            if (StationEvent.Contains(events, StationEventType.DayEnded))
                MorningMove(ref rival, stock, events);

            rival.OurShare = CompetitionMath.Share(OurAppeal(ref state, stock), TheirAppeal(rival, stock));
        }

        private static void MorningMove(ref Competitor rival, DynamicBuffer<FuelStock> stock, DynamicBuffer<StationEvent> events)
        {
            if (rival.PromoDaysLeft > 0 && --rival.PromoDaysLeft == 0)
                rival.Promo = CompetitorPromo.None;

            var move = CompetitionMath.DecideMove(rival.OurShare, rival.Random.NextFloat());
            if (move == CompetitorMove.Promo && rival.Promo != CompetitorPromo.None)
                move = CompetitorMove.CutPrices;

            for (int i = 0; i < stock.Length; i++)
            {
                var fuel = (FuelType)i;
                var priceMove = move == CompetitorMove.Promo ? CompetitorMove.Hold : move;
                rival.SetPrice(fuel, CompetitionMath.NextPrice(rival.Price(fuel), stock[i].MarketPrice, priceMove));
            }

            if (move == CompetitorMove.Promo)
            {
                rival.Promo = rival.Random.NextFloat() < 0.5f ? CompetitorPromo.Discount : CompetitorPromo.Advertising;
                rival.PromoDaysLeft = 2;
                StationEvent.Push(events, StationEventType.CompetitorPromoStarted, default, (float)rival.Promo);
            }
            else if (move == CompetitorMove.CutPrices)
            {
                StationEvent.Push(events, StationEventType.CompetitorPriceCut);
            }
            else if (move == CompetitorMove.RaisePrices)
            {
                StationEvent.Push(events, StationEventType.CompetitorPriceRise);
            }

            rival.Reputation = CompetitionMath.NextReputation(rival.Reputation, rival.Promo);
        }

        private float OurAppeal(ref SystemState state, DynamicBuffer<FuelStock> stock)
        {
            float price = 0f, market = 0f;
            for (int i = 0; i < stock.Length; i++)
            {
                price += stock[i].SellPrice;
                market += stock[i].MarketPrice;
            }

            float cleanliness = SystemAPI.HasSingleton<StationCleanliness>()
                ? StationMath.CleanlinessTrafficFactor(SystemAPI.GetSingleton<StationCleanliness>().Value)
                : 1f;
            int services = 0;
            float decor = 1f;
            if (SystemAPI.HasSingleton<StationUpgrades>())
            {
                var upgrades = SystemAPI.GetSingleton<StationUpgrades>();
                services = (upgrades.CarWash > 0 ? 1 : 0) + (upgrades.TireService > 0 ? 1 : 0) +
                           (upgrades.Motel > 0 ? 1 : 0) + (upgrades.TruckParking > 0 ? 1 : 0) +
                           (upgrades.Comfort > 0 ? 1 : 0) + (upgrades.Attendant > 0 ? 1 : 0);
                decor = UpgradeMath.DecorMultiplier(upgrades.Decor);
            }

            float reputation = SystemAPI.GetSingleton<Economy>().Reputation;
            return CompetitionMath.Appeal(price / stock.Length, market / stock.Length, reputation,
                CompetitionMath.OurExtras(cleanliness, services, decor));
        }

        private static float TheirAppeal(in Competitor rival, DynamicBuffer<FuelStock> stock)
        {
            float price = 0f, market = 0f;
            for (int i = 0; i < stock.Length; i++)
            {
                float theirs = rival.Price((FuelType)i);
                if (rival.Promo == CompetitorPromo.Discount)
                    theirs *= CompetitionMath.DiscountShare;
                price += theirs;
                market += stock[i].MarketPrice;
            }

            return CompetitionMath.Appeal(price / stock.Length, market / stock.Length, rival.Reputation,
                CompetitionMath.PromoFactor(rival.Promo));
        }
    }
}
