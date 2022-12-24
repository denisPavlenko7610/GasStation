using GasStation.Components.RandomComponents;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GasStation.Authoring.RandomAuthoring
{
    public class RandomAuthoring : MonoBehaviour
    {
    }

    public class RandomBaker : Baker<RandomAuthoring>
    {
        public override void Bake(RandomAuthoring authoring)
        {
            AddComponent(new RandomComponent
            {
                Value = new Random(1)
            });
        }
    }
}