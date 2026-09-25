using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using GasStation.Mono.Scenery;
using UnityEngine;

namespace GasStation.Mono.Build
{
    /// <summary>
    /// Draws hired staff on the lot from primitives: a body in the role's uniform color, a head and a name tag
    /// (with "Zz" when tired). People off shift who went home are hidden.
    /// </summary>
    public class StaffPresenter : MonoBehaviour
    {
        private class Body
        {
            public GameObject Root;
            public TextMesh Tag;
            public string Text;
        }

        private readonly Dictionary<int, Body> _bodies = new();
        private readonly HashSet<int> _seen = new();
        private readonly List<int> _removed = new();
        private Font _font;

        private static Color Uniform(StaffRole role) => role switch
        {
            StaffRole.Attendant => new Color(0.95f, 0.5f, 0.1f),
            StaffRole.Janitor => new Color(0.2f, 0.6f, 0.3f),
            StaffRole.Mechanic => new Color(0.2f, 0.35f, 0.75f),
            StaffRole.Cook => new Color(0.95f, 0.95f, 0.95f),
            _ => new Color(0.75f, 0.2f, 0.25f)
        };

        private void Awake() => _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void LateUpdate()
        {
            if (!HudModel.HasStation)
                return;

            _seen.Clear();
            foreach (var staff in HudModel.StaffBodies)
            {
                _seen.Add(staff.WorkerId);
                if (!_bodies.TryGetValue(staff.WorkerId, out var body))
                {
                    body = Create(staff.Role);
                    _bodies.Add(staff.WorkerId, body);
                }

                body.Root.SetActive(!staff.Away);
                if (staff.Away)
                    continue;

                body.Root.transform.SetPositionAndRotation(staff.Position, staff.Rotation);
                UpdateTag(body, staff.WorkerId);
            }

            _removed.Clear();
            foreach (var pair in _bodies)
            {
                if (!_seen.Contains(pair.Key))
                    _removed.Add(pair.Key);
            }

            foreach (int id in _removed)
            {
                Destroy(_bodies[id].Root);
                _bodies.Remove(id);
            }
        }

        private void UpdateTag(Body body, int workerId)
        {
            string text = string.Empty;
            foreach (var worker in HudModel.Workers)
            {
                if (worker.Id != workerId)
                    continue;
                text = GameTexts.StaffName(worker.NameIndex) + (worker.Energy < 0.25f ? " Zz" : string.Empty);
                break;
            }

            if (text != body.Text)
            {
                body.Text = text;
                body.Tag.text = text;
            }

            // Name tags face the camera.
            var camera = CameraSingleton.Instance;
            if (camera != null)
                body.Tag.transform.rotation = camera.transform.rotation;
        }

        private Body Create(StaffRole role)
        {
            var root = new GameObject($"Staff_{role}");
            root.transform.SetParent(transform, false);
            PrimitiveArt.Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 0.8f, 0.5f), Uniform(role));
            PrimitiveArt.Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.75f, 0f), new Vector3(0.35f, 0.35f, 0.35f), new Color(0.9f, 0.75f, 0.6f));
            // A cap in the uniform color shows which way they face.
            PrimitiveArt.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.9f, 0.1f), new Vector3(0.3f, 0.06f, 0.35f), Uniform(role));

            var tagObject = new GameObject("NameTag");
            tagObject.transform.SetParent(root.transform, false);
            tagObject.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var tag = tagObject.AddComponent<TextMesh>();
            tag.font = _font;
            tag.fontSize = 48;
            tag.characterSize = 0.05f;
            tag.anchor = TextAnchor.MiddleCenter;
            tag.alignment = TextAlignment.Center;
            tag.color = Color.white;
            tagObject.GetComponent<MeshRenderer>().sharedMaterial = _font.material;

            return new Body { Root = root, Tag = tag };
        }

    }
}
