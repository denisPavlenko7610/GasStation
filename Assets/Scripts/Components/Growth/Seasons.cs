using Unity.Entities;

namespace GasStation.Components
{
    public enum SeasonKind : byte
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    public enum WeatherKind : byte
    {
        Clear = 0,
        Rain = 1,
        Heat = 2,
        Snow = 3
    }

    /// <summary>Season of the year and today's weather. Lives on the station entity.</summary>
    public struct SeasonState : IComponentData
    {
        public SeasonKind Season;
        public WeatherKind Weather;
        public Unity.Mathematics.Random Random;
    }

    /// <summary>License plates collected from customers (bit i = state i seen).</summary>
    public struct PlateCollection : IComponentData
    {
        public int Seen;
    }
}
