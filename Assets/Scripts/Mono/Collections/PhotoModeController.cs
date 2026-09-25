using System;
using System.IO;
using GasStation.Bridge;
using GasStation.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Mono.Collections
{
    /// <summary>
    /// Photo mode (F12): the HUD hides, WASD moves the camera, the wheel zooms, Q/E orbit, Space saves a
    /// screenshot to the Screenshots folder next to the save. F12 or Esc (handled by MenuController) returns to the game.
    /// </summary>
    public class PhotoModeController : MonoBehaviour
    {
        private const float PanSpeed = 12f;
        private const float OrbitSpeed = 70f;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !HudModel.HasStation)
                return;

            if (!PhotoMode.Active)
            {
                if (keyboard.f12Key.wasPressedThisFrame && !GamePause.MenuOpen && !BuildMode.Active && !LaptopState.IsOpen)
                    Enter();
                return;
            }

            if (keyboard.f12Key.wasPressedThisFrame)
            {
                ExitPhotoMode();
                return;
            }

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move.z += 1f;
            if (keyboard.sKey.isPressed) move.z -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed) move.x -= 1f;
            // Move relative to where the camera looks.
            PhotoMode.Focus += Quaternion.Euler(0f, PhotoMode.Yaw, 0f) * move.normalized * (PanSpeed * PhotoMode.Zoom * Time.unscaledDeltaTime);

            if (keyboard.qKey.isPressed) PhotoMode.Yaw -= OrbitSpeed * Time.unscaledDeltaTime;
            if (keyboard.eKey.isPressed) PhotoMode.Yaw += OrbitSpeed * Time.unscaledDeltaTime;

            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f)
                PhotoMode.Zoom = Mathf.Clamp(PhotoMode.Zoom - Mathf.Sign(scroll) * 0.1f, 0.35f, 2.5f);

            if (keyboard.spaceKey.wasPressedThisFrame)
                TakeScreenshot();
        }

        private static void Enter()
        {
            var camera = CameraSingleton.Instance;
            var focus = camera != null ? camera.transform.position - CameraSingleton.Offset : Vector3.zero;
            PhotoMode.Enter(focus, camera != null ? camera.transform.rotation : Quaternion.identity);
            HudModel.Notify(Loc.T("msg.photoMode"));
        }

        public static void ExitPhotoMode()
        {
            PhotoMode.Exit();
            var camera = CameraSingleton.Instance;
            if (camera != null)
                camera.transform.rotation = PhotoMode.SavedRotation;
        }

        private static void TakeScreenshot()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"station_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            HudModel.Notify(Loc.F("msg.screenshot", path));
        }
    }
}
