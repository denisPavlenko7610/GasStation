using GasStation.Components.PlayerComponents;
using GasStation.MonoBehaviours;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GasStation.Systems.CameraSystems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class CameraSystem : SystemBase
    {
        private Entity _target;
        private Random _random;
        private EntityQuery _query;

        protected override void OnCreate()
        {
            _random = Random.CreateFromIndex(1234);
            _query = GetEntityQuery(typeof(PlayerTag));
            RequireForUpdate(_query);
        }
        
        protected override void OnUpdate()
        {
            if ( _target == Entity.Null)
            {
                var players = _query.ToEntityArray(Allocator.Temp);
                _target = players[_random.NextInt(players.Length)];
            }
            
            var playerTransform = GetComponent<LocalToWorld>(_target);
            var camera = CameraSingleton.Camerainstance;
            var offset = CameraSingleton.CameraOffset;
            var position = new Vector3(playerTransform.Position.x, playerTransform.Position.y + offset.y,
                playerTransform.Position.z - 5f);
            camera.transform.position = position;
        }
    }
}