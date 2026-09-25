using GasStation.Bridge;
using GasStation.Mono.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GasStation.Mono
{
    /// <summary>
    /// Loads and applies the player settings, creates the HUD, menus and audio, and attaches the
    /// day/night cycle, without manual scene setup.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            GameSettings.Load();
            SettingsApplier.ApplyAll();

            if (Object.FindAnyObjectByType<StationHud>() == null)
            {
                var root = new GameObject("GasStation UI");
                root.AddComponent<StationHud>();
                root.AddComponent<StationAudio>();
                root.AddComponent<MenuController>();
                root.AddComponent<Scenery.WeatherEffects>();
                root.AddComponent<Audio.RadioPlayer>();
                Object.DontDestroyOnLoad(root);
            }

            EnsureEventSystem();
            AttachDayNightCycle();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            AttachDayNightCycle();
        }

        /// <summary>Menus need an event system driven by the Input System package.</summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }

        private static void AttachDayNightCycle()
        {
            var sun = RenderSettings.sun;
            if (sun == null)
            {
                // Resources.FindObjectsOfTypeAll avoids the FindObjectsSortMode overloads deprecated in Unity 6.5+.
                foreach (var light in Resources.FindObjectsOfTypeAll<Light>())
                {
                    if (light.type == LightType.Directional && light.gameObject.scene.IsValid() && light.isActiveAndEnabled)
                    {
                        sun = light;
                        break;
                    }
                }
            }

            if (sun != null && sun.GetComponent<DayNightCycle>() == null)
                sun.gameObject.AddComponent<DayNightCycle>();
        }
    }
}
