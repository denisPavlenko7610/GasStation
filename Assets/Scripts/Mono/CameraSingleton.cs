using GasStation.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Mono
{
    /// <summary>
    /// Camera state shared with the ECS follow system. Update() reads the zoom wheel and the
    /// right-mouse orbit drag and eases the offset; CameraFollowSystem applies it to the player.
    /// </summary>
    public class CameraSingleton : MonoBehaviour
    {
        [SerializeField] private float followDistance = 12f;
        [SerializeField] private float orbitDegreesPerPixel = 0.4f;
        [SerializeField] private float zoomPerWheelStep = 0.12f;
        [SerializeField] private float minZoom = 0.45f;
        [SerializeField] private float maxZoom = 2.4f;
        [SerializeField] private float easing = 8f;

        public static Camera Instance { get; private set; }
        public static Vector3 Offset { get; private set; }

        private float _height;
        private float _yaw;
        private float _zoom = 1f;
        private float _zoomTarget = 1f;

        private void Awake()
        {
            Instance = Camera.main;
            _height = transform.position.y;
            RebuildOffset();
        }

        private void Update()
        {
            // Build mode, photo mode and menus drive the camera themselves; don't fight their input.
            if (BuildMode.Active || PhotoMode.Active || GamePause.MenuOpen || LaptopState.IsOpen)
                return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                    _zoomTarget = Mathf.Clamp(_zoomTarget * Mathf.Exp(-scroll * zoomPerWheelStep * 0.01f), minZoom, maxZoom);

                if (mouse.rightButton.isPressed)
                {
                    _yaw = Mathf.Repeat(_yaw + mouse.delta.x.ReadValue() * orbitDegreesPerPixel, 360f);
                    RebuildOffset();
                }
            }

            if (!Mathf.Approximately(_zoom, _zoomTarget))
            {
                _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-easing * Time.deltaTime));
                RebuildOffset();
            }
        }

        private void RebuildOffset()
        {
            Offset = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(0f, _height, -followDistance) * _zoom;
        }
    }
}
