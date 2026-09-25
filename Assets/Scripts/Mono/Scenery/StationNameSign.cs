using GasStation.Bridge;
using GasStation.Localization;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>Writes the station name on the sign with a 3D text, facing the road.</summary>
    public class StationNameSign : MonoBehaviour
    {
        public Color color = new(1f, 0.85f, 0.3f);
        public float characterSize = 0.12f;

        private TextMesh _text;
        private int _version = -1;
        private GameLanguage _language;

        private void Awake()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("StationName");
            go.transform.SetParent(transform, false);

            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = 64;
            _text.characterSize = characterSize;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void Update()
        {
            if (_version == StationProfile.Version && _language == Loc.Language)
                return;

            _version = StationProfile.Version;
            _language = Loc.Language;
            _text.text = StationProfile.DisplayName;
        }
    }
}
