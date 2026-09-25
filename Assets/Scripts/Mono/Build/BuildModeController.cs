using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Logic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Mono.Build
{
    /// <summary>
    /// Build mode (B): WASD moves the camera, the wheel zooms, 1–8 pick a prop, Q/E rotate it, left click
    /// places it and right click removes the prop under the cursor (half the price back). A ghost shows where
    /// the prop goes: green when it fits, red when it does not.
    /// </summary>
    public class BuildModeController : MonoBehaviour
    {
        private const float PanSpeed = 14f;
        private const float RotateStep = 45f;
        private static readonly Color Valid = new(0.3f, 0.9f, 0.35f);
        private static readonly Color Invalid = new(0.95f, 0.25f, 0.2f);

        private GameObject _ghost;
        private PropType _ghostType;
        private bool _ghostValid;
        private readonly List<float2> _others = new();

        /// <summary>Why the prop under the cursor cannot be placed; None when it can.</summary>
        public static PlacementError CurrentError { get; private set; }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !HudModel.HasStation || !HudModel.HasBuildArea || GamePause.MenuOpen)
            {
                SetGhostVisible(false);
                return;
            }

            if (keyboard.bKey.wasPressedThisFrame)
                Toggle();

            if (!BuildMode.Active)
            {
                SetGhostVisible(false);
                return;
            }

            HandleCamera(keyboard);
            HandleSelection(keyboard);

            var camera = CameraSingleton.Instance;
            var mouse = Mouse.current;
            if (camera == null || mouse == null)
                return;

            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
            {
                SetGhostVisible(false);
                return;
            }

            float3 point = PropMath.Snap((float3)ray.GetPoint(distance));
            UpdateGhost(point);

            if (mouse.leftButton.wasPressedThisFrame)
                StationCommands.PlaceProp(BuildMode.Selected, point, BuildMode.Yaw);
            if (mouse.rightButton.wasPressedThisFrame)
                StationCommands.RemoveProp(point);
        }

        private static void Toggle()
        {
            if (BuildMode.Active)
            {
                BuildMode.Exit();
                return;
            }

            var camera = CameraSingleton.Instance;
            var focus = camera != null ? camera.transform.position - CameraSingleton.Offset : Vector3.zero;
            BuildMode.Enter(focus);
        }

        private static void HandleCamera(Keyboard keyboard)
        {
            var move = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.z -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;

            var focus = BuildMode.Focus + move.normalized * (PanSpeed * BuildMode.Zoom * Time.unscaledDeltaTime);
            var area = HudModel.BuildArea;
            focus.x = Mathf.Clamp(focus.x, area.Min.x - 10f, area.Max.x + 10f);
            focus.z = Mathf.Clamp(focus.z, area.Min.y - 15f, area.Max.y + 5f);
            BuildMode.Focus = focus;

            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f)
                BuildMode.Zoom = Mathf.Clamp(BuildMode.Zoom - Mathf.Sign(scroll) * 0.1f, BuildMode.MinZoom, BuildMode.MaxZoom);
        }

        private static void HandleSelection(Keyboard keyboard)
        {
            for (int i = 0; i < PropTypes.Count; i++)
            {
                if (DigitPressed(keyboard, i + 1))
                    BuildMode.Selected = (PropType)i;
            }

            if (keyboard.qKey.wasPressedThisFrame)
                BuildMode.Yaw = Mathf.Repeat(BuildMode.Yaw - RotateStep, 360f);
            if (keyboard.eKey.wasPressedThisFrame)
                BuildMode.Yaw = Mathf.Repeat(BuildMode.Yaw + RotateStep, 360f);
        }

        private void UpdateGhost(float3 point)
        {
            if (_ghost == null || _ghostType != BuildMode.Selected)
            {
                if (_ghost != null)
                    Destroy(_ghost);
                _ghostType = BuildMode.Selected;
                _ghost = PropVisuals.Create(_ghostType, transform);
                _ghost.name = "BuildGhost";
                _ghostValid = !_ghostValid; // force a tint below
            }

            SetGhostVisible(true);
            _ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, BuildMode.Yaw, 0f));

            _others.Clear();
            foreach (var prop in HudModel.Props)
                _others.Add(prop.Position.xz);

            CurrentError = PropMath.Check(BuildMode.Selected, point.xz, HudModel.BuildArea, HudModel.NoBuildZones, _others,
                HudModel.Level.Level, HudModel.Economy.Money);
            bool valid = CurrentError == PlacementError.None;
            if (valid == _ghostValid)
                return;

            _ghostValid = valid;
            PropVisuals.Tint(_ghost, valid ? Valid : Invalid);
        }

        private void SetGhostVisible(bool visible)
        {
            if (_ghost != null && _ghost.activeSelf != visible)
                _ghost.SetActive(visible);
        }

        private static bool DigitPressed(Keyboard keyboard, int digit) => digit switch
        {
            1 => keyboard.digit1Key.wasPressedThisFrame,
            2 => keyboard.digit2Key.wasPressedThisFrame,
            3 => keyboard.digit3Key.wasPressedThisFrame,
            4 => keyboard.digit4Key.wasPressedThisFrame,
            5 => keyboard.digit5Key.wasPressedThisFrame,
            6 => keyboard.digit6Key.wasPressedThisFrame,
            7 => keyboard.digit7Key.wasPressedThisFrame,
            8 => keyboard.digit8Key.wasPressedThisFrame,
            _ => false
        };
    }
}
