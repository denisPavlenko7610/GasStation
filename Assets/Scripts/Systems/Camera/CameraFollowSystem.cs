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
                // Overhead view looking down at the focus.
                camera.transform.SetPositionAndRotation(BuildMode.Focus + CameraSingleton.Offset * BuildMode.Zoom,
                    Quaternion.LookRotation(-CameraSingleton.Offset));
                return;
            }

            var player = SystemAPI.GetSingletonEntity<PlayerTag>();
            Vector3 position = SystemAPI.GetComponent<LocalToWorld>(player).Position;
            CameraSingleton.PlayerPosition = position;

            // First person: the camera is the owner's eyes.
            camera.transform.SetPositionAndRotation(position + Vector3.up * CameraSingleton.EyeHeight,
                Quaternion.Euler(CameraSingleton.Pitch, CameraSingleton.Yaw, 0f));
        }
    }
}
