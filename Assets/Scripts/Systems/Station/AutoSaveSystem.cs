using GasStation.Bridge;
using GasStation.Components;
using GasStation.Save;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>Remembers the baked state for "new game", loads the save on start and saves at the end of each day.</summary>
    [UpdateInGroup(typeof(StationSystemGroup))]
    [UpdateAfter(typeof(GameTimeSystem))]
    [UpdateBefore(typeof(StationCommandSystem))]
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
                    HudModel.Notify($"Продолжаем: день {data.day}");
                }

                _lastSavedDay = SystemAPI.GetSingleton<DayReport>().Day;
                return;
            }

            int finishedDay = SystemAPI.GetSingleton<DayReport>().Day;
            if (finishedDay == _lastSavedDay)
                return;

            _lastSavedDay = finishedDay;
            SaveService.Write(SaveService.Capture(EntityManager, station));
        }
    }
}
