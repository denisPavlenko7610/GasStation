using GasStation.Components;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring
{
    /// <summary>
    /// Visible deliveries: a fuel tanker and a goods truck drive in along the car spawner's entry route,
    /// unload at these points and leave along its exit route. Without this component deliveries just arrive.
    /// </summary>
    public class DeliveryTrucksAuthoring : MonoBehaviour
    {
        public GameObject fuelTruckPrefab;
        public GameObject cargoTruckPrefab;
        public Transform fuelUnloadPoint;
        public Transform cargoUnloadPoint;
        public float speed = 7f;
        [Tooltip("Seconds the truck stands while unloading")] public float unloadTime = 6f;
        [Tooltip("The truck sets off when the delivery has this many seconds left")] public float leadTime = 12f;
    }

    public class DeliveryTrucksBaker : Baker<DeliveryTrucksAuthoring>
    {
        public override void Bake(DeliveryTrucksAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var self = GetComponent<Transform>();
            var fuel = authoring.fuelUnloadPoint != null ? authoring.fuelUnloadPoint : self;
            var cargo = authoring.cargoUnloadPoint != null ? authoring.cargoUnloadPoint : self;
            DependsOn(fuel);
            DependsOn(cargo);

            AddComponent(entity, new DeliveryTrucks
            {
                FuelTruckPrefab = authoring.fuelTruckPrefab != null ? GetEntity(authoring.fuelTruckPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                CargoTruckPrefab = authoring.cargoTruckPrefab != null ? GetEntity(authoring.cargoTruckPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                FuelUnload = fuel.position,
                FuelUnloadRotation = fuel.rotation,
                CargoUnload = cargo.position,
                CargoUnloadRotation = cargo.rotation,
                Speed = authoring.speed,
                UnloadTime = authoring.unloadTime,
                LeadTime = authoring.leadTime
            });
        }
    }
}
