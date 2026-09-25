using GasStation.Localization;
using UnityEngine;

namespace GasStation.Bridge
{
    /// <summary>The player's station name, shown on the sign and in the HUD. Saved with the game.</summary>
    public static class StationProfile
    {
        public const int MaxNameLength = 24;

        private static string _name;

        /// <summary>Changes every time the name changes, so views can refresh cheaply.</summary>
        public static int Version { get; private set; }

        /// <summary>The custom name, or the localized default when none was set.</summary>
        public static string DisplayName => string.IsNullOrWhiteSpace(_name) ? Loc.T("station.defaultName") : _name;

        /// <summary>The custom name only (null when the default is used); this is what is saved.</summary>
        public static string CustomName => _name;

        /// <summary>The station cat's name (null = the localized default). Saved with the game.</summary>
        public static string CatName => string.IsNullOrWhiteSpace(_catName) ? Loc.T("cat.defaultName") : _catName;

        public static string CustomCatName => _catName;

        private static string _catName;

        public static void SetCatName(string name)
        {
            name = name?.Trim();
            if (name != null && name.Length > MaxNameLength)
                name = name.Substring(0, MaxNameLength);
            _catName = string.IsNullOrEmpty(name) ? null : name;
            Version++;
        }

        public static void SetName(string name)
        {
            name = name?.Trim();
            if (name != null && name.Length > MaxNameLength)
                name = name.Substring(0, MaxNameLength);
            _name = string.IsNullOrEmpty(name) ? null : name;
            Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _name = null;
            _catName = null;
            Version++;
        }
    }
}
