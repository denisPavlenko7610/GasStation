using GasStation.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace GasStation.Editor
{
    /// <summary>
    /// Sets up Unity Localization for the game: Localization Settings, the English and Russian locales and
    /// the "GasStation" string table collection filled from LocTable. Safe to run again: it only adds and
    /// updates entries, so translations edited in the Localization Tables window survive unless LocTable
    /// changes the same key.
    /// </summary>
    public static class LocalizationSetup
    {
        private const string Root = "Assets/Localization";
        private const string LocalesFolder = Root + "/Locales";
        private const string TablesFolder = Root + "/Tables";

        [MenuItem("GasStation/Localization/Setup (English, Russian)")]
        private static void Setup()
        {
            StationEditorUtility.EnsureFolder(LocalesFolder);
            StationEditorUtility.EnsureFolder(TablesFolder);

            var settings = GetOrCreateSettings();
            var english = GetOrCreateLocale("en", "English");
            var russian = GetOrCreateLocale("ru", "Russian");

            var startup = settings.GetStartupLocaleSelectors();
            if (!startup.Exists(selector => selector is SystemLocaleSelector))
                startup.Insert(0, new SystemLocaleSelector());
            if (!startup.Exists(selector => selector is SpecificLocaleSelector))
                startup.Add(new SpecificLocaleSelector { LocaleId = english.Identifier });

            var collection = LocalizationEditorSettings.GetStringTableCollection(LocTable.TableName)
                             ?? LocalizationEditorSettings.CreateStringTableCollection(LocTable.TableName, TablesFolder);

            var englishTable = GetOrAddTable(collection, english);
            var russianTable = GetOrAddTable(collection, russian);

            foreach (var entry in LocTable.Entries)
            {
                russianTable.AddEntry(entry.Key, entry.Value[(int)GameLanguage.Russian]);
                englishTable.AddEntry(entry.Key, entry.Value[(int)GameLanguage.English]);
            }

            // Tables are small: preload them so the first frame already has text.
            LocalizationEditorSettings.SetPreloadTableFlag(englishTable, true);
            LocalizationEditorSettings.SetPreloadTableFlag(russianTable, true);

            EditorUtility.SetDirty(englishTable);
            EditorUtility.SetDirty(russianTable);
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"GasStation: Unity Localization is set up — locales en/ru, table '{LocTable.TableName}' " +
                      $"with {LocTable.Entries.Count} entries. Press L in game to switch the language.");
        }

        private static LocalizationSettings GetOrCreateSettings()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "Localization Settings";
            AssetDatabase.CreateAsset(settings, $"{Root}/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            return settings;
        }

        private static Locale GetOrCreateLocale(string code, string name)
        {
            var existing = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
            if (existing != null)
                return existing;

            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = $"{name} ({code})";
            AssetDatabase.CreateAsset(locale, $"{LocalesFolder}/{name} ({code}).asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static StringTable GetOrAddTable(StringTableCollection collection, Locale locale)
        {
            if (collection.GetTable(locale.Identifier) is StringTable table)
                return table;

            return (StringTable)collection.AddNewTable(locale.Identifier);
        }
    }
}
