using GasStation.Bridge;
using GasStation.Logic;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// Switches a light on at night. Lamps that are still broken stay dark until their renovation is done;
    /// neon lights take the neon colours of the "80s neon" paint scheme.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class NightLight : MonoBehaviour
    {
        private const int NeonScheme = 3;
        private static readonly Color NeonColor = new(0.2f, 0.95f, 0.95f);

        [Tooltip("Renovation that must be done for this light to work; -1 = always works")]
        public int renovationId = -1;
        [Tooltip("Use neon colours with the 80s neon paint scheme")]
        public bool neon;

        private Light _light;
        private float _intensity;
        private Color _color;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _intensity = _light.intensity;
            _color = _light.color;
        }

        private void Update()
        {
            if (!HudModel.HasStation)
                return;

            bool working = renovationId < 0 || HudModel.IsRenovated(renovationId);
            float night = working ? LightingMath.NightFactor(HudModel.Hour) : 0f;

            _light.enabled = night > 0.01f;
            _light.intensity = _intensity * night;
            _light.color = neon && HudModel.PaintScheme == NeonScheme ? NeonColor : _color;
        }
    }
}
