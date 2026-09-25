namespace GasStation.Components
{
    public enum FuelType : byte
    {
        Petrol92 = 0,
        Petrol95 = 1,
        Diesel = 2
    }

    public static class FuelTypes
    {
        public const int Count = 3;
    }
}
