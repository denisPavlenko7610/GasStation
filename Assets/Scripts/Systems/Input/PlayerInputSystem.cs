using GasStation.Bridge;
using GasStation.Components;
using GasStation.Mono;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Systems
{
    [UpdateInGroup(typeof(StationSystemGroup), OrderFirst = true)]
    public partial class PlayerInputSystem : SystemBase
    {
        private PlayerInputAction _inputAction;

        protected override void OnCreate()
        {
            _inputAction = new PlayerInputAction();
            _inputAction.Enable();
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnDestroy()
        {
            _inputAction?.Disable();
            _inputAction?.Dispose();
        }

        protected override void OnUpdate()
        {
            // Menus pause the game; their clicks must not reach the station.
            // Build mode uses WASD for the camera and clicks for placing props.
            bool menuOpen = GamePause.MenuOpen || BuildMode.Active || PhotoMode.Active;
            Vector2 move = menuOpen ? Vector2.zero : _inputAction.Player.Move.ReadValue<Vector2>();
            bool interact = !menuOpen && (_inputAction.Player.Fire.WasPressedThisFrame()
                                          || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame));
            bool interactHeld = !menuOpen && (_inputAction.Player.Fire.IsPressed()
                                              || (Keyboard.current != null && Keyboard.current.eKey.isPressed));

            // WASD walks relative to where the owner looks.
            var yaw = Quaternion.Euler(0f, CameraSingleton.Yaw, 0f);
            Vector3 walk = yaw * new Vector3(move.x, 0f, move.y);
            Vector3 look = Quaternion.Euler(CameraSingleton.Pitch, CameraSingleton.Yaw, 0f) * Vector3.forward;

            foreach (var (moveInput, interaction) in SystemAPI
                         .Query<RefRW<MoveInput>, RefRW<PlayerInteraction>>()
                         .WithAll<PlayerTag>())
            {
                moveInput.ValueRW.Value = walk;
                interaction.ValueRW.LookDirection = look;
                interaction.ValueRW.InteractPressed = interact;
                interaction.ValueRW.InteractHeld = interactHeld;
            }
        }
    }
}
