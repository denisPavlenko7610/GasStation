using GasStation.Bridge;
using GasStation.Logic;
using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>
    /// Tints the station buildings with the current paint scheme. Uses a MaterialPropertyBlock, so the
    /// shared materials of the asset pack stay untouched.
    /// </summary>
    public class StationPainter : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");

        [Tooltip("Walls, canopies, roofs")] public Renderer[] primary;
        [Tooltip("Pumps, trims, signs")] public Renderer[] accent;

        private MaterialPropertyBlock _block;
        private int _appliedScheme = -1;

        private void Awake() => _block = new MaterialPropertyBlock();

        private void Update()
        {
            if (!HudModel.HasStation || HudModel.PaintScheme == _appliedScheme)
                return;

            _appliedScheme = HudModel.PaintScheme;
            Apply(primary, StyleMath.Primary(_appliedScheme));
            Apply(accent, StyleMath.Accent(_appliedScheme));
        }

        private void Apply(Renderer[] renderers, Unity.Mathematics.float3 rgb)
        {
            if (renderers == null)
                return;

            var tint = new UnityEngine.Color(rgb.x, rgb.y, rgb.z, 1f);
            foreach (var target in renderers)
            {
                if (target == null)
                    continue;

                target.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, tint);
                _block.SetColor(Color, tint);
                target.SetPropertyBlock(_block);
            }
        }
    }
}
