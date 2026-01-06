#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniLab.Common.Utility;

namespace UniLab.TextManager.Editor
{
    public class LocalizationImporterWindow : EditorWindow
    {
        private TextAsset _csvFile;
        private const string AssetPath = "Assets/Resources/LocalizationData.asset";
        private const string KeyEnumPath = "Assets/Generated/UniLab/TextManager/LocalizationKeyEnum.cs";
        private const string LangEnumPath = "Assets/Generated/UniLab/TextManager/Language.cs";

        [MenuItem("UniLab/TextManager/Import CSV")]
        public static void ShowWindow()
        {
            GetWindow<LocalizationImporterWindow>("Import Localization CSV");
        }

        private void OnGUI()
        {
            GUILayout.Label("Localization CSV Importer", EditorStyles.boldLabel);
            _csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", _csvFile, typeof(TextAsset), false);

            if (GUILayout.Button("Import and Generate") && _csvFile != null)
            {
                ImportAndGenerate(_csvFile);
            }
        }

        private void ImportAndGenerate(TextAsset csv)
        {
            var lines = csv.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return;
            }

            var headers = lines[0].ParseCsvLine().Skip(1).ToList(); // Skip key column
            var entries = new List<LocalizationEntry>();
            var keySet = new SortedSet<string>();

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("//") || string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var columns = line.ParseCsvLine();
                if (columns.Count < 1 || string.IsNullOrEmpty(columns[0].Trim()))
                {
                    continue;
                }

                var entry = new LocalizationEntry
                {
                    Key = columns[0].Trim(),
                    Hash = KeyHash.Fnv1AHash(columns[0].Trim()),
                    Values = new List<string>()
                };

                for (var j = 1; j < headers.Count + 1; j++)
                {
                    entry.Values.Add(j < columns.Count ? columns[j].Trim() : "");
                }

                entries.Add(entry);
                keySet.Add(entry.Key);
            }

            var asset = CreateInstance<LocalizationData>();
            asset.Languages = headers;
            asset.Entries = entries;

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath) ?? string.Empty);
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.CreateAsset(asset, AssetPath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            const string comment = "// Auto Generate\n// UniLab->TextManager->Import CSV";
            GenerateEnum("LocalizationKeyEnum", KeyEnumPath, comment, keySet);
            GenerateEnum("Language", LangEnumPath, comment, new SortedSet<string>(headers));
            AssetDatabase.Refresh();
            Debug.Log("Localization Data, Key Enum, and Language Enum generated successfully.");
        }

        private void GenerateEnum(string enumName, string outputPath, string comment, SortedSet<string> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine(comment);
            sb.AppendLine("public enum " + enumName);
            sb.AppendLine("{");

            foreach (var item in items)
            {
                var safe = item.Replace(" ", "_").Replace("-", "_");
                if (char.IsDigit(safe[0])) safe = "_" + safe;
                sb.AppendLine("    " + safe + ",");
            }

            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}
#endif