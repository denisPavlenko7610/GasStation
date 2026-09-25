using System.Collections.Generic;
using System.Linq;
using GasStation.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GasStation.Editor
{
    /// <summary>
    /// Creates a ready-to-tweak station layout (station, spawner with routes, two pumps) in the active scene.
    /// Open the Entities SubScene for editing and make it the active scene before running.
    /// </summary>
    public static class StationSetupMenu
    {
        private static readonly string[] CarFolders =
        {
            "Assets/Prefabs/Environment/Interactable/Cars",
            "Assets/Models/Interactable/Cars"
        };

        [MenuItem("GasStation/Создать объекты станции")]
        private static void CreateStation()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var sceneView = SceneView.lastActiveSceneView;
            var center = sceneView != null ? sceneView.pivot : Vector3.zero;
            center.y = 0f;

            var root = new GameObject("GasStationSetup");
            Undo.RegisterCreatedObjectUndo(root, "Create station");
            root.transform.position = center;

            Create("Station", root.transform, Vector3.zero).AddComponent<StationAuthoring>();

            var spawnerGo = Create("CarSpawner", root.transform, new Vector3(-40f, 0f, 0f));
            spawnerGo.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var spawner = spawnerGo.AddComponent<CarSpawnerAuthoring>();
            spawner.carPrefabs = FindCarPrefabs();
            spawner.entryRoute = new[] { Create("Entry_0", spawnerGo.transform, new Vector3(-25f, 0f, 0f)).transform };

            var queueHead = Create("QueueHead", spawnerGo.transform, new Vector3(-10f, 0f, 0f));
            queueHead.transform.rotation = Quaternion.LookRotation(Vector3.right);
            spawner.queueHead = queueHead.transform;

            spawner.exitRoute = new[]
            {
                Create("Exit_0", spawnerGo.transform, new Vector3(12f, 0f, 0f)).transform,
                Create("Exit_1_Despawn", spawnerGo.transform, new Vector3(40f, 0f, 0f)).transform
            };

            CreatePump(root.transform, 1, new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -2.5f));
            CreatePump(root.transform, 2, new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 2.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;

            Debug.Log($"GasStation: объекты станции созданы в сцене '{scene.name}'. " +
                      $"Найдено машин: {spawner.carPrefabs.Length}. Расставьте точки маршрута и колонки под окружение.");
        }

        private static void CreatePump(Transform parent, int number, Vector3 position, Vector3 stopPosition)
        {
            var pumpGo = Create($"Pump_{number}", parent, position);
            var pump = pumpGo.AddComponent<PumpAuthoring>();
            pump.number = number;

            var stop = Create("StopPoint", pumpGo.transform, stopPosition - position);
            stop.transform.rotation = Quaternion.LookRotation(Vector3.right);
            pump.stopPoint = stop.transform;
        }

        private static GameObject Create(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static GameObject[] FindCarPrefabs()
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
    }
}
