using UnityEngine;
using UnityEngine.SceneManagement;

namespace GasStation.Mono
{
    /// <summary>Creates the HUD and audio, and attaches the day/night cycle, without manual scene setup.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindAnyObjectByType<StationHud>() == null)
            {
                var root = new GameObject("StationHud");
                root.AddComponent<StationHud>();
                root.AddComponent<StationAudio>();
                Object.DontDestroyOnLoad(root);
            }

            AttachDayNightCycle();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachDayNightCycle();

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
