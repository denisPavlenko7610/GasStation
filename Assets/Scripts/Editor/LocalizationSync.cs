using GasStation.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace GasStation.Editor
{
    /// <summary>
    /// Copies LocTable (the source of truth for texts) into the "GasStation" string tables. The game reads the
    /// string tables first, so edited texts only show up after this sync.
    /// </summary>
    public static class LocalizationSync
    {
        [MenuItem("GasStation/Sync localization tables")]
        public static void Sync()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocTable.TableName);
            if (collection == null)
            {
                Debug.LogError($"GasStation: string table collection '{LocTable.TableName}' not found.");
                return;
            }

            int changed = 0;
            foreach (var table in collection.StringTables)
            {
                int language = table.LocaleIdentifier.Code.StartsWith("ru") ? (int)GameLanguage.Russian : (int)GameLanguage.English;
                foreach (var pair in LocTable.Entries)
                {
                    string value = pair.Value[language];
                    var entry = table.GetEntry(pair.Key);
                    if (entry != null && entry.Value == value)
                        continue;

                    table.AddEntry(pair.Key, value);
                    changed++;
                }

                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log($"GasStation: localization tables synced, {changed} strings updated.");
        }
    }
}
