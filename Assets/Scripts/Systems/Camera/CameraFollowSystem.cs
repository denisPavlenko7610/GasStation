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
        private const float Ease = 8f;

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
            float dt = SystemAPI.Time.DeltaTime;

            // Ease toward the target instead of snapping, so zoom and orbit feel smooth.
            Vector3 desired = position + CameraSingleton.Offset;
            camera.transform.position = Vector3.Lerp(camera.transform.position, desired, 1f - Mathf.Exp(-Ease * dt));
            var look = position + Vector3.up * 1.2f;
            camera.transform.rotation = Quaternion.LookRotation((look - camera.transform.position).normalized);
        }
    }
}
