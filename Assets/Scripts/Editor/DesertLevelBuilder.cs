using System.Collections.Generic;
using GasStation.Authoring;
using GasStation.Mono;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GasStation.Editor
{
    /// <summary>
    /// Builds a playable desert level from the "Gas station" asset pack: a highway, an abandoned station
    /// with a canopy, shop, fences, props and litter, plus a SubScene with all gameplay objects.
    ///
    /// Layout (top view, X along the highway, Z away from it):
    ///   z = 28      fence
    ///   z = 20      shop (operator's room), tank, vending machine, old car
    ///   z = -12..12 canopy with pumps 1-4, cars drive along +X through the stop points
    ///   z = -22     highway; cars come from -X, turn in at x = -38 and leave at x = 40
    /// </summary>
    public static class DesertLevelBuilder
    {
        private const string MainScenePath = "Assets/Scenes/Desert.unity";
        private const string SubSceneFolder = "Assets/Scenes/Desert";
        private const string SubScenePath = SubSceneFolder + "/DesertGameplay.unity";
        private const string MaterialFolder = "Assets/Materials/Level";

        private const float RoadZ = -22f;
        private const float RoadHalfLength = 150f;
        private static readonly Rect Lot = new(-36f, -15f, 72f, 42f);

        [MenuItem("GasStation/Построить уровень «Пустыня»")]
        private static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) != null &&
                !EditorUtility.DisplayDialog("Пустыня", $"{MainScenePath} уже существует. Перестроить уровень?", "Перестроить", "Отмена"))
                return;

            if (StationEditorUtility.FindPrefab("Station_Canopy") == null)
            {
                EditorUtility.DisplayDialog("Пустыня", $"Не найден пак ассетов в {StationEditorUtility.PackFolder}.", "OK");
                return;
            }

            StationEditorUtility.EnsureFolder(SubSceneFolder);
            StationEditorUtility.EnsureFolder(MaterialFolder);

            var main = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(main, MainScenePath);

            BuildEnvironment(main);
            SetupCameraAndLight(main);

            var gameplay = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            BuildGameplay(gameplay);
            EditorSceneManager.SaveScene(gameplay, SubScenePath);
            EditorSceneManager.CloseScene(gameplay, true);

            var subSceneGo = new GameObject("Gameplay (SubScene)");
            SceneManager.MoveGameObjectToScene(subSceneGo, main);
            var subScene = subSceneGo.AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath);
            subScene.AutoLoadScene = true;

            EditorSceneManager.SaveScene(main);
            AddToBuildSettings(MainScenePath);
            Debug.Log($"GasStation: уровень построен: {MainScenePath}. Нажми Play.");
        }

        private static void BuildEnvironment(Scene scene)
        {
            var root = new GameObject("Environment");
            SceneManager.MoveGameObjectToScene(root, scene);
            var parent = root.transform;

            CreateGround(scene, parent);
            TileAlongX("Road_Part1", scene, parent, RoadZ, -RoadHalfLength, RoadHalfLength);
            TileAlongX("Road_fence", scene, parent, RoadZ - 8f, -RoadHalfLength, RoadHalfLength);

            Place("Station_Canopy", scene, parent, new Vector3(0f, 0f, 0f), 0f);
            Place("Petrol_pump", scene, parent, new Vector3(0f, 0f, -5f), 90f);
            Place("Petrol_pump", scene, parent, new Vector3(0f, 0f, 5f), 90f);
            // Closed pumps look old until the ExtraPump upgrade opens them.
            Place("Petrol_pump_2", scene, parent, new Vector3(0f, 0f, -12f), 90f);
            Place("Petrol_pump_2", scene, parent, new Vector3(0f, 0f, 12f), 90f);

            Place("Operator 's_room", scene, parent, new Vector3(0f, 0f, 21f), 180f);
            Place("Electric_panel", scene, parent, new Vector3(-11f, 0f, 21f), 180f);
            Place("Fire_extinguisher", scene, parent, new Vector3(7f, 0f, 18f), 180f);
            Place("Vending_machine_Rusted", scene, parent, new Vector3(10f, 0f, 19f), 180f);
            Place("Trash_can", scene, parent, new Vector3(-8f, 0f, 17f), 0f);
            Place("Trash_can_2", scene, parent, new Vector3(5f, 0f, -9f), 0f);
            Place("Gas_Cistern", scene, parent, new Vector3(-27f, 0f, 19f), 90f);
            Place("Old_Rust_Car", scene, parent, new Vector3(-18f, 0f, 24f), 35f);
            // Car wash bay, opened by the CarWash upgrade.
            Place("Station_Canopy_2", scene, parent, new Vector3(26f, 0f, 12f), 90f);
            Place("Air_conditioning", scene, parent, new Vector3(-6f, 0f, 24f), 180f);
            CreateRestroomHut(scene, parent, new Vector3(11f, 0f, 21f));
            Place("Hydrant", scene, parent, new Vector3(-34f, 0f, -13f), 0f);
            Place("Gas_Station_Sign", scene, parent, new Vector3(-26f, 0f, -15f), 180f);
            Place("Warning_sign_1", scene, parent, new Vector3(-16f, 0f, -9f), 180f);
            Place("Warning_sign_2", scene, parent, new Vector3(20f, 0f, -12f), 0f);
            Place("Mini_Flags", scene, parent, new Vector3(30f, 0f, -14f), 0f);

            for (int i = 0; i < 3; i++)
                Place("Conus", scene, parent, new Vector3(-42f + i * 2f, 0f, -14f), 0f);

            for (float x = -RoadHalfLength + 15f; x < RoadHalfLength; x += 30f)
                Place("Streetlight", scene, parent, new Vector3(x, 0f, RoadZ + 7f), 180f);

            foreach (var corner in new[] { new Vector2(-34f, -13f), new Vector2(34f, -13f), new Vector2(-34f, 25f), new Vector2(34f, 25f) })
                Place("Old_Lamp", scene, parent, new Vector3(corner.x, 0f, corner.y), 0f);

            // Broken fence around the back and the sides of the lot.
            string[] fences = { "Wire_fence_Part1", "Wire_fence_Part2", "Wire_fence_Part3" };
            TileLine(fences, scene, parent, new Vector3(-Lot.width / 2f, 0f, Lot.yMax), Vector3.right, Lot.width);
            TileLine(fences, scene, parent, new Vector3(Lot.xMin, 0f, Lot.yMin + 10f), Vector3.forward, Lot.height - 10f);
            TileLine(fences, scene, parent, new Vector3(Lot.xMax, 0f, Lot.yMin + 10f), Vector3.forward, Lot.height - 10f);

            ScatterNature(scene, parent);
        }

        private static void CreateGround(Scene scene, Transform parent)
        {
            var sand = GetOrCreateMaterial("DesertSand", new Color(0.82f, 0.68f, 0.48f));
            var asphalt = GetOrCreateMaterial("OldAsphalt", new Color(0.28f, 0.27f, 0.26f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            ground.GetComponent<Renderer>().sharedMaterial = sand;

            var lot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lot.name = "Lot";
            SceneManager.MoveGameObjectToScene(lot, scene);
            lot.transform.SetParent(parent, false);
            lot.transform.localPosition = new Vector3(Lot.center.x, 0.01f, Lot.center.y);
            lot.transform.localScale = new Vector3(Lot.width, 0.02f, Lot.height);
            lot.GetComponent<Renderer>().sharedMaterial = asphalt;

            // Driveway from the highway into the lot.
            var driveway = Object.Instantiate(lot, parent);
            driveway.name = "Driveway";
            driveway.transform.localPosition = new Vector3(0f, 0.01f, (RoadZ + Lot.yMin) / 2f);
            driveway.transform.localScale = new Vector3(Lot.width + 10f, 0.02f, Lot.yMin - RoadZ);
        }

        private static void CreateRestroomHut(Scene scene, Transform parent, Vector3 position)
        {
            var hut = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hut.name = "Restroom_Hut";
            SceneManager.MoveGameObjectToScene(hut, scene);
            hut.transform.SetParent(parent, false);
            hut.transform.localPosition = position + new Vector3(1.5f, 1.25f, 0f);
            hut.transform.localScale = new Vector3(3f, 2.5f, 3f);
            hut.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("RestroomWalls", new Color(0.75f, 0.78f, 0.8f));
        }

        private static void ScatterNature(Scene scene, Transform parent)
        {
            string[] plants = { "Cactus_1", "Cactus_2", "Cactus_3", "Cactus_4", "Bush", "Bush_2", "Bush_3" };
            string[] sand = { "Sand", "Sand_2" };
            var random = new System.Random(42);
            var keepOut = new Rect(Lot.xMin - 6f, RoadZ - 10f, Lot.width + 12f, Lot.yMax - RoadZ + 16f);

            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < 90; attempt++)
            {
                var position = new Vector3((float)random.NextDouble() * 300f - 150f, 0f, (float)random.NextDouble() * 220f - 90f);
                bool onRoad = Mathf.Abs(position.z - RoadZ) < 10f;
                if (onRoad || keepOut.Contains(new Vector2(position.x, position.z)))
                    continue;

                var names = placed % 5 == 4 ? sand : plants;
                Place(names[random.Next(names.Length)], scene, parent, position, (float)random.NextDouble() * 360f);
                placed++;
            }
        }

        private static void SetupCameraAndLight(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.TryGetComponent<Camera>(out var camera))
                {
                    camera.transform.SetPositionAndRotation(new Vector3(0f, 14f, -22f), Quaternion.Euler(45f, 0f, 0f));
                    camera.farClipPlane = 600f;
                    var singleton = go.AddComponent<CameraSingleton>();
                    var serialized = new SerializedObject(singleton);
                    serialized.FindProperty("followDistance").floatValue = 12f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (go.TryGetComponent<Light>(out var light) && light.type == LightType.Directional)
                {
                    RenderSettings.sun = light;
                    light.shadows = LightShadows.Soft;
                    go.AddComponent<DayNightCycle>();
                }
            }
        }

        private static void BuildGameplay(Scene scene)
        {
            var root = new GameObject("Gameplay");
            SceneManager.MoveGameObjectToScene(root, scene);
            var parent = root.transform;

            var station = Create("Station", parent, Vector3.zero);
            var stationAuthoring = station.AddComponent<StationAuthoring>();
            // The story starts at an abandoned station: little money, poor reputation.
            stationAuthoring.startMoney = 600f;
            stationAuthoring.startReputation = 0.3f;
            station.AddComponent<TrashSpawnerAuthoring>().trashPrefabs = StationEditorUtility.FindTrashPrefabs();

            var spawnerGo = Create("CarSpawner", parent, new Vector3(-RoadHalfLength + 20f, 0f, RoadZ));
            spawnerGo.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var spawner = spawnerGo.AddComponent<CarSpawnerAuthoring>();
            spawner.carPrefabs = StationEditorUtility.FindCarPrefabs();
            spawner.entryRoute = new[]
            {
                Create("Entry_0_Road", spawnerGo.transform, new Vector3(-45f, 0f, RoadZ)).transform,
                Create("Entry_1_TurnIn", spawnerGo.transform, new Vector3(-38f, 0f, -8f)).transform,
                Create("Entry_2_Lot", spawnerGo.transform, new Vector3(-30f, 0f, 0f)).transform
            };

            var queueHead = Create("QueueHead", spawnerGo.transform, new Vector3(-12f, 0f, 0f));
            queueHead.transform.rotation = Quaternion.LookRotation(Vector3.right);
            spawner.queueHead = queueHead.transform;
            spawner.queueSpacing = 6f;

            spawner.exitRoute = new[]
            {
                Create("Exit_0_Lot", spawnerGo.transform, new Vector3(16f, 0f, 0f)).transform,
                Create("Exit_1_TurnOut", spawnerGo.transform, new Vector3(32f, 0f, -10f)).transform,
                Create("Exit_2_Road", spawnerGo.transform, new Vector3(42f, 0f, RoadZ)).transform,
                Create("Exit_3_Despawn", spawnerGo.transform, new Vector3(RoadHalfLength - 20f, 0f, RoadZ)).transform
            };

            // Abandoned station: pump 1 barely works, the others must be repaired first.
            CreatePump(parent, 1, new Vector3(0f, 0f, -5f), -2.5f, 0, 0.6f);
            CreatePump(parent, 2, new Vector3(0f, 0f, 5f), 2.5f, 0, 0f);
            CreatePump(parent, 3, new Vector3(0f, 0f, -12f), -9.5f, 1, 0f);
            CreatePump(parent, 4, new Vector3(0f, 0f, 12f), 9.5f, 2, 0f);

            CreatePlayer(parent, new Vector3(-6f, 0f, -10f));

            var shop = Create("Shop_Door", parent, new Vector3(0f, 0f, 17.5f));
            shop.AddComponent<ShopAuthoring>().pedestrianPrefab = StationEditorUtility.GetOrCreatePedestrianPrefab();

            // Restroom on the right side of the shop; the hut is a placeholder until a real model is added.
            Create("Restroom_Door", parent, new Vector3(9f, 0f, 21f)).AddComponent<RestroomAuthoring>();

            // Four truck spots behind the wash, opened two at a time by the TruckParking upgrade.
            var parking = Create("TruckParking", parent, new Vector3(16f, 0f, 18f));
            var parkingAuthoring = parking.AddComponent<TruckParkingAuthoring>();
            parkingAuthoring.entry = parking.transform;
            parkingAuthoring.spots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var spot = Create($"Spot_{i + 1}", parent, new Vector3(16f + i * 6f, 0f, 24f));
                spot.transform.rotation = Quaternion.LookRotation(Vector3.right);
                parkingAuthoring.spots[i] = spot.transform;
            }

            var wash = Create("CarWash_Bay", parent, new Vector3(26f, 0f, 12f));
            wash.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var washAuthoring = wash.AddComponent<CarWashAuthoring>();
            washAuthoring.entryRoute = new[]
            {
                Create("Wash_Entry_0", wash.transform.parent, new Vector3(12f, 0f, 6f)).transform,
                Create("Wash_Entry_1", wash.transform.parent, new Vector3(18f, 0f, 12f)).transform
            };
            washAuthoring.exitRoute = new[]
            {
                Create("Wash_Exit_0", wash.transform.parent, new Vector3(36f, 0f, 12f)).transform,
                Create("Wash_Exit_1", wash.transform.parent, new Vector3(44f, 0f, -4f)).transform,
                Create("Wash_Exit_2_Road", wash.transform.parent, new Vector3(46f, 0f, RoadZ)).transform,
                Create("Wash_Exit_3_Despawn", wash.transform.parent, new Vector3(RoadHalfLength - 20f, 0f, RoadZ)).transform
            };

            // Litter of an abandoned station: everywhere except the pump lanes and the shop door.
            var trash = Create("Trash", parent, Vector3.zero);
            var keepOut = new List<Rect>
            {
                new(-3f, -14f, 6f, 28f),   // pump island
                new(-4f, 15f, 8f, 5f),     // shop door
                new(8f, 4f, 30f, 12f),     // wash lane
                new(10f, 17f, 26f, 10f)    // truck parking and restroom
            };
            StationEditorUtility.ScatterTrash(scene, trash.transform, new Vector3(Lot.center.x, 0f, Lot.center.y),
                new Vector2(Lot.width / 2f - 2f, Lot.height / 2f - 2f), 45, 1234, keepOut);
        }

        private static void CreatePlayer(Transform parent, Vector3 position)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.SetParent(parent, false);
            player.transform.localPosition = position + Vector3.up;
            // Movement is kinematic; a baked collider would only create a static physics body.
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("PlayerOveralls", new Color(0.9f, 0.45f, 0.1f));

            player.AddComponent<PlayerTagAuthoring>();
            player.AddComponent<MoveInputAuthoring>();
            var speed = new SerializedObject(player.AddComponent<SpeedAuthoring>());
            speed.FindProperty("<Value>k__BackingField").floatValue = 8f;
            speed.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePump(Transform parent, int number, Vector3 position, float stopZ, int requiredUpgradeLevel, float condition)
        {
            var pumpGo = Create($"Pump_{number}", parent, position);
            var pump = pumpGo.AddComponent<PumpAuthoring>();
            pump.number = number;
            pump.requiredUpgradeLevel = requiredUpgradeLevel;
            pump.startCondition = condition;

            var stop = Create("StopPoint", pumpGo.transform, new Vector3(0f, 0f, stopZ - position.z));
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

        private static GameObject Place(string prefabName, Scene scene, Transform parent, Vector3 position, float yaw)
        {
            var prefab = StationEditorUtility.FindPrefab(prefabName);
            if (prefab == null)
                Debug.LogWarning($"GasStation: префаб '{prefabName}' не найден, пропускаю.");
            return StationEditorUtility.Place(prefab, scene, parent, position, yaw);
        }

        /// <summary>Repeats a prefab along X, rotating it so its longest side follows the line.</summary>
        private static void TileAlongX(string prefabName, Scene scene, Transform parent, float z, float fromX, float toX)
        {
            var prefab = StationEditorUtility.FindPrefab(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"GasStation: префаб '{prefabName}' не найден, пропускаю.");
                return;
            }

            var size = StationEditorUtility.MeasurePrefab(prefab).size;
            bool alongZ = size.z > size.x;
            float length = Mathf.Max(1f, alongZ ? size.z : size.x);
            float yaw = alongZ ? 90f : 0f;

            for (float x = fromX; x < toX; x += length)
            {
                var tile = StationEditorUtility.Place(prefab, scene, parent, new Vector3(x, 0f, z), yaw);
                CenterOnLine(tile, z, alongX: true);
            }
        }

        private static void TileLine(string[] prefabNames, Scene scene, Transform parent, Vector3 start, Vector3 direction, float totalLength)
        {
            var prefabs = new List<GameObject>();
            foreach (var name in prefabNames)
            {
                var prefab = StationEditorUtility.FindPrefab(name);
                if (prefab != null)
                    prefabs.Add(prefab);
            }

            if (prefabs.Count == 0)
                return;

            var size = StationEditorUtility.MeasurePrefab(prefabs[0]).size;
            bool longAlongZ = size.z > size.x;
            float length = Mathf.Max(1f, Mathf.Max(size.x, size.z));
            bool lineAlongX = Mathf.Abs(direction.x) > 0.5f;
            float yaw = lineAlongX == longAlongZ ? 90f : 0f;

            int index = 0;
            for (float travelled = length / 2f; travelled < totalLength; travelled += length, index++)
            {
                // Leave gaps: the fence of an abandoned station is broken.
                if (index % 4 == 3)
                    continue;

                StationEditorUtility.Place(prefabs[index % prefabs.Count], scene, parent, start + direction * travelled, yaw);
            }
        }

        private static void CenterOnLine(GameObject go, float z, bool alongX)
        {
            if (go == null || !alongX)
                return;

            var bounds = StationEditorUtility.GetBounds(go);
            go.transform.position += new Vector3(0f, 0f, z - bounds.center.z);
        }

        private static Material GetOrCreateMaterial(string name, Color color) =>
            StationEditorUtility.GetOrCreateMaterial(name, color);

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
                return;

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
