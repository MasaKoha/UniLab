#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace UniLab.Native.Editor
{
    /// <summary>
    /// iOS 写真保存に必要な Info.plist キーを追加する。
    /// </summary>
    public static class IosPhotoLibraryUsagePostprocessor
    {
        private const string PhotoLibraryAddUsageDescription = "NSPhotoLibraryAddUsageDescription";
        private const string UsageDescriptionText = "スクリーンショットを写真アプリへ保存するために使用します。";

        /// <summary>
        /// iOS ビルド後に Info.plist へ写真追加用途の説明を設定する。
        /// </summary>
        /// <param name="target">ビルドターゲット。</param>
        /// <param name="pathToBuiltProject">Xcode プロジェクトの出力先。</param>
        [PostProcessBuild]
        public static void AddPhotoLibraryUsageDescription(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString(PhotoLibraryAddUsageDescription, UsageDescriptionText);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
