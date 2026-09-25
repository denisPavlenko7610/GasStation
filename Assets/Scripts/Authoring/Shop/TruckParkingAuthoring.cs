using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Overnight parking for truckers. Each spot is a transform (the truck faces its forward); the
    /// TruckParking upgrade opens them two at a time, in order.
    /// </summary>
    public class TruckParkingAuthoring : MonoBehaviour
    {
        public Transform entry;
        public Transform[] spots;
        public float feePerNight = 20f;
        public uint seed = 13;
    }

    public class TruckParkingBaker : Baker<TruckParkingAuthoring>
    {
        public override void Bake(TruckParkingAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var entry = authoring.entry != null ? authoring.entry : GetComponent<Transform>();
            DependsOn(entry);

            AddComponent(entity, new TruckParking
            {
                FeePerNight = authoring.feePerNight,
                Entry = entry.position,
                Random = Unity.Mathematics.Random.CreateFromIndex(authoring.seed)
            });

            var spots = AddBuffer<ParkingSpot>(entity);
            if (authoring.spots == null)
                return;

            foreach (var spot in authoring.spots)
            {
                if (spot == null)
                    continue;
                DependsOn(spot);
                spots.Add(new ParkingSpot { Position = spot.position, Rotation = spot.rotation, Occupant = Entity.Null });
            }
        }
    }
}
