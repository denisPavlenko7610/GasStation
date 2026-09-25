using GasStation.Bridge;
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

            if (PhotoMode.Active)
            {
                // Orbit around the focus at the usual camera height and distance, turned by the photo yaw.
                var orbit = Quaternion.Euler(0f, PhotoMode.Yaw, 0f) * CameraSingleton.Offset * PhotoMode.Zoom;
                camera.transform.position = PhotoMode.Focus + orbit;
                camera.transform.rotation = Quaternion.LookRotation(PhotoMode.Focus - camera.transform.position);
                return;
            }

            if (BuildMode.Active)
            {
                camera.transform.position = BuildMode.Focus + CameraSingleton.Offset * BuildMode.Zoom;
                return;
            }

            var player = SystemAPI.GetSingletonEntity<PlayerTag>();
            Vector3 position = SystemAPI.GetComponent<LocalToWorld>(player).Position;
            camera.transform.position = position + CameraSingleton.Offset;
        }
    }
}
