using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace GasStation.Localization
{
    public enum GameLanguage
    {
        Russian = 0,
        English = 1
    }

    /// <summary>
    /// UI strings through Unity Localization (table "GasStation", locales "ru" and "en").
    /// Falls back to LocTable when the Localization Settings are not set up yet, so the game always has text.
    /// </summary>
    public static class Loc
    {
        private const string PrefsKey = "GasStation.Language";
        private const string MissingPrefix = "No translation found";

        private static GameLanguage? _fallbackLanguage;

        // The HUD asks for hundreds of strings every frame; table lookups are cached per language.
        private static readonly System.Collections.Generic.Dictionary<string, string>[] Cache =
        {
            new(), new()
        };

        public static GameLanguage Language
        {
            get
            {
                var locale = SelectedLocale();
                if (locale != null)
                    return locale.Identifier.Code.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                        ? GameLanguage.Russian
                        : GameLanguage.English;

                _fallbackLanguage ??= (GameLanguage)PlayerPrefs.GetInt(PrefsKey, (int)DefaultLanguage());
                return _fallbackLanguage.Value;
            }
        }

        public static string T(string key)
        {
            var language = Language;
            var cache = Cache[(int)language];
            if (cache.TryGetValue(key, out var cached))
                return cached;

            if (TryGetFromTable(key, out var value))
            {
                cache[key] = value;
                return value;
            }

            // Not cached: the table may still be loading, it will be asked again next time.
            return LocTable.Entries.TryGetValue(key, out var entry) ? entry[(int)language] : key;
        }

        /// <summary>Forget cached strings, e.g. after the translations were edited.</summary>
        public static void ClearCache()
        {
            foreach (var cache in Cache)
                cache.Clear();
        }

        public static string F(string key, params object[] args)
        {
            string format = T(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        public static void ToggleLanguage() =>
            SetLanguage(Language == GameLanguage.Russian ? GameLanguage.English : GameLanguage.Russian);

        public static void SetLanguage(GameLanguage language)
        {
            PlayerPrefs.SetInt(PrefsKey, (int)language);
            _fallbackLanguage = language;

            if (!LocalizationSettings.HasSettings)
                return;

            string code = language == GameLanguage.Russian ? "ru" : "en";
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                if (locale.Identifier.Code.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                {
                    LocalizationSettings.SelectedLocale = locale;
                    return;
                }
            }
        }

        /// <summary>Applies the language chosen in a previous session, if any.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreLanguage()
        {
            _fallbackLanguage = null;
            ClearCache();
            if (PlayerPrefs.HasKey(PrefsKey))
                SetLanguage((GameLanguage)PlayerPrefs.GetInt(PrefsKey));
        }

        private static bool TryGetFromTable(string key, out string value)
        {
            value = null;
            if (SelectedLocale() == null)
                return false;

            try
            {
                value = LocalizationSettings.StringDatabase.GetLocalizedString(LocTable.TableName, key);
            }
            catch (Exception)
            {
                return false;
            }

            return !string.IsNullOrEmpty(value) && !value.StartsWith(MissingPrefix, StringComparison.Ordinal);
        }

        private static Locale SelectedLocale()
        {
            if (!LocalizationSettings.HasSettings)
                return null;

            var operation = LocalizationSettings.SelectedLocaleAsync;
            return operation.IsDone ? operation.Result : null;
        }

        private static GameLanguage DefaultLanguage() =>
            Application.systemLanguage is SystemLanguage.Russian or SystemLanguage.Ukrainian or SystemLanguage.Belarusian
                ? GameLanguage.Russian
                : GameLanguage.English;
    }
}
