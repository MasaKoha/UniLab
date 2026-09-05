#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UniLab.AI
{
    /// <summary>単独撮影と観測付き撮影の発行・完了待ち・画像解析を共有します。</summary>
    internal static class AiCaptureSupport
    {
        private const float CaptureTimeoutSeconds = 3f;
        private const int InitialTextureSize = 2;
        private const double BlankDeviationThreshold = 3.0;
        private const double RedLuminanceWeight = 0.2126;
        private const double GreenLuminanceWeight = 0.7152;
        private const double BlueLuminanceWeight = 0.0722;

        /// <summary>撮影名のディレクトリ逸脱を撮影前に拒否します。</summary>
        internal static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"\A[A-Za-z0-9_-]+\z"))
            {
                throw new ArgumentException("name は英数字・_・- のみで必ず指定してください。");
            }
        }

        /// <summary>現在フレームの撮影を予約し、生成予定の絶対パスを返します。</summary>
        internal static string Request(string name, string outputDirectory)
        {
            ValidateName(name);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var directory = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(DebugOutputPath.DirectoryPath, "captures")
                : Path.GetFullPath(Path.Combine(projectRoot, outputDirectory));
            Directory.CreateDirectory(directory);
            var path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
            // 前回のファイルを今回の撮影完了と誤認しないようにする。
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ScreenCapture.CaptureScreenshot(path);
            return path;
        }

        /// <summary>PNG が読み取れるまで待ち、既存の観測本文を保ったまま撮影結果を埋めます。</summary>
        internal static IEnumerator<object> CompleteAsync(AiCommandResponse response)
        {
            var startedAt = Time.realtimeSinceStartup;
            do
            {
                yield return null;
                var completed = false;
                try
                {
                    completed = TryReadImage(response);
                }
                catch (Exception exception)
                {
                    response.ok = false;
                    response.error = exception.Message;
                }

                if (!response.ok)
                {
                    yield break;
                }

                if (completed)
                {
                    response.settled = true;
                    yield break;
                }
            }
            while (Time.realtimeSinceStartup - startedAt < CaptureTimeoutSeconds);

            response.ok = false;
            response.settled = false;
            response.error = "capture timeout";
        }

        /// <summary>RGB の輝度の母標準偏差を 0〜255 の尺度で計算します。</summary>
        internal static double ComputeLuminanceDeviation(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return 0.0;
            }

            var mean = 0.0;
            var squaredDifferenceSum = 0.0;
            var sampleCount = 0;
            foreach (var pixel in pixels)
            {
                var luminance = pixel.r * RedLuminanceWeight + pixel.g * GreenLuminanceWeight + pixel.b * BlueLuminanceWeight;
                sampleCount++;
                var difference = luminance - mean;
                mean += difference / sampleCount;
                squaredDifferenceSum += difference * (luminance - mean);
            }

            return Math.Sqrt(Math.Max(0.0, squaredDifferenceSum / sampleCount));
        }

        private static bool TryReadImage(AiCommandResponse response)
        {
            if (!File.Exists(response.path))
            {
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(response.path);
            }
            catch (IOException)
            {
                // 書き込み側がファイルを占有している間だけ次フレームへ送る。
                return false;
            }

            if (bytes.Length == 0)
            {
                return false;
            }

            return LoadImage(response, bytes);
        }

        private static bool LoadImage(AiCommandResponse response, byte[] bytes)
        {
            // perf: 撮影 1 回あたり 1 枚だけ。毎フレームではない。
            var texture = new Texture2D(InitialTextureSize, InitialTextureSize);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    throw new InvalidDataException("capture PNG を読み込めませんでした。");
                }

                response.width = texture.width;
                response.height = texture.height;
                response.blank = ComputeLuminanceDeviation(texture.GetPixels32()) < BlankDeviationThreshold;
                return true;
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
#endif
