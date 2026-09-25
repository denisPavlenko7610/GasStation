using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;

namespace GasStation.Systems
{
    /// <summary>Staff commands: hiring, firing, praise, training, raises and shifts.</summary>
    public partial class StationCommandSystem
    {
        private void HireCandidate(Entity station, int index)
        {
            if (!SystemAPI.HasComponent<StaffRoster>(station))
                return;

            var candidates = SystemAPI.GetBuffer<StaffCandidate>(station);
            if (index < 0 || index >= candidates.Length)
                return;

            if (SystemAPI.GetComponent<StaffPower>(station).Headcount >= StaffMath.MaxStaff)
            {
                HudModel.Notify(Loc.F("msg.staffFull", StaffMath.MaxStaff));
                return;
            }

            var candidate = candidates[index];
            float fee = StaffMath.HiringFee(candidate.Wage);
            var economy = SystemAPI.GetComponentRW<Economy>(station);
            if (economy.ValueRO.Money < fee)
            {
                HudModel.Notify(Loc.F("msg.noMoneyHire", fee));
                return;
            }

            economy.ValueRW.Money -= fee;
            economy.ValueRW.DayExpenses += fee;
            candidates.RemoveAt(index);

            var roster = SystemAPI.GetComponent<StaffRoster>(station);
            int id = roster.NextId++;
            SystemAPI.SetComponent(station, roster);
            StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.WorkerHired, default, id);

            // New people fill the emptier shift of their role, so nights are covered too.
            int dayShift = 0, nightShift = 0;
            foreach (var other in SystemAPI.Query<RefRO<Worker>>())
            {
                if (other.ValueRO.Role != candidate.Role)
                    continue;
                if (other.ValueRO.Shift == WorkShift.Day)
                    dayShift++;
                else
                    nightShift++;
            }

            var worker = EntityManager.CreateEntity();
            EntityManager.AddComponentData(worker, new Worker
            {
                Id = id,
                Role = candidate.Role,
                Skill = candidate.Skill,
                Wage = candidate.Wage,
                Honesty = candidate.Honesty,
                NameIndex = candidate.NameIndex,
                Trait = candidate.Trait,
                Shift = dayShift > nightShift ? WorkShift.Night : WorkShift.Day,
                Energy = 1f,
                Mood = StaffMath.StartMood,
                PraisedDay = -1
            });

            HudModel.Notify(Loc.F("msg.hired", GameTexts.RoleName(candidate.Role), GameTexts.StaffName(candidate.NameIndex)));
        }

        private void FireWorker(Entity station, int id)
        {
            foreach (var (worker, entity) in SystemAPI.Query<RefRO<Worker>>().WithEntityAccess())
            {
                if (worker.ValueRO.Id != id)
                    continue;

                string name = GameTexts.StaffName(worker.ValueRO.NameIndex);
                var role = worker.ValueRO.Role;
                StationEvent.Push(SystemAPI.GetBuffer<StationEvent>(station), StationEventType.WorkerFired, default, id);
                EntityManager.DestroyEntity(entity);
                HudModel.Notify(Loc.F("msg.fired", GameTexts.RoleName(role), name));
                return;
            }
        }

        private void ManageWorker(Entity station, StationCommandType action, int workerId)
        {
            int today = SystemAPI.HasComponent<GameTime>(station) ? SystemAPI.GetComponent<GameTime>(station).Day : 0;
            foreach (var worker in SystemAPI.Query<RefRW<Worker>>())
            {
                if (worker.ValueRO.Id != workerId)
                    continue;

                ref var w = ref worker.ValueRW;
                string name = GameTexts.StaffName(w.NameIndex);
                switch (action)
                {
                    case StationCommandType.PraiseWorker:
                        if (w.PraisedDay == today)
                        {
                            HudModel.Notify(Loc.F("msg.alreadyPraised", name));
                            return;
                        }

                        w.PraisedDay = today;
                        w.Mood = math.saturate(w.Mood + StaffMath.PraiseMood);
                        HudModel.Notify(Loc.F("msg.praised", name));
                        break;

                    case StationCommandType.TrainWorker:
                    {
                        if (!StaffMath.CanTrain(w))
                        {
                            HudModel.Notify(Loc.F("msg.trainingMax", name));
                            return;
                        }

                        float cost = StaffMath.TrainingCost(w.Training);
                        var economy = SystemAPI.GetComponentRW<Economy>(station);
                        if (economy.ValueRO.Money < cost)
                        {
                            HudModel.Notify(Loc.F("msg.noMoney", cost));
                            return;
                        }

                        economy.ValueRW.Money -= cost;
                        economy.ValueRW.DayExpenses += cost;
                        w.Training++;
                        w.Skill = math.min(StaffMath.MaxSkill, w.Skill + StaffMath.TrainingSkill);
                        HudModel.Notify(Loc.F("msg.trained", name, w.Skill * 100f));
                        break;
                    }

                    case StationCommandType.RaiseWage:
                        w.Wage = StaffMath.Raise(w.Wage);
                        w.Mood = math.saturate(w.Mood + StaffMath.PraiseMood);
                        HudModel.Notify(Loc.F("msg.raised", name, w.Wage));
                        break;

                    case StationCommandType.ToggleShift:
                        w.Shift = w.Shift == WorkShift.Day ? WorkShift.Night : WorkShift.Day;
                        HudModel.Notify(Loc.F("msg.shiftChanged", name, Loc.T($"shift.{w.Shift}")));
                        break;
                }

                return;
            }
        }
    }
}
