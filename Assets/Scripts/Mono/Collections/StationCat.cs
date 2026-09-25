using GasStation.Bridge;
using GasStation.Localization;
using GasStation.Mono.Scenery;
using UnityEngine;

namespace GasStation.Mono.Collections
{
    /// <summary>
    /// The station cat, built from primitives. It wanders around the lot, now and then climbs onto the roof of
    /// a waiting car for a nap, and shows its name when the player comes close. Named on the laptop.
    /// </summary>
    public class StationCat : MonoBehaviour
    {
        private const float WalkSpeed = 1.2f;
        private const float NameDistance = 4f;

        private enum Mood
        {
            Sitting,
            Walking,
            Napping
        }

        private GameObject _body;
        private TextMesh _tag;
        private Mood _mood = Mood.Sitting;
        private Vector3 _target;
        private float _timer = 2f;
        private int _nameVersion = -1;
        private GameLanguage _language;
        private bool _greetedToday;
        private int _greetedDay = -1;

        private void Start()
        {
            _body = new GameObject("Station Cat");
            _body.transform.SetParent(transform, false);
            var fur = new Color(0.9f, 0.55f, 0.2f);
            PrimitiveArt.Part(_body.transform, PrimitiveType.Capsule, new Vector3(0f, 0.22f, 0f), new Vector3(0.22f, 0.2f, 0.45f), fur, Quaternion.Euler(90f, 0f, 0f));
            PrimitiveArt.Part(_body.transform, PrimitiveType.Sphere, new Vector3(0f, 0.38f, 0.28f), new Vector3(0.2f, 0.18f, 0.18f), fur, Quaternion.identity);
            PrimitiveArt.Part(_body.transform, PrimitiveType.Cube, new Vector3(-0.06f, 0.49f, 0.3f), new Vector3(0.05f, 0.07f, 0.03f), fur, Quaternion.Euler(0f, 0f, 20f));
            PrimitiveArt.Part(_body.transform, PrimitiveType.Cube, new Vector3(0.06f, 0.49f, 0.3f), new Vector3(0.05f, 0.07f, 0.03f), fur, Quaternion.Euler(0f, 0f, -20f));
            PrimitiveArt.Part(_body.transform, PrimitiveType.Capsule, new Vector3(0f, 0.32f, -0.3f), new Vector3(0.05f, 0.18f, 0.05f), fur, Quaternion.Euler(-40f, 0f, 0f));

            var tagObject = new GameObject("CatName");
            tagObject.transform.SetParent(_body.transform, false);
            tagObject.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tag = tagObject.AddComponent<TextMesh>();
            _tag.font = font;
            _tag.fontSize = 48;
            _tag.characterSize = 0.04f;
            _tag.anchor = TextAnchor.MiddleCenter;
            _tag.color = new Color(1f, 0.85f, 0.5f);
            tagObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            _body.transform.position = HudModel.HasBuildArea ? RandomSpot() : new Vector3(6f, 0f, 14f);
            _body.SetActive(false);
        }

        private void Update()
        {
            if (_body == null)
                return;

            _body.SetActive(HudModel.HasStation);
            if (!HudModel.HasStation)
                return;

            _timer -= Time.deltaTime;
            switch (_mood)
            {
                case Mood.Sitting:
                case Mood.Napping:
                    if (_timer <= 0f)
                        Decide();
                    break;
                case Mood.Walking:
                    Walk();
                    break;
            }

            UpdateTag();
        }

        private void Decide()
        {
            // Naps on the roof of a waiting car, otherwise strolls to a random spot.
            if (Random.value < 0.3f && HudModel.Cards.Count > 0)
            {
                var car = HudModel.Cards[Random.Range(0, HudModel.Cards.Count)];
                _body.transform.position = car.Position + new Vector3(0f, 1.45f, 0f);
                _mood = Mood.Napping;
                _timer = Random.Range(10f, 25f);
                return;
            }

            if (_mood == Mood.Napping)
                _body.transform.position = new Vector3(_body.transform.position.x + 1.5f, 0f, _body.transform.position.z);

            _target = RandomSpot();
            _mood = Mood.Walking;
        }

        private void Walk()
        {
            var position = _body.transform.position;
            var toTarget = _target - position;
            toTarget.y = 0f;
            if (toTarget.magnitude < 0.1f)
            {
                _mood = Mood.Sitting;
                _timer = Random.Range(3f, 9f);
                return;
            }

            var step = toTarget.normalized * (WalkSpeed * Time.deltaTime);
            _body.transform.position = position + Vector3.ClampMagnitude(step, toTarget.magnitude);
            _body.transform.rotation = Quaternion.LookRotation(toTarget.normalized);
        }

        private void UpdateTag()
        {
            if (_nameVersion != StationProfile.Version || _language != Loc.Language)
            {
                _nameVersion = StationProfile.Version;
                _language = Loc.Language;
                _tag.text = StationProfile.CatName;
            }

            var offset = HudModel.PlayerPosition - _body.transform.position;
            offset.y = 0f;
            bool near = offset.sqrMagnitude <= NameDistance * NameDistance;
            _tag.gameObject.SetActive(near);

            var camera = CameraSingleton.Instance;
            if (near && camera != null)
                _tag.transform.rotation = camera.transform.rotation;

            // Once a day the cat comes to say hello.
            if (_greetedDay != HudModel.Day)
            {
                _greetedDay = HudModel.Day;
                _greetedToday = false;
            }

            if (near && !_greetedToday && _mood != Mood.Napping)
            {
                _greetedToday = true;
                HudModel.Notify(Loc.F("msg.catPurrs", StationProfile.CatName));
            }
        }

        private Vector3 RandomSpot()
        {
            var area = HudModel.BuildArea;
            var min = area.Min;
            var max = area.Max;
            if (max.x <= min.x || max.y <= min.y)
                return new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(12f, 18f));

            // Stay near the buildings, away from the road.
            return new Vector3(Random.Range(min.x + 2f, max.x - 2f), 0f, Random.Range(Mathf.Lerp(min.y, max.y, 0.55f), max.y - 1f));
        }

    }
}
