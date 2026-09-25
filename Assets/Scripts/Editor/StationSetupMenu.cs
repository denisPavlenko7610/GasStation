using GasStation.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GasStation.Editor
{
    /// <summary>
    /// Adds gameplay objects to an existing scene. Open the Entities SubScene for editing and make it the
    /// active scene before running.
    /// </summary>
    public static class StationSetupMenu
    {
        [MenuItem("GasStation/Создать объекты станции")]
        private static void CreateStation()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var center = SceneViewCenter();

            var root = new GameObject("GasStationSetup");
            Undo.RegisterCreatedObjectUndo(root, "Create station");
            root.transform.position = center;

            var station = Create("Station", root.transform, Vector3.zero);
            station.AddComponent<StationAuthoring>();
            station.AddComponent<TrashSpawnerAuthoring>().trashPrefabs = StationEditorUtility.FindTrashPrefabs();

            var spawnerGo = Create("CarSpawner", root.transform, new Vector3(-40f, 0f, 0f));
            spawnerGo.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var spawner = spawnerGo.AddComponent<CarSpawnerAuthoring>();
            spawner.carPrefabs = StationEditorUtility.FindCarPrefabs();
            spawner.entryRoute = new[] { Create("Entry_0", spawnerGo.transform, new Vector3(-25f, 0f, 0f)).transform };

            var queueHead = Create("QueueHead", spawnerGo.transform, new Vector3(-10f, 0f, 0f));
            queueHead.transform.rotation = Quaternion.LookRotation(Vector3.right);
            spawner.queueHead = queueHead.transform;

            spawner.exitRoute = new[]
            {
                Create("Exit_0", spawnerGo.transform, new Vector3(12f, 0f, 0f)).transform,
                Create("Exit_1_Despawn", spawnerGo.transform, new Vector3(40f, 0f, 0f)).transform
            };

            CreatePump(root.transform, 1, new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -2.5f), 0);
            // Worn pump for the "repair a pump" quest.
            CreatePump(root.transform, 2, new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 2.5f), 0).startCondition = 0.3f;
            // Pumps 3 and 4 are opened by the ExtraPump upgrade.
            CreatePump(root.transform, 3, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, -9.5f), 1);
            CreatePump(root.transform, 4, new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, 9.5f), 2);

            var shop = Create("Shop_Door", root.transform, new Vector3(0f, 0f, 15f));
            shop.AddComponent<ShopAuthoring>().pedestrianPrefab = StationEditorUtility.GetOrCreatePedestrianPrefab();

            var wash = Create("CarWash_Bay", root.transform, new Vector3(20f, 0f, 10f));
            wash.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var washAuthoring = wash.AddComponent<CarWashAuthoring>();
            washAuthoring.entryRoute = new[] { Create("Wash_Entry", root.transform, new Vector3(10f, 0f, 6f)).transform };
            washAuthoring.exitRoute = new[]
            {
                Create("Wash_Exit_0", root.transform, new Vector3(30f, 0f, 10f)).transform,
                Create("Wash_Exit_1_Despawn", root.transform, new Vector3(45f, 0f, 0f)).transform
            };

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;

            Debug.Log($"GasStation: объекты станции созданы в сцене '{scene.name}'. " +
                      $"Найдено машин: {spawner.carPrefabs.Length}. Расставьте точки маршрута и колонки под окружение.");
        }

        [MenuItem("GasStation/Набросать мусор (30 шт. вокруг Scene View)")]
        private static void ScatterTrash()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var parent = new GameObject("Trash");
            Undo.RegisterCreatedObjectUndo(parent, "Scatter trash");
            SceneManagerMove(parent, scene);

            int placed = StationEditorUtility.ScatterTrash(scene, parent.transform, SceneViewCenter(),
                new Vector2(25f, 25f), 30, System.Environment.TickCount);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = parent;
            Debug.Log(placed > 0
                ? $"GasStation: разбросано {placed} ед. мусора в сцене '{scene.name}'."
                : "GasStation: не нашёл префабы мусора в паке 'Gas station'.");
        }

        private static Vector3 SceneViewCenter()
        {
            var sceneView = SceneView.lastActiveSceneView;
            var center = sceneView != null ? sceneView.pivot : Vector3.zero;
            center.y = 0f;
            return center;
        }

        private static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
        {
            if (go.scene != scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static PumpAuthoring CreatePump(Transform parent, int number, Vector3 position, Vector3 stopPosition, int requiredUpgradeLevel)
        {
            var pumpGo = Create($"Pump_{number}", parent, position);
            var pump = pumpGo.AddComponent<PumpAuthoring>();
            pump.number = number;
            pump.requiredUpgradeLevel = requiredUpgradeLevel;

            var stop = Create("StopPoint", pumpGo.transform, stopPosition - position);
            stop.transform.rotation = Quaternion.LookRotation(Vector3.right);
            pump.stopPoint = stop.transform;
            return pump;
        }

        private static GameObject Create(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }
}
