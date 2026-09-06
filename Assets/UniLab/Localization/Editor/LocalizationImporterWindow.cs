#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniLab.Common.Utility;

namespace UniLab.Localization.Editor
{
    public class LocalizationImporterWindow : EditorWindow
    {
        private TextAsset _csvFile;
        private const string AssetPath = "Assets/UniLab/Resources/LocalizationData.asset";
        private const string KeyEnumPath = "Assets/Generated/UniLab/TextManager/LocalizationKeyEnum.cs";
        private const string LangEnumPath = "Assets/Generated/UniLab/TextManager/Language.cs";
        private const string GeneratedNamespace = "UniLab.Localization.Generated";
        private static readonly HashSet<string> CSharpKeywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte",
            "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
            "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile",
            "while"
        };

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

            var headers = lines[0].ParseCsvLine()
                .Skip(1) // Skip key column
                .Select(header => header.Trim())
                .Where(header => !string.IsNullOrEmpty(header))
                .ToList();
            if (headers.Count == 0)
            {
                Debug.LogError("Localization CSV must contain at least one language column.");
                return;
            }

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

                var key = columns[0].Trim();
                if (!keySet.Add(key))
                {
                    Debug.LogWarning($"Duplicate localization key skipped: {key}");
                    continue;
                }

                var entry = new LocalizationEntry
                {
                    Key = key,
                    Hash = KeyHash.Fnv1AHash(key),
                    Values = new List<string>()
                };

                for (var j = 1; j < headers.Count + 1; j++)
                {
                    entry.Values.Add(j < columns.Count ? columns[j].Trim() : "");
                }

                entries.Add(entry);
            }

            var asset = CreateInstance<LocalizationData>();
            asset.Languages = headers;
            asset.Entries = entries;

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath) ?? string.Empty);
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.CreateAsset(asset, AssetPath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            if (!asset.Validate(out var issues))
            {
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"Localization import validation: {issue}");
                }
            }

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
            sb.AppendLine("namespace " + GeneratedNamespace);
            sb.AppendLine("{");
            sb.AppendLine("    public enum " + enumName);
            sb.AppendLine("    {");

            var usedNames = new HashSet<string>();
            foreach (var item in items)
            {
                var safe = CreateEnumMemberName(item, usedNames);
                sb.AppendLine("        " + safe + ",");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            File.WriteAllText(outputPath, sb.ToString());
        }

        private static string CreateEnumMemberName(string item, HashSet<string> usedNames)
        {
            var sb = new StringBuilder();
            foreach (var c in item)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            var safe = sb.ToString().Trim('_');
            if (string.IsNullOrEmpty(safe))
            {
                safe = "Value";
            }

            if (!(char.IsLetter(safe[0]) || safe[0] == '_'))
            {
                safe = "_" + safe;
            }

            if (CSharpKeywords.Contains(safe))
            {
                safe = "_" + safe;
            }

            var unique = safe;
            var suffix = 2;
            while (!usedNames.Add(unique))
            {
                unique = $"{safe}_{suffix}";
                suffix++;
            }

            return unique;
        }
    }
}
#endif
