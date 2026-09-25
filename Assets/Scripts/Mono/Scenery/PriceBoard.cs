using System.Text;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Logic;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>Roadside price board with a 3D text: our prices, or the competitor's across the road.</summary>
    public class PriceBoard : MonoBehaviour
    {
        public bool competitor;
        public Color color = new(1f, 0.55f, 0.15f);
        public float characterSize = 0.1f;

        private const float RefreshInterval = 0.5f;

        private TextMesh _text;
        private float _nextRefresh;
        private readonly StringBuilder _builder = new();

        private void Awake()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Prices");
            go.transform.SetParent(transform, false);

            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = 64;
            _text.characterSize = characterSize;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Left;
            _text.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void Update()
        {
            if (!HudModel.HasStation || Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + RefreshInterval;
            var rival = HudModel.Competitor;
            bool showRival = competitor && !rival.BoughtOut;

            _builder.Clear();
            _builder.AppendLine(showRival ? "PetroMax" : StationProfile.DisplayName);
            for (int i = 0; i < FuelTypes.Count; i++)
            {
                var fuel = (FuelType)i;
                float price = showRival ? rival.Price(fuel) : HudModel.Fuel[i].SellPrice;
                if (showRival && rival.Promo == CompetitorPromo.Discount)
                    price *= CompetitionMath.DiscountShare;
                _builder.Append(ShortName(fuel)).Append("  ").Append(price.ToString("0.00"));
                if (i < FuelTypes.Count - 1)
                    _builder.AppendLine();
            }

            _text.text = _builder.ToString();
        }

        private static string ShortName(FuelType fuel) => fuel switch
        {
            FuelType.Petrol92 => "92 ",
            FuelType.Petrol95 => "95 ",
            _ => "DSL"
        };
    }
}
