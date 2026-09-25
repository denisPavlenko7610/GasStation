using System.Collections.Generic;
using System.IO;
using System.Linq;
using GasStation.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GasStation.Editor
{
    /// <summary>Shared helpers for the station editor tools: finding pack prefabs and placing them.</summary>
    public static class StationEditorUtility
    {
        public const string PackFolder = "Assets/_External/Gas station/Prefabs";

        private static readonly string[] CarFolders =
        {
            "Assets/Prefabs/Environment/Interactable/Cars",
            "Assets/Models/Interactable/Cars"
        };

        private static readonly string[] TrashNames =
        {
            "Garbage", "Rusted_garbage", "Aluminum_Can", "Cardboard_box_1", "Cardboard_box_2", "Cardboard_box_3", "wheel"
        };

        /// <summary>Litter must be small enough to pick up by hand.</summary>
        private const float MaxTrashSize = 1.6f;

        /// <summary>Finds a prefab by file name (ignoring stray spaces, e.g. "Conus .prefab") inside a folder.</summary>
        public static GameObject FindPrefab(string name, string folder = PackFolder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return null;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).Trim() == name)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        public static GameObject[] FindCarPrefabs()
        {
            var folders = CarFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0)
                return new GameObject[0];

            var result = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab t:Model", folders))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !result.Contains(asset))
                    result.Add(asset);
            }

            return result.ToArray();
        }

        /// <summary>Small litter prefabs from the Gas station pack.</summary>
        public static GameObject[] FindTrashPrefabs()
        {
            var result = new List<GameObject>();
            foreach (var name in TrashNames)
            {
                var prefab = FindPrefab(name);
                if (prefab != null && MeasurePrefab(prefab).size.magnitude <= MaxTrashSize * 1.8f)
                    result.Add(prefab);
            }

            return result.ToArray();
        }

        public static Bounds MeasurePrefab(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var bounds = GetBounds(instance);
            Object.DestroyImmediate(instance);
            return bounds;
        }

        public static Bounds GetBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Instantiates a prefab so that its bottom touches groundY. Returns null when the prefab is missing.</summary>
        public static GameObject Place(GameObject prefab, Scene scene, Transform parent, Vector3 position, float yaw, float groundY = 0f)
        {
            if (prefab == null)
                return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (parent != null)
                instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var bounds = GetBounds(instance);
            if (bounds.size != Vector3.zero)
                instance.transform.position += Vector3.up * (groundY - bounds.min.y);

            return instance;
        }

        /// <summary>Scatters litter with TrashAuthoring around a point, avoiding a list of keep-out rectangles.</summary>
        public static int ScatterTrash(Scene scene, Transform parent, Vector3 center, Vector2 halfSize, int count,
            int seed, IReadOnlyList<Rect> keepOut = null)
        {
            var prefabs = FindTrashPrefabs();
            if (prefabs.Length == 0)
                return 0;

            Physics.SyncTransforms();
            var random = new System.Random(seed);
            int placed = 0;
            for (int attempt = 0; attempt < count * 10 && placed < count; attempt++)
            {
                var position = center + new Vector3(
                    ((float)random.NextDouble() * 2f - 1f) * halfSize.x,
                    0f,
                    ((float)random.NextDouble() * 2f - 1f) * halfSize.y);

                if (keepOut != null && keepOut.Any(rect => rect.Contains(new Vector2(position.x, position.z))))
                    continue;

                float groundY = GroundHeight(position);
                var prefab = prefabs[random.Next(prefabs.Length)];
                var trash = Place(prefab, scene, parent, new Vector3(position.x, groundY, position.z),
                    (float)random.NextDouble() * 360f, groundY);
                trash.AddComponent<TrashAuthoring>();
                placed++;
            }

            return placed;
        }

        /// <summary>A simple walking figure used for drivers going to the shop. Created once as a prefab asset.</summary>
        public static GameObject GetOrCreatePedestrianPrefab()
        {
            const string folder = "Assets/Prefabs/Characters";
            const string path = folder + "/Pedestrian.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            EnsureFolder(folder);
            var root = new GameObject("Pedestrian");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("PedestrianShirt", new Color(0.2f, 0.45f, 0.8f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Fuel tanker and box truck built from primitives (forward = +Z), saved once as prefabs.</summary>
        public static (GameObject fuelTruck, GameObject cargoTruck) GetOrCreateTruckPrefabs()
        {
            const string folder = "Assets/Prefabs/Vehicles";
            EnsureFolder(folder);

            var fuel = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/FuelTanker.prefab");
            if (fuel == null)
            {
                var root = TruckChassis("FuelTanker", new Color(0.8f, 0.12f, 0.1f));
                var tank = TruckPart(root, "Tank", PrimitiveType.Cylinder, new Vector3(0f, 2.05f, -1.1f), new Vector3(2.1f, 2.6f, 2.1f),
                    GetOrCreateMaterial("TankerSilver", new Color(0.82f, 0.84f, 0.86f)));
                tank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                TruckPart(root, "Stripe", PrimitiveType.Cube, new Vector3(0f, 2.05f, -1.1f), new Vector3(2.14f, 0.35f, 5f),
                    GetOrCreateMaterial("TankerStripe", new Color(0.8f, 0.12f, 0.1f)));
                fuel = PrefabUtility.SaveAsPrefabAsset(root, folder + "/FuelTanker.prefab");
                Object.DestroyImmediate(root);
            }

            var cargo = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/CargoTruck.prefab");
            if (cargo == null)
            {
                var root = TruckChassis("CargoTruck", new Color(0.15f, 0.35f, 0.8f));
                TruckPart(root, "Box", PrimitiveType.Cube, new Vector3(0f, 2.2f, -1.1f), new Vector3(2.4f, 2.8f, 5.4f),
                    GetOrCreateMaterial("CargoBox", new Color(0.95f, 0.85f, 0.35f)));
                cargo = PrefabUtility.SaveAsPrefabAsset(root, folder + "/CargoTruck.prefab");
                Object.DestroyImmediate(root);
            }

            return (fuel, cargo);
        }

        private static GameObject TruckChassis(string name, Color cabColor)
        {
            var root = new GameObject(name);
            var dark = GetOrCreateMaterial("TruckChassis", new Color(0.12f, 0.12f, 0.13f));
            var glass = GetOrCreateMaterial("TruckGlass", new Color(0.2f, 0.3f, 0.4f));
            TruckPart(root, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.4f, 8.4f), dark);
            TruckPart(root, "Cab", PrimitiveType.Cube, new Vector3(0f, 1.9f, 3f), new Vector3(2.3f, 2f, 2.2f),
                GetOrCreateMaterial($"{name}Cab", cabColor));
            TruckPart(root, "Windshield", PrimitiveType.Cube, new Vector3(0f, 2.25f, 4.12f), new Vector3(2f, 0.9f, 0.05f), glass);

            foreach (float z in new[] { 3f, -1.2f, -3f })
            {
                foreach (float x in new[] { -1.05f, 1.05f })
                {
                    var wheel = TruckPart(root, "Wheel", PrimitiveType.Cylinder, new Vector3(x, 0.5f, z), new Vector3(1f, 0.2f, 1f), dark);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            return root;
        }

        private static GameObject TruckPart(GameObject root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        public static Material GetOrCreateMaterial(string name, Color color)
        {
            const string folder = "Assets/Materials/Level";
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(folder);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static float GroundHeight(Vector3 position)
        {
            // The lowest hit is the ground; higher ones are roofs, canopies and props.
            var origin = new Vector3(position.x, position.y + 200f, position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 500f);
            if (hits.Length == 0)
                return position.y;

            float lowest = float.MaxValue;
            foreach (var hit in hits)
                lowest = Mathf.Min(lowest, hit.point.y);
            return lowest;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
