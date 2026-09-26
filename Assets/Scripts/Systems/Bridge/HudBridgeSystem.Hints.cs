using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>What the player can do right here (the E hint) and where the current quest points.</summary>
    public partial class HudBridgeSystem
    {
        /// <summary>Picks the place the current quest is about, nearest to the player.</summary>
        private void CopyQuestTarget()
        {
            HudModel.HasQuestTarget = false;
            float3 player = float3.zero;
            bool hasPlayer = false;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                player = transform.ValueRO.Position;
                hasPlayer = true;
            }

            if (!hasPlayer)
                return;

            float best = float.MaxValue;
            float3 target = float3.zero;

            switch (HudModel.Quest.Goal)
            {
                case QuestGoal.CollectTrash:
                    foreach (var transform in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<Trash>())
                        Consider(ref best, ref target, player, transform.ValueRO.Position);
                    break;

                case QuestGoal.RepairPump:
                    foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
                    {
                        if (pump.ValueRO.Condition < ProgressMath.RepairThreshold &&
                            pump.ValueRO.RequiredUpgradeLevel <= HudModel.Upgrades.ExtraPump)
                            Consider(ref best, ref target, player, pump.ValueRO.InteractionPoint);
                    }
                    break;

                case QuestGoal.ServeCustomers:
                case QuestGoal.CatchThief:
                    foreach (var pump in SystemAPI.Query<RefRO<Pump>>())
                    {
                        var occupant = pump.ValueRO.Occupant;
                        if (occupant != Entity.Null && SystemAPI.Exists(occupant) && SystemAPI.HasComponent<Car>(occupant) &&
                            SystemAPI.GetComponent<Car>(occupant).State == CarState.WaitingForService)
                            Consider(ref best, ref target, player, pump.ValueRO.InteractionPoint);
                    }
                    break;

                case QuestGoal.Renovate:
                    foreach (var renovation in SystemAPI.Query<RefRO<Renovation>>())
                    {
                        if (!renovation.ValueRO.Done)
                            Consider(ref best, ref target, player, renovation.ValueRO.Position);
                    }
                    break;

                case QuestGoal.CleanRestroom:
                    if (SystemAPI.HasSingleton<Restroom>() && SystemAPI.GetSingleton<Restroom>().Dirt > FacilityMath.RestroomCleanThreshold)
                        Consider(ref best, ref target, player, SystemAPI.GetSingleton<Restroom>().Door);
                    break;

                case QuestGoal.ChangeTires:
                    if (HudModel.TireCarWaiting)
                        Consider(ref best, ref target, player, SystemAPI.GetSingleton<TireService>().Bay);
                    break;

                case QuestGoal.HostGuests:
                    if (HudModel.MotelDirty > 0)
                        Consider(ref best, ref target, player, SystemAPI.GetSingleton<Motel>().Door);
                    break;
            }

            if (best == float.MaxValue)
                return;

            HudModel.HasQuestTarget = true;
            HudModel.QuestTarget = target;
        }

        private static void Consider(ref float best, ref float3 target, float3 player, float3 position)
        {
            float distance = math.distancesq(position.xz, player.xz);
            if (distance < best)
            {
                best = distance;
                target = position;
            }
        }

        private void CopyHint()
        {
            HudModel.Hint = InteractionHint.None;
            foreach (var interaction in SystemAPI.Query<RefRO<PlayerInteraction>>().WithAll<PlayerTag>())
            {
                var pumpEntity = interaction.ValueRO.NearbyPump;
                if (pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity))
                    HudModel.Hint = HintFor(Describe(SystemAPI.GetComponent<Pump>(pumpEntity)));

                if (HudModel.Hint is not (InteractionHint.CanStartFueling or InteractionHint.Fueling) && pumpEntity != Entity.Null && SystemAPI.Exists(pumpEntity) &&
                    SystemAPI.GetComponent<Pump>(pumpEntity).Condition < ProgressMath.RepairThreshold)
                    HudModel.Hint = InteractionHint.Repair;

                bool fuelingAction = HudModel.Hint is InteractionHint.CanStartFueling or InteractionHint.Fueling or InteractionHint.Repair;
                if (!fuelingAction && NearWaitingTireCar())
                {
                    HudModel.Hint = InteractionHint.Tires;
                    fuelingAction = true;
                }

                if (!fuelingAction && NearDirtyMotel())
                {
                    HudModel.Hint = InteractionHint.MotelRoom;
                    fuelingAction = true;
                }

                if (!fuelingAction && NearDirtyRestroom())
                {
                    HudModel.Hint = InteractionHint.Restroom;
                    fuelingAction = true;
                }

                // Same order as the systems that consume the interact press.
                var renovationEntity = interaction.ValueRO.NearbyRenovation;
                if (!fuelingAction && renovationEntity != Entity.Null && SystemAPI.Exists(renovationEntity))
                {
                    HudModel.NearRenovationKind = SystemAPI.GetComponent<Renovation>(renovationEntity).Kind;
                    HudModel.Hint = InteractionHint.Renovate;
                    fuelingAction = true;
                }

                var trash = interaction.ValueRO.NearbyTrash;
                if (!fuelingAction && trash != Entity.Null && SystemAPI.Exists(trash))
                    HudModel.Hint = InteractionHint.Trash;
            }

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
                HudModel.PlayerPosition = transform.ValueRO.Position;

            // The grill comes before the laptop: E cooks there.
            if (HudModel.Hint is InteractionHint.None or InteractionHint.PumpFree && HudModel.Upgrades.Diner > 0 &&
                SystemAPI.HasSingleton<Diner>())
            {
                var grillOffset = HudModel.PlayerPosition - (UnityEngine.Vector3)SystemAPI.GetSingleton<Diner>().Grill;
                grillOffset.y = 0f;
                if (grillOffset.sqrMagnitude <= 2.5f * 2.5f)
                    HudModel.Hint = InteractionHint.Grill;
            }

            HudModel.HasLaptop = SystemAPI.HasSingleton<Laptop>();
            if (!HudModel.HasLaptop)
                return;

            HudModel.LaptopPosition = SystemAPI.GetSingleton<Laptop>().Position;
            // The laptop has the lowest priority: E does the station work first.
            var offset = HudModel.PlayerPosition - HudModel.LaptopPosition;
            offset.y = 0f;
            if (HudModel.Hint is InteractionHint.None or InteractionHint.PumpFree &&
                offset.sqrMagnitude <= HudModel.LaptopRadius * HudModel.LaptopRadius)
                HudModel.Hint = InteractionHint.Laptop;
        }

        private bool NearWaitingTireCar()
        {
            if (!HudModel.TireCarWaiting || !SystemAPI.HasSingleton<StationSettings>())
                return false;

            var bay = SystemAPI.GetSingleton<TireService>().Bay;
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithAll<PlayerTag>())
            {
                if (Unity.Mathematics.math.distancesq(transform.ValueRO.Position.xz, bay.xz) <= radius * radius)
                    return true;
            }

            return false;
        }

        private bool NearDirtyMotel()
        {
            if (HudModel.MotelDirty == 0 || !SystemAPI.HasSingleton<StationSettings>())
                return false;

            var door = SystemAPI.GetSingleton<Motel>().Door;
            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithAll<PlayerTag>())
            {
                if (Unity.Mathematics.math.distancesq(transform.ValueRO.Position.xz, door.xz) <= radius * radius)
                    return true;
            }

            return false;
        }

        private bool NearDirtyRestroom()
        {
            if (!SystemAPI.HasSingleton<Restroom>() || !SystemAPI.HasSingleton<StationSettings>())
                return false;

            var restroom = SystemAPI.GetSingleton<Restroom>();
            if (restroom.Dirt <= FacilityMath.RestroomCleanThreshold)
                return false;

            float radius = SystemAPI.GetSingleton<StationSettings>().InteractionRadius;
            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithAll<PlayerTag>())
            {
                if (Unity.Mathematics.math.distancesq(transform.ValueRO.Position.xz, restroom.Door.xz) <= radius * radius)
                    return true;
            }

            return false;
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
