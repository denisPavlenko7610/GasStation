using UnityEngine;

namespace GasStation.Bridge
{
    /// <summary>Photo mode (F12): the interface hides and the camera flies free around a focus point.</summary>
    public static class PhotoMode
    {
        public static bool Active { get; private set; }
        public static Vector3 Focus;
        /// <summary>Degrees around the focus.</summary>
        public static float Yaw;
        public static float Zoom = 1f;
        /// <summary>The camera rotation before photo mode, restored on exit.</summary>
        public static Quaternion SavedRotation;

        public static void Enter(Vector3 focus, Quaternion cameraRotation)
        {
            Active = true;
            Focus = new Vector3(focus.x, 0f, focus.z);
            Yaw = 0f;
            Zoom = 1f;
            SavedRotation = cameraRotation;
        }

        public static void Exit() => Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Active = false;
    }
}
