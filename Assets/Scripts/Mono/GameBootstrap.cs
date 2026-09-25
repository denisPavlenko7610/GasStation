using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>Creates the HUD and attaches the day/night cycle without manual scene setup.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindFirstObjectByType<StationHud>() == null)
            {
                var hud = new GameObject("StationHud");
                hud.AddComponent<StationHud>();
                Object.DontDestroyOnLoad(hud);
            }

            var sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.type == LightType.Directional)
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
