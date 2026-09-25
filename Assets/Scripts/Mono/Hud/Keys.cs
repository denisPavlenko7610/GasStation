using UnityEngine.InputSystem;

namespace GasStation.Mono.Hud
{
    /// <summary>Keyboard helpers shared by the HUD and build mode.</summary>
    public static class Keys
    {
        /// <summary>Digit 1..9 on the top row pressed this frame.</summary>
        public static bool DigitPressed(Keyboard keyboard, int digit) => digit switch
        {
            1 => keyboard.digit1Key.wasPressedThisFrame,
            2 => keyboard.digit2Key.wasPressedThisFrame,
            3 => keyboard.digit3Key.wasPressedThisFrame,
            4 => keyboard.digit4Key.wasPressedThisFrame,
            5 => keyboard.digit5Key.wasPressedThisFrame,
            6 => keyboard.digit6Key.wasPressedThisFrame,
            7 => keyboard.digit7Key.wasPressedThisFrame,
            8 => keyboard.digit8Key.wasPressedThisFrame,
            9 => keyboard.digit9Key.wasPressedThisFrame,
            _ => false
        };

        public static bool PlusPressed(Keyboard keyboard) =>
            keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame;

        public static bool MinusPressed(Keyboard keyboard) =>
            keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame;
    }
}
