using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniLab.AI.Editor
{
    /// <summary>
    /// 見た目の崩れを構造差分ではなく画像差分で拾い、人の目視確認を PR 差分へ置き換える。
    /// </summary>
    public static class VisualRegression
    {
        private const string OutputDirectoryName = "visual-regression";
        private const string IgnoreFileName = "ignore.json";
        private const string ReportFileName = "report.json";
        private const string CaptureExtension = ".png";
        private const string TimestampFormat = "yyyyMMdd-HHmmss";
        private const string PassStatus = "pass";
        private const string FailStatus = "fail";
        private const string NoBaselineStatus = "no-baseline";
        private const string SizeMismatchStatus = "size-mismatch";

        /// <summary>
        /// 実画像の束を一括比較し、後段がレポートパスだけで結果へ到達できるようにする。
        /// </summary>
        public static string Compare(string capturesDirectory, string baselinesDirectory, VisualRegressionOptions options)
        {
            if (string.IsNullOrEmpty(capturesDirectory))
            {
                throw new ArgumentException("capturesDirectory は必須です。", nameof(capturesDirectory));
            }

            if (string.IsNullOrEmpty(baselinesDirectory))
            {
                throw new ArgumentException("baselinesDirectory は必須です。", nameof(baselinesDirectory));
            }

            if (!Directory.Exists(capturesDirectory))
            {
                throw new DirectoryNotFoundException($"capturesDirectory が見つかりません。 path={capturesDirectory}");
            }

            Directory.CreateDirectory(baselinesDirectory);

            var comparisonOptions = options ?? new VisualRegressionOptions();
            var runTimestamp = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            var outputDirectory = Path.Combine(DebugOutputPath.DirectoryPath, OutputDirectoryName, runTimestamp);
            Directory.CreateDirectory(outputDirectory);

            var ignoreSettings = VisualRegressionIgnoreParser.ParseFile(Path.Combine(baselinesDirectory, IgnoreFileName));
            var captureFilePaths = Directory.GetFiles(capturesDirectory, $"*{CaptureExtension}", SearchOption.TopDirectoryOnly);
            Array.Sort(captureFilePaths, StringComparer.Ordinal);

            var results = new List<VisualRegressionResult>(captureFilePaths.Length);
            var passCount = 0;
            var failCount = 0;
            var noBaselineCount = 0;
            var sizeMismatchCount = 0;

            for (var fileIndex = 0; fileIndex < captureFilePaths.Length; fileIndex++)
            {
                var captureFilePath = captureFilePaths[fileIndex];
                var captureName = Path.GetFileNameWithoutExtension(captureFilePath);
                var actualOutputPath = Path.Combine(outputDirectory, $"{captureName}-actual{CaptureExtension}");
                File.Copy(captureFilePath, actualOutputPath, true);

                var baselinePath = Path.Combine(baselinesDirectory, $"{captureName}{CaptureExtension}");
                if (!File.Exists(baselinePath))
                {
                    noBaselineCount++;
                    results.Add(new VisualRegressionResult(captureName, NoBaselineStatus, "ベースライン画像が存在しません。", actualOutputPath, baselinePath, string.Empty, 0.0f, 0, 0, 0));
                    continue;
                }

                var actualTexture = LoadTexture(captureFilePath);
                var baselineTexture = LoadTexture(baselinePath);
                try
                {
                    if (actualTexture.width != baselineTexture.width || actualTexture.height != baselineTexture.height)
                    {
                        sizeMismatchCount++;
                        results.Add(new VisualRegressionResult(captureName, SizeMismatchStatus, $"解像度が一致しません。 actual={actualTexture.width}x{actualTexture.height} baseline={baselineTexture.width}x{baselineTexture.height}", actualOutputPath, baselinePath, string.Empty, 1.0f, 0, 0, 0));
                        continue;
                    }

                    var comparisonResult = CompareTexturePair(captureName, actualTexture, baselineTexture, actualOutputPath, baselinePath, outputDirectory, ResolveIgnoreRects(ignoreSettings, captureName), comparisonOptions);
                    results.Add(comparisonResult);
                    if (comparisonResult.status == PassStatus)
                    {
                        passCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(actualTexture);
                    UnityEngine.Object.DestroyImmediate(baselineTexture);
                }
            }

            var report = new VisualRegressionReport(
                capturesDirectory,
                baselinesDirectory,
                outputDirectory,
                DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
                results.ToArray(),
                passCount,
                failCount,
                noBaselineCount,
                sizeMismatchCount);

            var reportPath = Path.Combine(outputDirectory, ReportFileName);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            return reportPath;
        }

        /// <summary>
        /// 意図した見た目変更をまとめて反映し、PR へベースライン更新を同梱しやすくする。
        /// </summary>
        public static void AcceptAll(string capturesDirectory, string baselinesDirectory)
        {
            if (string.IsNullOrEmpty(capturesDirectory) || string.IsNullOrEmpty(baselinesDirectory))
            {
                throw new ArgumentException("capturesDirectory と baselinesDirectory は必須です。");
            }

            Directory.CreateDirectory(baselinesDirectory);
            var captureFilePaths = Directory.GetFiles(capturesDirectory, $"*{CaptureExtension}", SearchOption.TopDirectoryOnly);
            Array.Sort(captureFilePaths, StringComparer.Ordinal);

            for (var fileIndex = 0; fileIndex < captureFilePaths.Length; fileIndex++)
            {
                var captureFilePath = captureFilePaths[fileIndex];
                var baselinePath = Path.Combine(baselinesDirectory, Path.GetFileName(captureFilePath));
                File.Copy(captureFilePath, baselinePath, true);
            }
        }

        /// <summary>
        /// 失敗 1 枚だけを局所的に受け入れ、他のベースラインを不用意に更新しないようにする。
        /// </summary>
        public static void Accept(string captureName, string capturesDirectory, string baselinesDirectory)
        {
            if (string.IsNullOrEmpty(captureName))
            {
                throw new ArgumentException("captureName は必須です。", nameof(captureName));
            }

            if (string.IsNullOrEmpty(capturesDirectory) || string.IsNullOrEmpty(baselinesDirectory))
            {
                throw new ArgumentException("capturesDirectory と baselinesDirectory は必須です。");
            }

            var sourcePath = Path.Combine(capturesDirectory, $"{captureName}{CaptureExtension}");
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"capture が見つかりません。 name={captureName}", sourcePath);
            }

            Directory.CreateDirectory(baselinesDirectory);
            var destinationPath = Path.Combine(baselinesDirectory, $"{captureName}{CaptureExtension}");
            File.Copy(sourcePath, destinationPath, true);
        }

        private static VisualRegressionResult CompareTexturePair(string captureName, Texture2D actualTexture, Texture2D baselineTexture, string actualOutputPath, string baselinePath, string outputDirectory, VisualRegressionIgnoreRect[] ignoreRects, VisualRegressionOptions options)
        {
            var scaledActual = Downscale(actualTexture, Mathf.Max(1, options.downscaleDivisor));
            var scaledBaseline = Downscale(baselineTexture, Mathf.Max(1, options.downscaleDivisor));
            var diffTexture = new Texture2D(scaledActual.width, scaledActual.height, TextureFormat.RGBA32, false, false);
            try
            {
                var changedPixelCount = 0;
                var comparedPixelCount = 0;
                var ignoredPixelCount = 0;
                for (var y = 0; y < scaledActual.height; y++)
                {
                    for (var x = 0; x < scaledActual.width; x++)
                    {
                        if (IsIgnored(x, y, scaledActual.width, scaledActual.height, actualTexture.width, actualTexture.height, ignoreRects))
                        {
                            diffTexture.SetPixel(x, y, new Color32(0, 0, 0, 0));
                            ignoredPixelCount++;
                            continue;
                        }

                        comparedPixelCount++;
                        var actualColor = (Color32)scaledActual.GetPixel(x, y);
                        var baselineColor = (Color32)scaledBaseline.GetPixel(x, y);
                        var difference = GetMaxDifference(actualColor, baselineColor);
                        if (difference > options.differenceThreshold)
                        {
                            diffTexture.SetPixel(x, y, new Color32(255, 0, 0, 255));
                            changedPixelCount++;
                            continue;
                        }

                        diffTexture.SetPixel(x, y, new Color32(actualColor.r, actualColor.g, actualColor.b, options.unchangedAlpha));
                    }
                }

                diffTexture.Apply(false, false);
                var differenceRatio = comparedPixelCount <= 0 ? 0.0f : (float)changedPixelCount / comparedPixelCount;
                var diffPath = Path.Combine(outputDirectory, $"{captureName}-diff{CaptureExtension}");
                File.WriteAllBytes(diffPath, diffTexture.EncodeToPNG());

                var status = differenceRatio <= options.allowedDifferenceRatio ? PassStatus : FailStatus;
                var message = status == PassStatus
                    ? "許容差分内です。"
                    : $"変化率が許容値を超えました。 ratio={differenceRatio:P3} threshold={options.allowedDifferenceRatio:P3}";
                return new VisualRegressionResult(captureName, status, message, actualOutputPath, baselinePath, diffPath, differenceRatio, changedPixelCount, comparedPixelCount, ignoredPixelCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scaledActual);
                UnityEngine.Object.DestroyImmediate(scaledBaseline);
                UnityEngine.Object.DestroyImmediate(diffTexture);
            }
        }

        private static Texture2D LoadTexture(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"画像の読み込みに失敗しました。 path={filePath}");
            }

            return texture;
        }

        private static Texture2D Downscale(Texture2D sourceTexture, int downscaleDivisor)
        {
            var targetWidth = Mathf.Max(1, Mathf.CeilToInt((float)sourceTexture.width / downscaleDivisor));
            var targetHeight = Mathf.Max(1, Mathf.CeilToInt((float)sourceTexture.height / downscaleDivisor));
            var targetTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false);

            for (var targetY = 0; targetY < targetHeight; targetY++)
            {
                for (var targetX = 0; targetX < targetWidth; targetX++)
                {
                    var sourceStartX = targetX * downscaleDivisor;
                    var sourceStartY = targetY * downscaleDivisor;
                    var sourceEndX = Mathf.Min(sourceStartX + downscaleDivisor, sourceTexture.width);
                    var sourceEndY = Mathf.Min(sourceStartY + downscaleDivisor, sourceTexture.height);

                    var pixelCount = 0;
                    var red = 0;
                    var green = 0;
                    var blue = 0;
                    var alpha = 0;
                    for (var sourceY = sourceStartY; sourceY < sourceEndY; sourceY++)
                    {
                        for (var sourceX = sourceStartX; sourceX < sourceEndX; sourceX++)
                        {
                            var color = (Color32)sourceTexture.GetPixel(sourceX, sourceY);
                            red += color.r;
                            green += color.g;
                            blue += color.b;
                            alpha += color.a;
                            pixelCount++;
                        }
                    }

                    if (pixelCount <= 0)
                    {
                        targetTexture.SetPixel(targetX, targetY, new Color32(0, 0, 0, 0));
                        continue;
                    }

                    targetTexture.SetPixel(targetX, targetY, new Color32(
                        (byte)(red / pixelCount),
                        (byte)(green / pixelCount),
                        (byte)(blue / pixelCount),
                        (byte)(alpha / pixelCount)));
                }
            }

            targetTexture.Apply(false, false);
            return targetTexture;
        }

        private static int GetMaxDifference(Color32 left, Color32 right)
        {
            var redDifference = Mathf.Abs(left.r - right.r);
            var greenDifference = Mathf.Abs(left.g - right.g);
            var blueDifference = Mathf.Abs(left.b - right.b);
            return Mathf.Max(redDifference, Mathf.Max(greenDifference, blueDifference));
        }

        private static VisualRegressionIgnoreRect[] ResolveIgnoreRects(VisualRegressionIgnoreSettings ignoreSettings, string captureName)
        {
            if (ignoreSettings == null || ignoreSettings.captures == null)
            {
                return Array.Empty<VisualRegressionIgnoreRect>();
            }

            for (var regionIndex = 0; regionIndex < ignoreSettings.captures.Length; regionIndex++)
            {
                var region = ignoreSettings.captures[regionIndex];
                if (region == null || region.captureName != captureName)
                {
                    continue;
                }

                return region.rects ?? Array.Empty<VisualRegressionIgnoreRect>();
            }

            return Array.Empty<VisualRegressionIgnoreRect>();
        }

        private static bool IsIgnored(int scaledX, int scaledY, int scaledWidth, int scaledHeight, int sourceWidth, int sourceHeight, VisualRegressionIgnoreRect[] ignoreRects)
        {
            if (ignoreRects == null || ignoreRects.Length == 0)
            {
                return false;
            }

            var scaleX = (float)sourceWidth / scaledWidth;
            var scaleY = (float)sourceHeight / scaledHeight;
            var sourceX = (scaledX + 0.5f) * scaleX;
            var sourceY = (scaledY + 0.5f) * scaleY;

            for (var rectIndex = 0; rectIndex < ignoreRects.Length; rectIndex++)
            {
                var ignoreRect = ignoreRects[rectIndex];
                if (ignoreRect == null)
                {
                    continue;
                }

                var minX = ignoreRect.x;
                var maxX = ignoreRect.x + ignoreRect.width;
                var minY = ignoreRect.y;
                var maxY = ignoreRect.y + ignoreRect.height;
                if (sourceX >= minX && sourceX < maxX && sourceY >= minY && sourceY < maxY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
