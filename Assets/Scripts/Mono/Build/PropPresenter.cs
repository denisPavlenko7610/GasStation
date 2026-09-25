using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Mono.Scenery;
using UnityEngine;

namespace GasStation.Mono.Build
{
    /// <summary>Keeps one GameObject per placed prop (HudModel.Props); lamps get a night light.</summary>
    public class PropPresenter : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _objects = new();
        private readonly HashSet<int> _seen = new();
        private readonly List<int> _removed = new();

        private void LateUpdate()
        {
            if (!HudModel.HasStation)
                return;

            _seen.Clear();
            foreach (var prop in HudModel.Props)
            {
                _seen.Add(prop.Id);
                if (_objects.ContainsKey(prop.Id))
                    continue;

                var go = PropVisuals.Create(prop.Type, transform);
                go.transform.SetPositionAndRotation(prop.Position, Quaternion.Euler(0f, prop.Yaw, 0f));
                if (prop.Type == PropType.Lamp)
                    AddLight(go);
                _objects.Add(prop.Id, go);
            }

            _removed.Clear();
            foreach (var pair in _objects)
            {
                if (!_seen.Contains(pair.Key))
                    _removed.Add(pair.Key);
            }

            foreach (int id in _removed)
            {
                Destroy(_objects[id]);
                _objects.Remove(id);
            }
        }

        private static void AddLight(GameObject lamp)
        {
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, PropVisuals.LampHeight, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.shadows = LightShadows.None;
            lightGo.AddComponent<NightLight>();
        }
    }
}
