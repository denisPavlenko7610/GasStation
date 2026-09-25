using GasStation.Bridge;
using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>Rotates the directional light according to the in-game hour.</summary>
    [RequireComponent(typeof(Light))]
    public class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private float maxIntensity = 1.35f;
        [SerializeField] private float nightIntensity = 0.08f;

        private Light _light;
        private float _yaw;
        private ReflectionProbe _probe;
        private int _lastProbeHour = -1;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _yaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            if (!HudModel.HasStation)
                return;

            // 6:00 — sunrise (0°), 12:00 — zenith (90°), 18:00 — sunset (180°).
            float elevation = (HudModel.Hour - 6f) / 24f * 360f;
            transform.rotation = Quaternion.Euler(elevation, _yaw, 0f);

            float daylight = Mathf.Clamp01(Mathf.Sin(elevation * Mathf.Deg2Rad));
            _light.intensity = Mathf.Lerp(nightIntensity, maxIntensity, daylight) * Scenery.WeatherEffects.SunMultiplier;

            // The sky and the station reflection change with the sun; rebake the probe hourly.
            if (_probe == null)
                _probe = FindAnyObjectByType<ReflectionProbe>();

            int hour = Mathf.FloorToInt(HudModel.Hour);
            if (_probe != null && hour != _lastProbeHour)
            {
                _lastProbeHour = hour;
                _probe.RenderProbe();
            }
        }
    }
}
