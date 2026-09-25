using GasStation.Components;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>EV chargers: the spots where electric cars park to charge. Opened two at a time by the EvCharger upgrade.</summary>
    public class ChargingStationAuthoring : MonoBehaviour
    {
        public Transform[] spots;
        public float pricePerKwh = EvMath.PricePerKwh;
        public uint seed = 23;
    }

    public class ChargingStationBaker : Baker<ChargingStationAuthoring>
    {
        public override void Bake(ChargingStationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ChargingStation
            {
                PricePerKwh = authoring.pricePerKwh,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var spots = AddBuffer<ChargerSpot>(entity);
            if (authoring.spots == null)
                return;

            foreach (var spot in authoring.spots)
            {
                if (spot == null)
                    continue;
                DependsOn(spot);
                spots.Add(new ChargerSpot { Position = spot.position, Rotation = spot.rotation, Occupant = Entity.Null });
            }
        }
    }

    /// <summary>The diner grill next to the shop. The cook stands here; the player cooks here with E.</summary>
    public class DinerAuthoring : MonoBehaviour
    {
        public int startIngredients = 10;
        public uint seed = 29;
    }

    public class DinerBaker : Baker<DinerAuthoring>
    {
        public override void Bake(DinerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Diner
            {
                Grill = (float3)authoring.transform.position,
                Ingredients = authoring.startIngredients,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var counter = AddBuffer<DinerCounter>(entity);
            for (int i = 0; i < DinerDishes.Count; i++)
                counter.Add(new DinerCounter { Price = DinerMath.Price((DinerDish)i) });
        }
    }
}
