using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniLab.AssetDelivery.Sample.Editor
{
    /// <summary>
    /// Generates Addressables sample assets required by the Asset Delivery sample scene.
    /// </summary>
    public static class AssetDeliverySampleAssetGenerator
    {
        private const string MenuPath = "UniLab/AssetDelivery/Sample/Generate Placeholder Asset";
        private const string Address = "sample_sprite";
        private const string Label = "sample";
        private const string GeneratedDirectory = "Assets/UniLab/AssetDelivery/Sample/Generated";
        private const string SpriteAssetPath = GeneratedDirectory + "/sample_sprite.png";
        private const int TextureSize = 256;
        private const int CheckerSize = 32;
        private const float FirstCheckerColorRed = 0.1f;
        private const float FirstCheckerColorGreen = 0.45f;
        private const float FirstCheckerColorBlue = 0.95f;
        private const float SecondCheckerColorRed = 0.95f;
        private const float SecondCheckerColorGreen = 0.85f;
        private const float SecondCheckerColorBlue = 0.2f;
        private const float CheckerColorAlpha = 1.0f;
        private const string SettingsMissingMessage = "Addressables settings could not be created.";
        private const string SuccessMessageFormat = "Generated Asset Delivery sample placeholder at {0}. Address: {1}, Label: {2}";

        private static readonly Color FirstCheckerColor = new Color(FirstCheckerColorRed, FirstCheckerColorGreen, FirstCheckerColorBlue, CheckerColorAlpha);
        private static readonly Color SecondCheckerColor = new Color(SecondCheckerColorRed, SecondCheckerColorGreen, SecondCheckerColorBlue, CheckerColorAlpha);

        /// <summary>
        /// Generates the Addressables placeholder sprite used by the Asset Delivery sample.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void GeneratePlaceholderAsset()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError(SettingsMissingMessage);
                return;
            }

            EnsureGeneratedDirectoryExists();
            WritePlaceholderTexture();
            ConfigureSpriteImporter();
            RegisterAddressableEntry(settings);
            SelectFastPlayMode(settings);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(string.Format(SuccessMessageFormat, SpriteAssetPath, Address, Label));
        }

        private static void EnsureGeneratedDirectoryExists()
        {
            if (Directory.Exists(GeneratedDirectory))
            {
                return;
            }

            Directory.CreateDirectory(GeneratedDirectory);
            AssetDatabase.Refresh();
        }

        private static void WritePlaceholderTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize);
            for (var pixelY = 0; pixelY < TextureSize; pixelY++)
            {
                for (var pixelX = 0; pixelX < TextureSize; pixelX++)
                {
                    texture.SetPixel(pixelX, pixelY, GetCheckerColor(pixelX, pixelY));
                }
            }

            texture.Apply();
            File.WriteAllBytes(SpriteAssetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SpriteAssetPath);
        }

        private static Color GetCheckerColor(int pixelX, int pixelY)
        {
            var columnIndex = pixelX / CheckerSize;
            var rowIndex = pixelY / CheckerSize;
            var useFirstColor = (columnIndex + rowIndex) % 2 == 0;
            if (useFirstColor)
            {
                return FirstCheckerColor;
            }

            return SecondCheckerColor;
        }

        private static void ConfigureSpriteImporter()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpriteAssetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        private static void RegisterAddressableEntry(AddressableAssetSettings settings)
        {
            var guid = AssetDatabase.AssetPathToGUID(SpriteAssetPath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = Address;
            entry.SetLabel(Label, true, true);
        }

        private static void SelectFastPlayMode(AddressableAssetSettings settings)
        {
            for (var i = 0; i < settings.DataBuilders.Count; i++)
            {
                if (settings.DataBuilders[i] is not BuildScriptFastMode)
                {
                    continue;
                }

                settings.ActivePlayModeDataBuilderIndex = i;
                return;
            }
        }
    }
}
