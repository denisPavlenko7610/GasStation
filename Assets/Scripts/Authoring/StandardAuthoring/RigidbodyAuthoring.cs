using GasStation.Components.StandardComponents;
using RDTools.AutoAttach;
using Unity.Entities;
using UnityEngine;

namespace GasStation.Authoring.StandardAuthoring
{
    public class RigidbodyAuthoring : MonoBehaviour
    {
        //[field: SerializeField, Attach] public Rigidbody Rigidbody { get; private set; }
    }

    // public class RigidbodyBaker : Baker<RigidbodyAuthoring>
    // {
    //     public override void Bake(RigidbodyAuthoring authoring)
    //     {
    //         AddComponent(new RigidbodyComponent
    //         {
    //             Value = authoring.Rigidbody
    //         });
    //     }
    // }
}