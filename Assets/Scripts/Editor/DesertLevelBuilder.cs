using System.Collections.Generic;
using GasStation.Authoring;
using GasStation.Mono;
using GasStation.Mono.Scenery;
using GasStation.Components;
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

            // Buildings and pumps are tinted by StationPainter with the current paint scheme.
            var primary = new List<GameObject>();
            var accent = new List<GameObject>();

            primary.Add(Place("Station_Canopy", scene, parent, new Vector3(0f, 0f, 0f), 0f));
            accent.Add(Place("Petrol_pump", scene, parent, new Vector3(0f, 0f, -5f), 90f));
            accent.Add(Place("Petrol_pump", scene, parent, new Vector3(0f, 0f, 5f), 90f));

            var shop = Place("Operator 's_room", scene, parent, new Vector3(0f, 0f, 21f), 180f);
            primary.Add(shop);
            Place("Electric_panel", scene, parent, new Vector3(-11f, 0f, 21f), 180f);
            Place("Fire_extinguisher", scene, parent, new Vector3(7f, 0f, 18f), 180f);
            Place("Trash_can", scene, parent, new Vector3(-8f, 0f, 17f), 0f);
            Place("Trash_can_2", scene, parent, new Vector3(5f, 0f, -9f), 0f);
            Place("Gas_Cistern", scene, parent, new Vector3(-33f, 0f, -7f), 90f);
            Place("Old_Rust_Car", scene, parent, new Vector3(-46f, 0f, 22f), 35f);
            Place("Air_conditioning", scene, parent, new Vector3(-6f, 0f, 24f), 180f);
            primary.Add(CreateRestroomHut(scene, parent, new Vector3(11f, 0f, 21f)));
            Place("Hydrant", scene, parent, new Vector3(-34f, 0f, -13f), 0f);
            Place("Warning_sign_1", scene, parent, new Vector3(-16f, 0f, -9f), 180f);
            Place("Warning_sign_2", scene, parent, new Vector3(-24f, 0f, -12f), 0f);
            Place("Mini_Flags", scene, parent, new Vector3(30f, 0f, -14f), 0f);

            for (int i = 0; i < 3; i++)
                Place("Conus", scene, parent, new Vector3(-42f + i * 2f, 0f, -14f), 0f);

            for (float x = -RoadHalfLength + 15f; x < RoadHalfLength; x += 30f)
            {
                var streetlight = Place("Streetlight", scene, parent, new Vector3(x, 0f, RoadZ + 7f), 180f);
                AddNightLight(streetlight, TopOf(streetlight, new Vector3(x, 6f, RoadZ + 7f)), 16f, 2.5f, new Color(1f, 0.82f, 0.55f));
            }

            // Canopy lights (neon with the 80s scheme) and a warm light behind the shop windows once they are fixed.
            var canopy = primary.Count > 0 ? primary[0] : null;
            float canopyHeight = canopy != null ? StationEditorUtility.GetBounds(canopy).max.y - 0.6f : 5f;
            foreach (float z in new[] { -4f, 4f })
                AddNightLight(canopy, new Vector3(0f, canopyHeight, z), 18f, 3f, new Color(0.9f, 0.95f, 1f), neon: true);
            if (shop != null)
            {
                var shopBounds = StationEditorUtility.GetBounds(shop);
                AddNightLight(shop, new Vector3(shopBounds.center.x, 2f, shopBounds.min.z - 1.2f), 9f, 2f,
                    new Color(1f, 0.8f, 0.5f), renovationId: (int)RenovationKind.Windows);
            }

            BuildRenovations(scene, parent, shop, accent);
            BuildConstructionSites(scene, parent, primary, accent);
            BuildDecor(scene, parent, shop);
            BuildPriceBoard(scene, parent);
            BuildCompetitor(scene, parent);
            ScatterNature(scene, parent);

            var painter = root.AddComponent<StationPainter>();
            painter.primary = RenderersOf(primary);
            painter.accent = RenderersOf(accent);
        }

        /// <summary>
        /// "Before/after" pairs for the renovations (ids match the RenovationAuthoring points in the SubScene).
        /// The asset pack has rusty/clean pairs; boards and graffiti are simple primitives.
        /// </summary>
        private static void BuildRenovations(Scene scene, Transform parent, GameObject shop, List<GameObject> accent)
        {
            var shopBounds = shop != null ? StationEditorUtility.GetBounds(shop) : new Bounds(new Vector3(0f, 2f, 21f), new Vector3(12f, 4f, 7f));

            // 0: boarded-up windows on the shop front (left and right of the door).
            var (windowsBroken, _) = RenovationPair(scene, parent, (int)RenovationKind.Windows, "Windows");
            var boards = GetOrCreateMaterial("OldBoards", new Color(0.36f, 0.25f, 0.16f));
            float front = shopBounds.min.z - 0.06f;
            foreach (float x in new[] { shopBounds.center.x - 3.5f, shopBounds.center.x + 3.5f })
            {
                for (int i = 0; i < 3; i++)
                {
                    var board = Primitive(scene, windowsBroken.transform, $"Board_{i}", boards,
                        new Vector3(x, 1.1f + i * 0.35f, front), new Vector3(1.6f, 0.22f, 0.06f));
                    board.transform.rotation = Quaternion.Euler(0f, 0f, (i - 1) * 8f);
                }
            }

            // 1: graffiti on the side walls of the shop.
            var (graffitiBroken, _) = RenovationPair(scene, parent, (int)RenovationKind.Graffiti, "Graffiti");
            var paints = new[]
            {
                GetOrCreateMaterial("GraffitiPink", new Color(0.9f, 0.2f, 0.6f)),
                GetOrCreateMaterial("GraffitiGreen", new Color(0.2f, 0.85f, 0.35f)),
                GetOrCreateMaterial("GraffitiBlue", new Color(0.2f, 0.5f, 0.95f))
            };
            for (int i = 0; i < 3; i++)
            {
                float z = shopBounds.center.z + (i - 1) * 1.8f;
                Primitive(scene, graffitiBroken.transform, $"Tag_W{i}", paints[i],
                    new Vector3(shopBounds.min.x - 0.04f, 1.2f + 0.3f * i, z), new Vector3(0.04f, 0.9f, 1.4f));
                Primitive(scene, graffitiBroken.transform, $"Tag_E{i}", paints[(i + 1) % 3],
                    new Vector3(shopBounds.max.x + 0.04f, 1.0f + 0.35f * i, z), new Vector3(0.04f, 0.8f, 1.6f));
            }

            // 2: the wire fence with holes becomes a clean fence.
            var (fenceBroken, fenceFixed) = RenovationPair(scene, parent, (int)RenovationKind.Fence, "Fence");
            string[] wireFences = { "Wire_fence_Part1", "Wire_fence_Part2", "Wire_fence_Part3" };
            string[] cleanFences = { "Fence_Part1_Clean", "Fence_Part2_Clean", "Fence_Part3_Clean" };
            FenceAround(wireFences, scene, fenceBroken.transform, gaps: true);
            FenceAround(cleanFences, scene, fenceFixed.transform, gaps: false);

            // 3: old lamps become new ones.
            var (lampsBroken, lampsFixed) = RenovationPair(scene, parent, (int)RenovationKind.Lamps, "Lamps");
            foreach (var corner in new[] { new Vector2(-34f, -13f), new Vector2(34f, -13f), new Vector2(-34f, 25f), new Vector2(34f, 25f) })
            {
                var position = new Vector3(corner.x, 0f, corner.y);
                Place("Old_Lamp", scene, lampsBroken.transform, position, 0f);
                var lamp = Place("Lamp", scene, lampsFixed.transform, position, 0f);
                AddNightLight(lamp, TopOf(lamp, position + Vector3.up * 4f), 14f, 2.5f, new Color(1f, 0.85f, 0.6f),
                    renovationId: (int)RenovationKind.Lamps);
            }

            // 4: rusty vending machine becomes a new one.
            var (vendingBroken, vendingFixed) = RenovationPair(scene, parent, (int)RenovationKind.VendingMachine, "VendingMachine");
            Place("Vending_machine_Rusted", scene, vendingBroken.transform, new Vector3(10f, 0f, 19f), 180f);
            Place("Vending_machine", scene, vendingFixed.transform, new Vector3(10f, 0f, 19f), 180f);

            // 5: a crooked old sign becomes the station sign.
            var (signBroken, signFixed) = RenovationPair(scene, parent, (int)RenovationKind.Sign, "Sign");
            var oldSign = Place("Gas_Station_Sign_2", scene, signBroken.transform, new Vector3(-26f, 0f, -15f), 180f);
            if (oldSign != null)
                oldSign.transform.rotation = Quaternion.Euler(0f, 180f, 9f);
            var sign = Place("Gas_Station_Sign", scene, signFixed.transform, new Vector3(-26f, 0f, -15f), 180f);
            accent.Add(sign);
            if (sign != null)
            {
                // The station name appears on the sign once it is renovated.
                var signBounds = StationEditorUtility.GetBounds(sign);
                var nameAnchor = Create("NameAnchor", signFixed.transform, Vector3.zero);
                nameAnchor.transform.position = new Vector3(signBounds.center.x, signBounds.max.y + 0.7f, signBounds.center.z);
                nameAnchor.AddComponent<StationNameSign>().characterSize = 0.16f;
            }
            AddNightLight(sign, new Vector3(-26f, 2f, -12.5f), 9f, 2.5f, new Color(1f, 0.9f, 0.7f),
                renovationId: (int)RenovationKind.Sign, neon: true);
        }

        /// <summary>Decorations bought with the Decor upgrade, three levels, each a small group of objects.</summary>
        private static void BuildDecor(Scene scene, Transform parent, GameObject shop)
        {
            // Name plate over the shop door, always visible.
            if (shop != null)
            {
                var bounds = StationEditorUtility.GetBounds(shop);
                var plate = Create("ShopNamePlate", parent, Vector3.zero);
                plate.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.5f, bounds.min.z - 0.15f);
                plate.AddComponent<StationNameSign>().characterSize = 0.09f;
            }

            // Level 1: flags along the driveway and cacti in pots by the shop.
            var level1 = DecorLevel(scene, parent, 1);
            for (int i = 0; i < 4; i++)
                Place("Mini_Flags", scene, level1.transform, new Vector3(-30f + i * 20f, 0f, -14f), 0f);
            var pot = GetOrCreateMaterial("ClayPot", new Color(0.72f, 0.38f, 0.22f));
            foreach (float x in new[] { -6f, 6f })
            {
                Primitive(scene, level1.transform, "Pot", pot, new Vector3(x, 0.3f, 16.5f), new Vector3(0.9f, 0.6f, 0.9f));
                // Standing in the pot, not on the ground.
                StationEditorUtility.Place(StationEditorUtility.FindPrefab("Cactus_2"), scene, level1.transform,
                    new Vector3(x, 0.6f, 16.5f), 0f, 0.6f);
            }

            // Level 2: benches and flower beds.
            var level2 = DecorLevel(scene, parent, 2);
            var wood = GetOrCreateMaterial("BenchWood", new Color(0.55f, 0.36f, 0.2f));
            var soil = GetOrCreateMaterial("FlowerBed", new Color(0.25f, 0.45f, 0.18f));
            var petals = new[]
            {
                GetOrCreateMaterial("FlowerRed", new Color(0.9f, 0.2f, 0.25f)),
                GetOrCreateMaterial("FlowerYellow", new Color(0.95f, 0.85f, 0.2f)),
                GetOrCreateMaterial("FlowerPurple", new Color(0.6f, 0.3f, 0.85f))
            };
            foreach (float x in new[] { -14f, 14f })
            {
                Primitive(scene, level2.transform, "BenchSeat", wood, new Vector3(x, 0.45f, 16f), new Vector3(2.2f, 0.12f, 0.6f));
                Primitive(scene, level2.transform, "BenchBack", wood, new Vector3(x, 0.8f, 16.3f), new Vector3(2.2f, 0.6f, 0.1f));
                Primitive(scene, level2.transform, "BenchLegL", wood, new Vector3(x - 0.9f, 0.2f, 16f), new Vector3(0.1f, 0.4f, 0.5f));
                Primitive(scene, level2.transform, "BenchLegR", wood, new Vector3(x + 0.9f, 0.2f, 16f), new Vector3(0.1f, 0.4f, 0.5f));
            }

            foreach (float x in new[] { -20f, 20f })
            {
                Primitive(scene, level2.transform, "Bed", soil, new Vector3(x, 0.1f, -12f), new Vector3(4f, 0.2f, 1.2f));
                for (int i = 0; i < 8; i++)
                {
                    var flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    flower.name = "Flower";
                    SceneManager.MoveGameObjectToScene(flower, scene);
                    flower.transform.SetParent(level2.transform, true);
                    flower.transform.position = new Vector3(x - 1.7f + i * 0.5f, 0.35f, -12f + (i % 2 == 0 ? -0.25f : 0.25f));
                    flower.transform.localScale = Vector3.one * 0.3f;
                    Object.DestroyImmediate(flower.GetComponent<Collider>());
                    flower.GetComponent<Renderer>().sharedMaterial = petals[i % petals.Length];
                }
            }

            // Level 3: string lights along the front edge of the canopy.
            var level3 = DecorLevel(scene, parent, 3);
            var bulbs = new[]
            {
                GetOrCreateMaterial("BulbWarm", new Color(1f, 0.85f, 0.4f)),
                GetOrCreateMaterial("BulbRed", new Color(1f, 0.3f, 0.3f)),
                GetOrCreateMaterial("BulbBlue", new Color(0.3f, 0.6f, 1f))
            };
            for (int i = 0; i < 24; i++)
            {
                float x = -12f + i;
                float sag = Mathf.Sin(i / 23f * Mathf.PI) * 0.4f;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulb";
                SceneManager.MoveGameObjectToScene(bulb, scene);
                bulb.transform.SetParent(level3.transform, true);
                bulb.transform.position = new Vector3(x, 4.2f - sag, -8f);
                bulb.transform.localScale = Vector3.one * 0.18f;
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.GetComponent<Renderer>().sharedMaterial = bulbs[i % bulbs.Length];
            }

            foreach (float x in new[] { -6f, 6f })
                AddNightLight(level3, new Vector3(x, 3.8f, -8f), 8f, 1.5f, new Color(1f, 0.8f, 0.5f), neon: true);
        }

        private static GameObject DecorLevel(Scene scene, Transform parent, int level)
        {
            var root = Create($"Decor_Level{level}", parent, Vector3.zero);
            var group = Create("Decorations", root.transform, Vector3.zero);
            var construction = root.AddComponent<ConstructionSite>();
            construction.upgrade = UpgradeType.Decor;
            construction.requiredLevel = level;
            construction.building = group;
            construction.buildSeconds = 1.5f;
            construction.scaffolding = false;
            return group;
        }

        /// <summary>Buildings that appear when their upgrade is bought; until then a fenced-off site with cones.</summary>
        private static void BuildConstructionSites(Scene scene, Transform parent, List<GameObject> primary, List<GameObject> accent)
        {
            var (washBuilding, washSite) = ConstructionPair(scene, parent, "CarWash", UpgradeType.CarWash, 1, new Vector3(26f, 0f, 12f));
            primary.Add(Place("Station_Canopy_2", scene, washBuilding.transform, new Vector3(26f, 0f, 12f), 90f));
            SiteMarkers(scene, washSite.transform, new Vector3(26f, 0f, 12f), new Vector2(8f, 6f));

            var (tiresBuilding, tiresSite) = ConstructionPair(scene, parent, "TireService", UpgradeType.TireService, 1, new Vector3(30f, 0f, -9f));
            primary.Add(CreateShed(scene, tiresBuilding.transform, new Vector3(31f, 0f, -9f), new Vector3(6f, 3.2f, 5f)));
            for (int i = 0; i < 4; i++)
                Place("wheel", scene, tiresBuilding.transform, new Vector3(31f + 0.9f * i - 1.3f, 0f, -12.5f), 20f * i);
            SiteMarkers(scene, tiresSite.transform, new Vector3(30f, 0f, -9f), new Vector2(5f, 4f));

            var (motelBuilding, motelSite) = ConstructionPair(scene, parent, "Motel", UpgradeType.Motel, 1, new Vector3(-26f, 0f, 22f));
            primary.Add(Place("Operator 's_room_2", scene, motelBuilding.transform, new Vector3(-26f, 0f, 22f), 180f));
            SiteMarkers(scene, motelSite.transform, new Vector3(-26f, 0f, 22f), new Vector2(8f, 4f));

            var (parkingBuilding, parkingSite) = ConstructionPair(scene, parent, "TruckParking", UpgradeType.TruckParking, 1, new Vector3(25f, 0f, 24f));
            var paint = GetOrCreateMaterial("RoadMarking", new Color(0.95f, 0.95f, 0.9f));
            for (int i = 0; i <= 4; i++)
                Primitive(scene, parkingBuilding.transform, $"Line_{i}", paint, new Vector3(13f + i * 6f, 0.03f, 24f), new Vector3(0.15f, 0.02f, 5f));
            SiteMarkers(scene, parkingSite.transform, new Vector3(25f, 0f, 24f), new Vector2(12f, 2.5f));

            // Pumps 3 and 4 are installed by the ExtraPump upgrade (they still need a repair afterwards).
            foreach (var (level, z) in new[] { (1, -12f), (2, 12f) })
            {
                var (pumpBuilding, pumpSite) = ConstructionPair(scene, parent, $"Pump_{2 + level}", UpgradeType.ExtraPump, level, new Vector3(0f, 0f, z));
                accent.Add(Place("Petrol_pump_2", scene, pumpBuilding.transform, new Vector3(0f, 0f, z), 90f));
                SiteMarkers(scene, pumpSite.transform, new Vector3(0f, 0f, z), new Vector2(1.5f, 1.5f));
            }
        }

        /// <summary>A realtime point light that NightLight switches on after dusk.</summary>
        private static void AddNightLight(GameObject owner, Vector3 position, float range, float intensity, Color color,
            int renovationId = -1, bool neon = false)
        {
            if (owner == null)
                return;

            var go = new GameObject("NightLight");
            go.transform.SetParent(owner.transform, false);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.enabled = false;

            var night = go.AddComponent<NightLight>();
            night.renovationId = renovationId;
            night.neon = neon;
        }

        /// <summary>A point just under the top of an object, or the fallback when it is missing.</summary>
        /// <summary>Our price board by the entrance, facing the highway.</summary>
        private static void BuildPriceBoard(Scene scene, Transform parent)
        {
            var pole = GetOrCreateMaterial("SignPole", new Color(0.35f, 0.35f, 0.38f));
            var panel = GetOrCreateMaterial("PriceBoardPanel", new Color(0.08f, 0.08f, 0.1f));
            var root = Create("PriceBoard", parent, Vector3.zero);
            var position = new Vector3(-28f, 0f, -16f);
            Primitive(scene, root.transform, "Pole", pole, position + new Vector3(0f, 2f, 0f), new Vector3(0.25f, 4f, 0.25f));
            Primitive(scene, root.transform, "Panel", panel, position + new Vector3(0f, 4.6f, 0f), new Vector3(2.6f, 2f, 0.2f));

            var text = Create("Text", root.transform, position + new Vector3(0f, 4.6f, -0.12f));
            text.AddComponent<PriceBoard>();
        }

        /// <summary>
        /// PetroMax across the highway: an empty fenced lot that turns into a station on day 3 (CompetitorStation).
        /// </summary>
        private static void BuildCompetitor(Scene scene, Transform parent)
        {
            var center = new Vector3(25f, 0f, RoadZ - 26f);
            var root = Create("Competitor_PetroMax", parent, Vector3.zero);
            var emptyLot = Create("EmptyLot", root.transform, Vector3.zero);
            var station = Create("Station", root.transform, Vector3.zero);

            SiteMarkers(scene, emptyLot.transform, center, new Vector2(10f, 7f));

            var red = GetOrCreateMaterial("PetroMaxRed", new Color(0.8f, 0.1f, 0.1f));
            var white = GetOrCreateMaterial("PetroMaxWhite", new Color(0.92f, 0.92f, 0.9f));
            var dark = GetOrCreateMaterial("PriceBoardPanel", new Color(0.08f, 0.08f, 0.1f));
            var asphalt = GetOrCreateMaterial("OldAsphalt", new Color(0.28f, 0.27f, 0.26f));
            var brand = new List<GameObject>();

            Primitive(scene, station.transform, "Forecourt", asphalt, center + new Vector3(0f, 0.02f, 0f), new Vector3(30f, 0.04f, 18f));
            Place("Station_Canopy", scene, station.transform, center, 180f);
            Place("Petrol_pump", scene, station.transform, center + new Vector3(0f, 0f, -3f), 90f);
            Place("Petrol_pump", scene, station.transform, center + new Vector3(0f, 0f, 3f), 90f);

            // Shop: a white box with a red stripe, behind the canopy (away from the road).
            var shopCenter = center + new Vector3(0f, 0f, -12f);
            Primitive(scene, station.transform, "Shop", white, shopCenter + new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 6f));
            brand.Add(Primitive(scene, station.transform, "ShopStripe", red, shopCenter + new Vector3(0f, 3.4f, 3.05f), new Vector3(12.1f, 0.8f, 0.1f)));

            // Tall brand sign by the road with their price board.
            var signPosition = center + new Vector3(-13f, 0f, 8f);
            Primitive(scene, station.transform, "SignPole", white, signPosition + new Vector3(0f, 3.5f, 0f), new Vector3(0.35f, 7f, 0.35f));
            brand.Add(Primitive(scene, station.transform, "SignTop", red, signPosition + new Vector3(0f, 7.6f, 0f), new Vector3(3.2f, 1.2f, 0.3f)));
            Primitive(scene, station.transform, "SignPrices", dark, signPosition + new Vector3(0f, 5.6f, 0f), new Vector3(2.8f, 2.4f, 0.25f));
            // Faces the highway, i.e. +Z: the text is turned around.
            var prices = Create("Prices", station.transform, signPosition + new Vector3(0f, 5.6f, 0.15f));
            prices.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            prices.AddComponent<PriceBoard>().competitor = true;

            // Promo banner on the fence posts by the road.
            var banner = Create("PromoBanner", station.transform, Vector3.zero);
            var bannerPosition = center + new Vector3(8f, 0f, 9f);
            Primitive(scene, banner.transform, "PostL", white, bannerPosition + new Vector3(-2.2f, 1f, 0f), new Vector3(0.15f, 2f, 0.15f));
            Primitive(scene, banner.transform, "PostR", white, bannerPosition + new Vector3(2.2f, 1f, 0f), new Vector3(0.15f, 2f, 0.15f));
            Primitive(scene, banner.transform, "Cloth", GetOrCreateMaterial("PromoYellow", new Color(1f, 0.85f, 0.1f)),
                bannerPosition + new Vector3(0f, 1.6f, 0f), new Vector3(4.4f, 0.9f, 0.05f));

            var competitor = root.AddComponent<CompetitorStation>();
            competitor.emptyLot = emptyLot;
            competitor.station = station;
            competitor.promoBanner = banner;
            competitor.brandPanels = RenderersOf(brand);
            station.SetActive(false);
            banner.SetActive(false);
        }

        private static Vector3 TopOf(GameObject go, Vector3 fallback)
        {
            if (go == null)
                return fallback;

            var bounds = StationEditorUtility.GetBounds(go);
            return bounds.size == Vector3.zero ? fallback : new Vector3(bounds.center.x, bounds.max.y - 0.4f, bounds.center.z);
        }

        private static (GameObject broken, GameObject fixedState) RenovationPair(Scene scene, Transform parent, int id, string name)
        {
            var root = Create($"Renovation_{id}_{name}", parent, Vector3.zero);
            var broken = Create("Broken", root.transform, Vector3.zero);
            var fixedState = Create("Fixed", root.transform, Vector3.zero);
            var visual = root.AddComponent<RenovationVisual>();
            visual.id = id;
            visual.broken = broken;
            visual.fixedState = fixedState;
            return (broken, fixedState);
        }

        private static (GameObject building, GameObject site) ConstructionPair(Scene scene, Transform parent, string name,
            UpgradeType upgrade, int level, Vector3 position)
        {
            var root = Create($"Construction_{name}", parent, Vector3.zero);
            // The building wrapper sits on the ground so that scaling it grows the building upwards.
            var building = Create("Building", root.transform, new Vector3(position.x, 0f, position.z));
            var site = Create("Site", root.transform, Vector3.zero);
            var construction = root.AddComponent<ConstructionSite>();
            construction.upgrade = upgrade;
            construction.requiredLevel = level;
            construction.building = building;
            construction.site = site;
            return (building, site);
        }

        /// <summary>Cones at the corners and a heap of sand: an empty plot waiting for construction.</summary>
        private static void SiteMarkers(Scene scene, Transform parent, Vector3 center, Vector2 halfSize)
        {
            foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                Place("Conus", scene, parent, center + new Vector3(corner.x * halfSize.x, 0f, corner.y * halfSize.y), 0f);
            Place("Sand", scene, parent, center, 30f);
        }

        /// <summary>Simple open shed from primitives: back wall, two side walls and a roof.</summary>
        private static GameObject CreateShed(Scene scene, Transform parent, Vector3 center, Vector3 size)
        {
            var walls = GetOrCreateMaterial("ShedWalls", new Color(0.72f, 0.68f, 0.6f));
            var roof = GetOrCreateMaterial("ShedRoof", new Color(0.45f, 0.18f, 0.15f));
            var shed = Create("Tire_Shed", parent, Vector3.zero);
            Primitive(scene, shed.transform, "Back", walls, center + new Vector3(size.x / 2f, size.y / 2f, 0f), new Vector3(0.2f, size.y, size.z));
            Primitive(scene, shed.transform, "Left", walls, center + new Vector3(0f, size.y / 2f, -size.z / 2f), new Vector3(size.x, size.y, 0.2f));
            Primitive(scene, shed.transform, "Right", walls, center + new Vector3(0f, size.y / 2f, size.z / 2f), new Vector3(size.x, size.y, 0.2f));
            Primitive(scene, shed.transform, "Roof", roof, center + new Vector3(0f, size.y + 0.1f, 0f), new Vector3(size.x + 0.6f, 0.2f, size.z + 0.6f));
            return shed;
        }

        private static GameObject Primitive(Scene scene, Transform parent, string name, Material material, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void FenceAround(string[] prefabs, Scene scene, Transform parent, bool gaps)
        {
            TileLine(prefabs, scene, parent, new Vector3(-Lot.width / 2f, 0f, Lot.yMax), Vector3.right, Lot.width, gaps);
            TileLine(prefabs, scene, parent, new Vector3(Lot.xMin, 0f, Lot.yMin + 10f), Vector3.forward, Lot.height - 10f, gaps);
            TileLine(prefabs, scene, parent, new Vector3(Lot.xMax, 0f, Lot.yMin + 10f), Vector3.forward, Lot.height - 10f, gaps);
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

        private static Renderer[] RenderersOf(List<GameObject> objects)
        {
            var renderers = new List<Renderer>();
            foreach (var go in objects)
            {
                if (go != null)
                    renderers.AddRange(go.GetComponentsInChildren<Renderer>());
            }

            return renderers.ToArray();
        }

        private static GameObject CreateRestroomHut(Scene scene, Transform parent, Vector3 position)
        {
            var hut = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hut.name = "Restroom_Hut";
            SceneManager.MoveGameObjectToScene(hut, scene);
            hut.transform.SetParent(parent, false);
            hut.transform.localPosition = position + new Vector3(1.5f, 1.25f, 0f);
            hut.transform.localScale = new Vector3(3f, 2.5f, 3f);
            hut.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("RestroomWalls", new Color(0.75f, 0.78f, 0.8f));
            return hut;
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
            stationAuthoring.startPaintScheme = 0;
            station.AddComponent<TrashSpawnerAuthoring>().trashPrefabs = StationEditorUtility.FindTrashPrefabs();
            CreateBuildArea(parent);

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

            // Renovation points: stand here and press E. Ids match the RenovationVisual pairs in the main scene.
            var renovations = Create("Renovations", parent, Vector3.zero);
            AddRenovation(renovations.transform, RenovationKind.Windows, new Vector3(-3.5f, 0f, 16.5f));
            AddRenovation(renovations.transform, RenovationKind.Graffiti, new Vector3(-8.5f, 0f, 20f));
            AddRenovation(renovations.transform, RenovationKind.Fence, new Vector3(0f, 0f, 25f));
            AddRenovation(renovations.transform, RenovationKind.Lamps, new Vector3(-31f, 0f, -11f));
            AddRenovation(renovations.transform, RenovationKind.VendingMachine, new Vector3(10f, 0f, 16.5f));
            AddRenovation(renovations.transform, RenovationKind.Sign, new Vector3(-26f, 0f, -12.5f));

            // Visible deliveries: the tanker unloads by the pumps, the goods truck by the shop.
            var trucks = StationEditorUtility.GetOrCreateTruckPrefabs();
            var deliveries = Create("Deliveries", parent, Vector3.zero);
            var deliveriesAuthoring = deliveries.AddComponent<DeliveryTrucksAuthoring>();
            deliveriesAuthoring.fuelTruckPrefab = trucks.fuelTruck;
            deliveriesAuthoring.cargoTruckPrefab = trucks.cargoTruck;
            var fuelUnload = Create("FuelUnload", deliveries.transform, new Vector3(-18f, 0f, -8f));
            fuelUnload.transform.rotation = Quaternion.LookRotation(Vector3.right);
            deliveriesAuthoring.fuelUnloadPoint = fuelUnload.transform;
            var cargoUnload = Create("CargoUnload", deliveries.transform, new Vector3(-10f, 0f, 12f));
            cargoUnload.transform.rotation = Quaternion.LookRotation(Vector3.right);
            deliveriesAuthoring.cargoUnloadPoint = cargoUnload.transform;

            var shop = Create("Shop_Door", parent, new Vector3(0f, 0f, 17.5f));
            shop.AddComponent<ShopAuthoring>().pedestrianPrefab = StationEditorUtility.GetOrCreatePedestrianPrefab();

            var motel = Create("Motel_Reception", parent, new Vector3(-26f, 0f, 17.5f));
            var motelAuthoring = motel.AddComponent<MotelAuthoring>();
            motelAuthoring.entry = Create("Motel_Entry", parent, new Vector3(-12f, 0f, 9f)).transform;
            motelAuthoring.rooms = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                var room = Create($"Motel_Room_{i + 1}", parent, new Vector3(-34f + i * 4f, 0f, 13f));
                room.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                motelAuthoring.rooms[i] = room.transform;
            }

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

            var tires = Create("TireService_Bay", parent, new Vector3(26f, 0f, -8f));
            tires.transform.rotation = Quaternion.LookRotation(Vector3.right);
            tires.AddComponent<TireServiceAuthoring>().entryRoute = new[]
            {
                Create("Tires_Entry_0", parent, new Vector3(12f, 0f, -5f)).transform,
                Create("Tires_Entry_1", parent, new Vector3(18f, 0f, -8f)).transform
            };

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
                new(10f, 17f, 26f, 10f),   // truck parking and restroom
                new(8f, -13f, 26f, 9f),    // tire service lane
                new(-36f, 11f, 26f, 14f)   // motel parking
            };
            StationEditorUtility.ScatterTrash(scene, trash.transform, new Vector3(Lot.center.x, 0f, Lot.center.y),
                new Vector2(Lot.width / 2f - 2f, Lot.height / 2f - 2f), 45, 1234, keepOut);
        }

        /// <summary>
        /// Build mode area: the lot minus car lanes, pump islands and buildings. Free spots are the strips
        /// between the lanes, the front along the road and the corners of the lot.
        /// </summary>
        private static void CreateBuildArea(Transform parent)
        {
            var area = Create("BuildArea", parent, Vector3.zero).AddComponent<BuildAreaAuthoring>();
            area.lotMin = new Vector2(Lot.xMin, Lot.yMin);
            area.lotMax = new Vector2(Lot.xMax, Lot.yMax);
            area.noBuildZones = new[]
            {
                new BuildAreaAuthoring.Zone(-40f, -4f, 24f, 4f),     // queue and inner lanes
                new BuildAreaAuthoring.Zone(-40f, -11f, 24f, -8f),   // outer lane, pumps 1 and 3
                new BuildAreaAuthoring.Zone(-40f, 8f, 24f, 11f),     // outer lane, pumps 2 and 4
                new BuildAreaAuthoring.Zone(-2f, -13f, 2f, 13f),     // pump islands
                new BuildAreaAuthoring.Zone(-44f, -15f, -26f, 2f),   // entry from the highway
                new BuildAreaAuthoring.Zone(12f, -15f, 44f, -2f),    // exit and tire service
                new BuildAreaAuthoring.Zone(10f, 4f, 46f, 14f),      // car wash lane
                new BuildAreaAuthoring.Zone(-12f, 15f, 14f, 27f),    // shop and restroom
                new BuildAreaAuthoring.Zone(-36f, 11f, -16f, 19f),   // motel
                new BuildAreaAuthoring.Zone(14f, 16f, 36f, 27f),     // truck parking
                new BuildAreaAuthoring.Zone(-14f, 10f, -6f, 14f),    // cargo unloading
            };
        }

        private static void AddRenovation(Transform parent, RenovationKind kind, Vector3 position)
        {
            var point = Create($"Renovation_{kind}", parent, position);
            var renovation = point.AddComponent<RenovationAuthoring>();
            renovation.id = (int)kind;
            renovation.kind = kind;
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

        private static void TileLine(string[] prefabNames, Scene scene, Transform parent, Vector3 start, Vector3 direction, float totalLength,
            bool gaps = true)
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
                if (gaps && index % 4 == 3)
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
