using GasStation.Bridge;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// Makes the weather visible: the sandstorm (sandy fog, a dimmer sun, dust blowing past the camera), rain
    /// (grey haze and falling drops) and snow (white haze and flakes). Fades in and out; restores the scene's
    /// fog settings afterwards.
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
        private ParticleSystem _rain;
        private ParticleSystem _snow;
        private float _rainAmount;
        private float _snowAmount;

        private static readonly Color RainColor = new(0.55f, 0.6f, 0.68f);
        private static readonly Color SnowColor = new(0.9f, 0.92f, 0.96f);

        private void Update()
        {
            bool sandstorm = HudModel.HasStation && HudModel.World.Active == WorldEventKind.Sandstorm;
            float target = sandstorm ? 1f : 0f;
            Storm = Mathf.MoveTowards(Storm, target, FadeSpeed * Time.deltaTime);

            var weather = HudModel.HasStation ? HudModel.Season.Weather : WeatherKind.Clear;
            _rainAmount = Mathf.MoveTowards(_rainAmount, weather == WeatherKind.Rain ? 1f : 0f, FadeSpeed * Time.deltaTime);
            _snowAmount = Mathf.MoveTowards(_snowAmount, weather == WeatherKind.Snow ? 1f : 0f, FadeSpeed * Time.deltaTime);
            SunMultiplier = Mathf.Lerp(1f, 0.45f, Storm) * Mathf.Lerp(1f, 0.7f, Mathf.Max(_rainAmount, _snowAmount));

            float haze = Mathf.Max(Storm, Mathf.Max(_rainAmount, _snowAmount) * 0.5f);
            if (haze > 0.001f)
            {
                SaveFog();
                // The sandstorm wins over rain and snow.
                var color = Storm > 0.001f ? SandColor : _snowAmount > _rainAmount ? SnowColor : RainColor;
                float density = Storm > 0.001f ? 0.035f : 0.015f;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(_fogColor, color, haze);
                RenderSettings.fogDensity = Mathf.Lerp(_fogDensity, density, Storm > 0.001f ? Storm : haze * 2f);
            }
            else if (_savedFog)
            {
                RestoreFog();
            }

            UpdateDust();
            _rain = UpdateFalling(_rain, _rainAmount, "Rain", RainColor, 18f, 0.06f, 800f);
            _snow = UpdateFalling(_snow, _snowAmount, "Snow", SnowColor, 2.5f, 0.15f, 500f);
        }

        /// <summary>Rain or snow falling in a box around the camera.</summary>
        private ParticleSystem UpdateFalling(ParticleSystem particles, float amount, string name, Color color, float speed, float size, float rate)
        {
            var camera = Camera.main;
            if (camera == null)
                return particles;

            if (particles == null)
            {
                if (amount <= 0.001f)
                    return null;

                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                particles = go.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.startLifetime = 20f / speed;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
                main.startColor = new Color(color.r, color.g, color.b, 0.7f);
                main.maxParticles = 3000;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(50f, 1f, 40f);

                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                velocity.y = new ParticleSystem.MinMaxCurve(-speed * 1.1f, -speed * 0.9f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                if (shader != null)
                    renderer.sharedMaterial = new Material(shader) { color = new Color(color.r, color.g, color.b, 0.7f) };
            }

            // Falls from above the area the camera looks at.
            var look = camera.transform.position + camera.transform.forward * 12f;
            particles.transform.position = new Vector3(look.x, 18f, look.z);
            var emission = particles.emission;
            emission.rateOverTime = rate * amount;
            return particles;
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
