using GasStation.Components;
using GasStation.Mono;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace GasStation.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class CameraFollowSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            var camera = CameraSingleton.Instance;
            if (camera == null)
                return;

            var player = SystemAPI.GetSingletonEntity<PlayerTag>();
            Vector3 position = SystemAPI.GetComponent<LocalToWorld>(player).Position;
            camera.transform.position = position + CameraSingleton.Offset;
        }
    }
}
