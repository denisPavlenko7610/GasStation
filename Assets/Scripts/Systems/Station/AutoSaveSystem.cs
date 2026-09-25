using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Save;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Remembers the baked state for "new game", loads the save on start and saves at the end of each day.
    /// Runs last so that the save already contains everything that happens at midnight (bills, taxes, staff
    /// mood, stars, contracts, market, weather).
    /// </summary>
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(AchievementSystem))]
    public partial class AutoSaveSystem : SystemBase
    {
        private Entity _station;
        private int _lastSavedDay;

        protected override void OnCreate()
        {
            RequireForUpdate<Economy>();
            RequireForUpdate<StationUpgrades>();
            RequireForUpdate<DayReport>();
        }

        protected override void OnUpdate()
        {
            var station = SystemAPI.GetSingletonEntity<Economy>();
            if (station != _station)
            {
                _station = station;
                SaveService.Defaults = SaveService.Capture(EntityManager, station);
                if (SaveService.TryRead(out var data))
                {
                    SaveService.Apply(EntityManager, station, data);
                    HudModel.Notify(Loc.F("msg.continue", data.day));
                }

                _lastSavedDay = SystemAPI.GetSingleton<DayReport>().Day;
                return;
            }

            int finishedDay = SystemAPI.GetSingleton<DayReport>().Day;
            if (finishedDay == _lastSavedDay)
                return;

            _lastSavedDay = finishedDay;
            if (!GameSettings.Autosave)
                return;

            SaveService.Write(SaveService.Capture(EntityManager, station));
        }
    }
}
