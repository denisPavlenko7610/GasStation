using GasStation.Components.MovementComponents;
using GasStation.Components.PlayerComponents;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Systems.InputSystems
{
    public partial class PlayerInputSystem : SystemBase
    {
        private PlayerInputAction _inputAction;
        private Vector3 _moveVector;

        protected override void OnCreate()
        {
            _inputAction = new PlayerInputAction();
            _inputAction.Enable();
            _inputAction.Player.Move.performed += UpdateInput;
            _inputAction.Player.Move.canceled += CanceledInput;
        }

        protected override void OnDestroy()
        {
            _inputAction?.Disable();
            if (_inputAction != null) _inputAction.Player.Move.performed -= UpdateInput;
            if (_inputAction != null) _inputAction.Player.Move.canceled -= CanceledInput;
        }

        protected override void OnUpdate()
        {
            UpdateTargetPosition();
        }

        private void UpdateInput(InputAction.CallbackContext ctx)
        {
            _moveVector = _inputAction.Player.Move.ReadValue<Vector2>();
        }

        private void CanceledInput(InputAction.CallbackContext ctx)
        {
            _moveVector = Vector3.zero;
        }
        
        private void UpdateTargetPosition()
        {
            foreach (var targetPositionComponent in SystemAPI.Query<RefRW<TargetPositionComponent>>()
                         .WithAll<PlayerTag>())
            {
                targetPositionComponent.ValueRW.Value = new Vector3(_moveVector.x, 0, _moveVector.y);
            }
        }
    }
}