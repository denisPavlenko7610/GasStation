using UnityEngine;

namespace GasStation.Bridge
{
    /// <summary>The office laptop is open: it pauses the game and takes Esc for itself.</summary>
    public static class LaptopState
    {
        public static bool IsOpen { get; private set; }

        public static void SetOpen(bool open)
        {
            IsOpen = open;
            // The game over screen keeps the game paused after the laptop closes.
            GamePause.SetMenuOpen(open || HudModel.GameOver);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => IsOpen = false;
    }
}
