using GasStation.Components;
using UnityEngine;

namespace GasStation.Bridge
{
    /// <summary>
    /// Build mode state shared by the build controller, the HUD, player input and the camera. While it is on
    /// the player stands still, WASD moves the camera and the mouse places props.
    /// </summary>
    public static class BuildMode
    {
        public const float MinZoom = 0.6f;
        public const float MaxZoom = 1.8f;

        public static bool Active { get; private set; }
        public static PropType Selected = PropType.TrashBin;
        public static float Yaw;
        /// <summary>Point on the ground the camera looks at.</summary>
        public static Vector3 Focus;
        public static float Zoom = 1.2f;

        public static void Enter(Vector3 focus)
        {
            Active = true;
            Focus = new Vector3(focus.x, 0f, focus.z);
        }

        public static void Exit() => Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Active = false;
            Selected = PropType.TrashBin;
            Yaw = 0f;
            Zoom = 1.2f;
        }
    }
}
