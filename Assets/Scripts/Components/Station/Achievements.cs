using Unity.Entities;

namespace GasStation.Components
{
    /// <summary>Lifetime statistics of the station, used by achievements.</summary>
    public struct StationStats : IComponentData
    {
        public int Served;
        public int TrashCollected;
        public float Income;
        public int ThievesCaught;
        public int MotelGuests;
        public int TruckersHosted;
        public int TiresChanged;
        public int CarsWashed;
        public int PerfectDays;
        public int DaysPlayed;
    }

    /// <summary>Bit i set = achievement i unlocked.</summary>
    public struct Achievements : IComponentData
    {
        public ulong Unlocked;

        public bool Has(int index) => (Unlocked & (1UL << index)) != 0;
    }
}
