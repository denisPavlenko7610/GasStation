using UnityEngine;

namespace GasStation.Bridge
{
    /// <summary>The office laptop is open: it pauses the game and takes Esc for itself.</summary>
    public static class LaptopState
    {
        public static bool IsOpen { get; private set; }

        /// <summary>Frame the laptop closed on, so the same Esc press does not also open the pause menu.</summary>
        public static int ClosedFrame { get; private set; } = -1;

        public static void SetOpen(bool open)
        {
            if (IsOpen && !open)
                ClosedFrame = Time.frameCount;
            IsOpen = open;
            // The game over screen keeps the game paused after the laptop closes.
            GamePause.SetMenuOpen(open || HudModel.GameOver);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => IsOpen = false;
    }
}
