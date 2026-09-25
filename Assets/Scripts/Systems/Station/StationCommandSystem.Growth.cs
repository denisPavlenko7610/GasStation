using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>Build mode, contracts, owner skills and hosted events.</summary>
    public partial class StationCommandSystem
    {
        private void PlaceProp(Entity station, PropType type, float3 position, float yaw)
        {
            if (!SystemAPI.HasSingleton<BuildArea>())
                return;

            var areaEntity = SystemAPI.GetSingletonEntity<BuildArea>();
            var area = SystemAPI.GetComponent<BuildArea>(areaEntity);
            position = PropMath.Snap(position);

            var zones = new List<NoBuildZone>();
            foreach (var zone in SystemAPI.GetBuffer<NoBuildZone>(areaEntity))
                zones.Add(zone);
            var others = new List<float2>();
            foreach (var prop in SystemAPI.Query<RefRO<PlacedProp>>())
                others.Add(prop.ValueRO.Position.xz);

            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            var info = PropMath.Get(type);
            var error = PropMath.Check(type, position.xz, area, zones, others, stationLevel, economy.ValueRO.Money);
            if (error != PlacementError.None)
            {
                HudModel.Notify(error switch
                {
                    PlacementError.NeedsLevel => Loc.F("msg.propNeedsLevel", GameTexts.PropName(type), info.RequiredLevel),
                    PlacementError.NoMoney => Loc.F("msg.noMoney", info.Cost),
                    _ => Loc.T($"build.error.{error}")
                });
                return;
            }

            economy.ValueRW.Money -= info.Cost;
            economy.ValueRW.DayExpenses += info.Cost;

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new PlacedProp
            {
                Id = area.NextPropId++,
                Type = type,
                Position = position,
                Yaw = yaw
            });
            SystemAPI.SetComponent(areaEntity, area);
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.PropPlaced, default, (int)type);
        }

        private void RemoveProp(Entity station, float3 position)
        {
            var nearest = Entity.Null;
            var nearestType = PropType.TrashBin;
            float best = PropMath.PickRadius * PropMath.PickRadius;
            foreach (var (prop, entity) in SystemAPI.Query<RefRO<PlacedProp>>().WithEntityAccess())
            {
                float distance = math.distancesq(prop.ValueRO.Position.xz, position.xz);
                if (distance > best)
                    continue;
                best = distance;
                nearest = entity;
                nearestType = prop.ValueRO.Type;
            }

            if (nearest == Entity.Null)
                return;

            float refund = PropMath.Refund(nearestType);
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            economy.ValueRW.Money += refund;
            EntityManager.DestroyEntity(nearest);
            HudModel.Notify(Loc.F("msg.propRemoved", GameTexts.PropName(nearestType), refund));
        }

        private void AcceptContract(Entity station, int offerId)
        {
            if (!SystemAPI.HasBuffer<ContractOffer>(station) || !SystemAPI.HasBuffer<Contract>(station))
                return;

            var offers = SystemAPI.GetBuffer<ContractOffer>(station);
            var contracts = SystemAPI.GetBuffer<Contract>(station);
            int index = -1;
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].Id == offerId)
                    index = i;
            }

            if (index < 0)
                return;

            if (contracts.Length >= ContractMath.MaxActive)
            {
                HudModel.Notify(Loc.F("msg.contractsFull", ContractMath.MaxActive));
                return;
            }

            var offer = offers[index];
            int stationLevel = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            int required = ContractMath.Get(offer.Type).RequiredLevel;
            if (stationLevel < required)
            {
                HudModel.Notify(Loc.F("msg.contractNeedsLevel", required));
                return;
            }

            offers.RemoveAt(index);
            contracts.Add(new Contract
            {
                Id = offer.Id,
                Type = offer.Type,
                Price = offer.Price,
                DaysLeft = offer.Days,
                LastSentDay = -1,
                LastSentHour = -1
            });
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.ContractAccepted, default, (float)offer.Type, offer.Id);
        }

        private void DeclineContract(Entity station, int offerId)
        {
            if (!SystemAPI.HasBuffer<ContractOffer>(station))
                return;

            var offers = SystemAPI.GetBuffer<ContractOffer>(station);
            for (int i = offers.Length - 1; i >= 0; i--)
            {
                if (offers[i].Id == offerId)
                    offers.RemoveAt(i);
            }
        }

        /// <summary>Walking away from a contract costs three missed-vehicle penalties and some reputation.</summary>
        private void CancelContract(Entity station, int contractId)
        {
            if (!SystemAPI.HasBuffer<Contract>(station))
                return;

            var contracts = SystemAPI.GetBuffer<Contract>(station);
            for (int i = 0; i < contracts.Length; i++)
            {
                if (contracts[i].Id != contractId)
                    continue;

                var contract = contracts[i];
                float penalty = ContractMath.CancelPenalty(contract.Type);
                var economy = SystemAPI.GetComponentRW<Economy>(station);
                economy.ValueRW.Money -= penalty;
                economy.ValueRW.DayExpenses += penalty;
                economy.ValueRW.Reputation = StationMath.ClampReputation(economy.ValueRO.Reputation - ContractMath.CancelReputation);
                contracts.RemoveAt(i);
                HudModel.Notify(Loc.F("msg.contractWalkedAway", GameTexts.ContractName(contract.Type), penalty));
                return;
            }
        }

        private void LearnSkill(Entity station, OwnerSkill skill)
        {
            if (!SystemAPI.HasComponent<OwnerSkillSet>(station))
                return;

            var skills = SystemAPI.GetComponent<OwnerSkillSet>(station);
            int level = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (!SkillMath.CanLearn(skills.Learned, skill, level))
            {
                HudModel.Notify(Loc.T(SkillMath.FreePoints(level, skills.Learned) <= 0 ? "msg.noSkillPoints" : "msg.skillLocked"));
                return;
            }

            skills.Learned = SkillMath.Learn(skills.Learned, skill);
            SystemAPI.SetComponent(station, skills);
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.SkillLearned, default, (int)skill);
        }

        /// <summary>Plans an event for tomorrow; paid now.</summary>
        private void PlanEvent(Entity station, HostedEventKind kind)
        {
            if (!SystemAPI.HasComponent<HostedEvents>(station) || kind == HostedEventKind.None)
                return;

            var hosted = SystemAPI.GetComponent<HostedEvents>(station);
            int day = SystemAPI.GetComponent<GameTime>(station).Day;
            var info = HostedEventMath.Get(kind);
            int level = SystemAPI.HasComponent<StationLevel>(station) ? SystemAPI.GetComponent<StationLevel>(station).Level : 1;
            if (hosted.Planned != HostedEventKind.None || hosted.Active != HostedEventKind.None)
            {
                HudModel.Notify(Loc.T("msg.eventAlreadyPlanned"));
                return;
            }

            if (!HostedEventMath.CanPlan(day + 1, hosted.LastHostedDay))
            {
                HudModel.Notify(Loc.F("msg.eventTooSoon", hosted.LastHostedDay + HostedEventMath.EveryDays));
                return;
            }

            if (level < info.RequiredLevel)
            {
                HudModel.Notify(Loc.F("msg.contractNeedsLevel", info.RequiredLevel));
                return;
            }

            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < info.Cost)
            {
                HudModel.Notify(Loc.F("msg.noMoney", info.Cost));
                return;
            }

            economy.ValueRW.Money -= info.Cost;
            economy.ValueRW.DayExpenses += info.Cost;
            hosted.Planned = kind;
            hosted.PlannedDay = day + 1;
            SystemAPI.SetComponent(station, hosted);
            HudModel.Notify(Loc.F("msg.eventPlanned", Loc.T($"hosted.{kind}"), info.StartHour));
        }
    }
}
