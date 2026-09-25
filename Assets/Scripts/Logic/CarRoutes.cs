using GasStation.Components;
using Unity.Entities;

namespace GasStation.Logic
{
    public static class CarRoutes
    {
        public static void SendToExit(ref Car car, DynamicBuffer<PathPoint> path, DynamicBuffer<ExitRoutePoint> exitRoute)
        {
            car.State = CarState.Leaving;
            car.Pump = Entity.Null;
            path.Clear();
            for (int i = 0; i < exitRoute.Length; i++)
                path.Add(new PathPoint { Position = exitRoute[i].Position });
        }
    }
}
