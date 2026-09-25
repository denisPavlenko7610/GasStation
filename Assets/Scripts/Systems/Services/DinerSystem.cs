using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>
    /// The diner: cooks (and the player with E at the grill) turn ingredients into hot dogs and burgers on the
    /// counter; food left on the counter for three game hours goes cold and is thrown away. Sales happen in
    /// ShopSystem when visitors are hungry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(PlayerInteractionSystem))]
    [UpdateBefore(typeof(TrashPickupSystem))]
    public partial struct DinerSystem : ISystem
    {
        private const float GrillRadius = 2.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Diner>();
            state.RequireForUpdate<StationUpgrades>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int level = SystemAPI.GetSingleton<StationUpgrades>().Diner;
            if (level <= 0)
                return;

            var dinerEntity = SystemAPI.GetSingletonEntity<Diner>();
            ref var diner = ref SystemAPI.GetComponentRW<Diner>(dinerEntity).ValueRW;
            var counter = SystemAPI.GetBuffer<DinerCounter>(dinerEntity);
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            var time = SystemAPI.GetSingleton<GameTime>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            float deltaHours = deltaTime * time.MinutesPerSecond / 60f;
            float cookTime = DinerMath.CookTime(level);
            int capacity = DinerMath.CounterCapacity(level);

            float cooks = SystemAPI.HasSingleton<StaffPower>() ? SystemAPI.GetSingleton<StaffPower>().Cook : 0f;
            diner.CookProgress += cooks * deltaTime;

            bool playerCooked = PlayerAtGrill(ref state, diner.Grill);
            if (playerCooked)
                diner.CookProgress += cookTime * DinerMath.PlayerCookBonus;

            while (diner.CookProgress >= cookTime && counter.Length >= DinerDishes.Count)
            {
                int dish = DinerMath.NextDish(counter[0].Ready, counter[1].Ready, level, capacity);
                if (dish < 0 || diner.Ingredients <= 0)
                {
                    if (diner.Ingredients <= 0 && (cooks > 0f || playerCooked) && !diner.WarnedEmpty)
                    {
                        diner.WarnedEmpty = true;
                        StationEvent.Push(events, StationEventType.DinerOutOfIngredients);
                    }
                    diner.CookProgress = 0f;
                    break;
                }

                var item = counter[dish];
                if (item.Ready == 0)
                    item.HoursLeft = DinerMath.FreshHours;
                item.Ready++;
                counter[dish] = item;
                diner.Ingredients--;
                diner.CookProgress -= cookTime;
                if (playerCooked)
                {
                    StationEvent.Push(events, StationEventType.DishCooked, default, 0f, dish);
                    playerCooked = false;
                }
            }

            for (int i = 0; i < counter.Length; i++)
            {
                var item = counter[i];
                if (item.Ready <= 0)
                    continue;

                item.HoursLeft -= deltaHours;
                if (item.HoursLeft <= 0f)
                {
                    StationEvent.Push(events, StationEventType.FoodWasted, default, item.Ready, i);
                    item.Ready = 0;
                }

                counter[i] = item;
            }
        }

        /// <summary>The player pressed interact next to the grill (the press is used up).</summary>
        private bool PlayerAtGrill(ref SystemState state, float3 grill)
        {
            foreach (var (interaction, transform) in SystemAPI.Query<RefRW<PlayerInteraction>, RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                if (!interaction.ValueRO.InteractPressed ||
                    math.distancesq(transform.ValueRO.Position.xz, grill.xz) > GrillRadius * GrillRadius)
                    continue;

                interaction.ValueRW.InteractPressed = false;
                return true;
            }

            return false;
        }
    }
}
