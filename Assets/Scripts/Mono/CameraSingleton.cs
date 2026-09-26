using GasStation.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Mono
{
    /// <summary>
    /// Camera state shared with the ECS follow system. The game is played first-person: Update() reads mouse look
    /// into Yaw/Pitch and locks the cursor while walking; menus, the laptop, build and photo mode free it.
    /// Offset is the overhead view that build and photo mode orbit around the player's position.
    /// </summary>
    public class CameraSingleton : MonoBehaviour
    {
        [SerializeField] private float followDistance = 12f;
        [SerializeField] private float lookDegreesPerPixel = 0.12f;
        [SerializeField] private float maxPitch = 80f;

        /// <summary>Eye height above the player entity's origin (it sits 1 m above the ground).</summary>
        public const float EyeHeight = 0.65f;

        public static Camera Instance { get; private set; }
        public static Vector3 Offset { get; private set; }
        /// <summary>Where the player stands; build and photo mode start from here.</summary>
        public static Vector3 PlayerPosition { get; set; }
        public static float Yaw { get; private set; }
        public static float Pitch { get; private set; } = 12f;

        /// <summary>True while the mouse steers the view (cursor locked).</summary>
        public static bool Looking { get; private set; }

        private void Awake()
        {
            Instance = Camera.main;
            if (Instance != null)
                Instance.nearClipPlane = 0.05f;
            Offset = new Vector3(0f, transform.position.y, -followDistance);
        }

        private void Update()
        {
            // Build mode, photo mode, menus and the laptop need the pointer; don't fight their input.
            bool free = BuildMode.Active || PhotoMode.Active || GamePause.MenuOpen || LaptopState.IsOpen ||
                        !HudModel.HasStation || !Application.isFocused;
            SetLooking(!free);
            if (!Looking)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 delta = mouse.delta.ReadValue() * lookDegreesPerPixel * GameSettings.MouseSensitivity;
            Yaw = Mathf.Repeat(Yaw + delta.x, 360f);
            Pitch = Mathf.Clamp(Pitch - (GameSettings.InvertY ? -delta.y : delta.y), -maxPitch, maxPitch);
        }

        private void OnDisable() => SetLooking(false);

        private static void SetLooking(bool looking)
        {
            if (Looking == looking)
                return;

            Looking = looking;
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !looking;
        }
    }
}
