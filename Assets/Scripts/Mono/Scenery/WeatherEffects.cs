using GasStation.Bridge;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// Makes the sandstorm event visible: sandy fog, a dimmer sun and dust blowing past the camera.
    /// Fades in and out; restores the scene's fog settings afterwards.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        private const float FadeSpeed = 0.35f;
        private static readonly Color SandColor = new(0.78f, 0.62f, 0.42f);

        /// <summary>Multiplier for the sun intensity, read by DayNightCycle.</summary>
        public static float SunMultiplier { get; private set; } = 1f;

        /// <summary>0..1 strength of the current storm, used by audio.</summary>
        public static float Storm { get; private set; }

        private bool _fog;
        private Color _fogColor;
        private float _fogDensity;
        private FogMode _fogMode;
        private bool _savedFog;
        private ParticleSystem _dust;

        private void Update()
        {
            bool sandstorm = HudModel.HasStation && HudModel.World.Active == WorldEventKind.Sandstorm;
            float target = sandstorm ? 1f : 0f;
            Storm = Mathf.MoveTowards(Storm, target, FadeSpeed * Time.deltaTime);
            SunMultiplier = Mathf.Lerp(1f, 0.45f, Storm);

            if (Storm > 0.001f)
            {
                SaveFog();
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(_fogColor, SandColor, Storm);
                RenderSettings.fogDensity = Mathf.Lerp(_fogDensity, 0.035f, Storm);
            }
            else if (_savedFog)
            {
                RestoreFog();
            }

            UpdateDust();
        }

        private void SaveFog()
        {
            if (_savedFog)
                return;

            _savedFog = true;
            _fog = RenderSettings.fog;
            _fogColor = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            _fogMode = RenderSettings.fogMode;
        }

        private void RestoreFog()
        {
            _savedFog = false;
            RenderSettings.fog = _fog;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogMode = _fogMode;
        }

        private void UpdateDust()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            if (_dust == null)
            {
                if (Storm <= 0.001f)
                    return;
                _dust = CreateDust();
            }

            _dust.transform.position = camera.transform.position + camera.transform.forward * 10f;
            var emission = _dust.emission;
            emission.rateOverTime = 400f * Storm;
        }

        private ParticleSystem CreateDust()
        {
            var go = new GameObject("Sandstorm Dust");
            go.transform.SetParent(transform, false);
            var dust = go.AddComponent<ParticleSystem>();

            var main = dust.main;
            main.startLifetime = 2.5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startColor = new Color(SandColor.r, SandColor.g, SandColor.b, 0.6f);
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 12f, 30f);

            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(12f, 18f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null)
                renderer.sharedMaterial = new Material(shader) { color = new Color(SandColor.r, SandColor.g, SandColor.b, 0.6f) };

            return dust;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SunMultiplier = 1f;
            Storm = 0f;
        }
    }
}
