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
