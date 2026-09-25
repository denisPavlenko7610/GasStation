using GasStation.Bridge;
using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>Rotates the directional light according to the in-game hour.</summary>
    [RequireComponent(typeof(Light))]
    public class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private float maxIntensity = 1.2f;
        [SerializeField] private float nightIntensity = 0.05f;

        private Light _light;
        private float _yaw;

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
            _light.intensity = Mathf.Lerp(nightIntensity, maxIntensity, daylight);
        }
    }
}
