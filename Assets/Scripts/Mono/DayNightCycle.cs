using GasStation.Bridge;
using GasStation.Logic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GasStation.Mono
{
    /// <summary>
    /// Rotates the directional light according to the in-game hour. At night a second, dim and cold "moon" light
    /// lights the lot from above (the sun stays below the horizon so the sky remains dark), and the ambient light
    /// and reflections fade so the night actually reads as night.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private float maxIntensity = 1.35f;
        [SerializeField] private float nightIntensity = 0.08f;
        [SerializeField] private float moonIntensity = 0.2f;
        [SerializeField] private Color moonColor = new(0.55f, 0.65f, 1f);
        [SerializeField, Range(0f, 1f)] private float nightAmbient = 0.2f;
        [SerializeField, Range(0f, 1f)] private float nightReflections = 0.25f;

        private Light _light;
        private Light _moon;
        private float _yaw;
        private ReflectionProbe _probe;
        private int _lastProbeHour = -1;
        private SphericalHarmonicsL2 _dayAmbient;
        private Color _daySky, _dayEquator, _dayGround;
        private float _dayReflections;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _yaw = transform.eulerAngles.y;
            _dayAmbient = RenderSettings.ambientProbe;
            _daySky = RenderSettings.ambientSkyColor;
            _dayEquator = RenderSettings.ambientEquatorColor;
            _dayGround = RenderSettings.ambientGroundColor;
            _dayReflections = RenderSettings.reflectionIntensity;
        }

        private void OnDestroy()
        {
            if (_moon != null)
                Destroy(_moon.gameObject);
            RenderSettings.ambientProbe = _dayAmbient;
            RenderSettings.ambientSkyColor = _daySky;
            RenderSettings.ambientEquatorColor = _dayEquator;
            RenderSettings.ambientGroundColor = _dayGround;
            RenderSettings.reflectionIntensity = _dayReflections;
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

            float night = LightingMath.NightFactor(HudModel.Hour);
            var moon = Moon();
            moon.intensity = moonIntensity * night;
            moon.enabled = night > 0.01f;

            // Gradient (trilight) ambient is rebuilt from its colors, a skybox one lives in the probe: dim both.
            var ambient = Color.Lerp(Color.white, moonColor * nightAmbient, night);
            RenderSettings.ambientSkyColor = _daySky * ambient;
            RenderSettings.ambientEquatorColor = _dayEquator * ambient;
            RenderSettings.ambientGroundColor = _dayGround * ambient;
            if (RenderSettings.ambientMode == AmbientMode.Skybox)
                RenderSettings.ambientProbe = _dayAmbient * Mathf.Lerp(1f, nightAmbient, night);
            RenderSettings.reflectionIntensity = _dayReflections * Mathf.Lerp(1f, nightReflections, night);

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

        private Light Moon()
        {
            if (_moon != null)
                return _moon;

            var go = new GameObject("Moon");
            go.transform.rotation = Quaternion.Euler(55f, _yaw + 150f, 0f);
            _moon = go.AddComponent<Light>();
            _moon.type = LightType.Directional;
            _moon.color = moonColor;
            _moon.shadows = LightShadows.None;
            return _moon;
        }
    }
}
