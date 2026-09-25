using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// A building that appears when an upgrade is bought. Before that the <see cref="site"/> (cones, sand,
    /// markings) is shown; when the upgrade is bought during play the building rises inside scaffolding.
    /// </summary>
    public class ConstructionSite : MonoBehaviour
    {
        public UpgradeType upgrade;
        [Tooltip("Upgrade level at which the building exists")]
        public int requiredLevel = 1;
        [Tooltip("Wrapper at ground level around the building model; it is scaled up during construction")]
        public GameObject building;
        public GameObject site;
        [Tooltip("Seconds of game time the construction takes")]
        public float buildSeconds = 6f;
        [Tooltip("Show scaffolding while building (off for small things like decorations)")]
        public bool scaffolding = true;

        private static Material _scaffoldMaterial;

        private bool? _built;
        private float _progress = -1f;
        private Vector3 _buildingScale = Vector3.one;
        private readonly List<GameObject> _scaffolding = new();

        private void Awake()
        {
            if (building != null)
                _buildingScale = building.transform.localScale;
        }

        private void Update()
        {
            if (!HudModel.HasStation)
                return;

            bool built = HudModel.Upgrades.Get(upgrade) >= requiredLevel;
            if (_built != built)
            {
                bool startConstruction = _built.HasValue && built;
                _built = built;
                if (startConstruction)
                    BeginConstruction();
                else
                    ShowFinal(built);
            }

            if (_progress >= 0f)
                Construct();
        }

        private void ShowFinal(bool built)
        {
            if (building != null)
            {
                building.SetActive(built);
                building.transform.localScale = _buildingScale;
            }

            if (site != null)
                site.SetActive(!built);
        }

        private void BeginConstruction()
        {
            _progress = 0f;
            if (building == null)
                return;

            building.SetActive(true);
            building.transform.localScale = _buildingScale;
            if (scaffolding)
                CreateScaffolding(GetBounds(building));
            // The building object is a ground-level wrapper, so scaling it grows the building upwards.
            building.transform.localScale = new Vector3(_buildingScale.x, _buildingScale.y * 0.05f, _buildingScale.z);
        }

        private void Construct()
        {
            _progress += Time.deltaTime / Mathf.Max(0.1f, buildSeconds);
            float t = Mathf.Clamp01(_progress);
            if (building != null)
                building.transform.localScale = new Vector3(_buildingScale.x, _buildingScale.y * Mathf.Lerp(0.05f, 1f, Mathf.SmoothStep(0f, 1f, t)), _buildingScale.z);

            if (t < 1f)
                return;

            _progress = -1f;
            foreach (var part in _scaffolding)
                Destroy(part);
            _scaffolding.Clear();
            ShowFinal(true);
        }

        /// <summary>Four poles and two rails around the future building.</summary>
        private void CreateScaffolding(Bounds bounds)
        {
            if (_scaffoldMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _scaffoldMaterial = new Material(shader) { color = new Color(0.95f, 0.55f, 0.1f) };
                if (_scaffoldMaterial.HasProperty("_BaseColor"))
                    _scaffoldMaterial.SetColor("_BaseColor", new Color(0.95f, 0.55f, 0.1f));
            }

            float height = Mathf.Max(3f, bounds.size.y);
            var min = bounds.min - new Vector3(0.5f, 0f, 0.5f);
            var max = bounds.max + new Vector3(0.5f, 0f, 0.5f);
            float groundY = bounds.min.y;

            foreach (var corner in new[] { new Vector2(min.x, min.z), new Vector2(max.x, min.z), new Vector2(min.x, max.z), new Vector2(max.x, max.z) })
                AddPart(new Vector3(corner.x, groundY + height / 2f, corner.y), new Vector3(0.15f, height, 0.15f));

            foreach (float y in new[] { height * 0.45f, height * 0.9f })
            {
                AddPart(new Vector3((min.x + max.x) / 2f, groundY + y, min.z), new Vector3(max.x - min.x, 0.1f, 0.1f));
                AddPart(new Vector3((min.x + max.x) / 2f, groundY + y, max.z), new Vector3(max.x - min.x, 0.1f, 0.1f));
                AddPart(new Vector3(min.x, groundY + y, (min.z + max.z) / 2f), new Vector3(0.1f, 0.1f, max.z - min.z));
                AddPart(new Vector3(max.x, groundY + y, (min.z + max.z) / 2f), new Vector3(0.1f, 0.1f, max.z - min.z));
            }
        }

        private void AddPart(Vector3 position, Vector3 size)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Scaffolding";
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(transform, true);
            part.transform.position = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = _scaffoldMaterial;
            _scaffolding.Add(part);
        }

        private static Bounds GetBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, new Vector3(4f, 3f, 4f));

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
