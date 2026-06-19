using TMPro;
using UniLab.UI.Popup;
using UniLab.UI.Popup.Sample;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Popup.SampleEditor
{
    /// <summary>
    /// Popup 動作確認サンプルのシーン・プレハブをメニューから一括生成するビルダー。
    /// プレハブは Resources/Popup へ出力し、ランタイムで型名ロードする。シーン側はシーン内参照のみ配線する。
    /// </summary>
    public static class PopupSampleBuilder
    {
        private const string SampleDirectory = "Assets/UniLab/Sample/Popup";
        private const string PrefabDirectory = SampleDirectory + "/Resources/Popup";
        private const string ConfirmPrefabPath = PrefabDirectory + "/ConfirmPopup.prefab";
        private const string RewardPrefabPath = PrefabDirectory + "/RewardPopup.prefab";
        private const string ScenePath = SampleDirectory + "/PopupSample.unity";

        // 画面想定サイズ。背景・ルートのストレッチ基準に使う
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private static readonly Vector2 PanelSize = new(800f, 480f);

        /// <summary>シーン・プレハブを生成する。現在のシーンは保存確認後に置き換わる。</summary>
        [MenuItem("UniLab/Sample/Build Popup Sample")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureDirectory(PrefabDirectory);
            CreateConfirmPrefab();
            CreateRewardPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PopupSample] シーンとプレハブを生成しました: " + ScenePath);
        }

        private static void CreateConfirmPrefab()
        {
            var root = NewUIObject("ConfirmPopup", null, Vector2.zero, ReferenceResolution);
            var confirmPopup = root.AddComponent<ConfirmPopup>();

            var background = CreateBackground(root.transform);
            var panel = CreatePanel(root.transform);
            var title = CreateText(panel.transform, "Title", "Title", new Vector2(0f, 150f), 48f);
            var message = CreateText(panel.transform, "Message", "Message", new Vector2(0f, 30f), 32f);
            var confirmButton = CreateButton(panel.transform, "ConfirmButton", "OK", new Vector2(180f, -150f));
            var cancelButton = CreateButton(panel.transform, "CancelButton", "Cancel", new Vector2(-180f, -150f));

            // プレハブ内部（同一プレハブ内オブジェクト間）の参照を設定してから保存する
            SetReference(confirmPopup, "_backgroundButton", background);
            SetReference(confirmPopup, "_titleText", title);
            SetReference(confirmPopup, "_messageText", message);
            SetReference(confirmPopup, "_confirmButton", confirmButton);
            SetReference(confirmPopup, "_cancelButton", cancelButton);

            PrefabUtility.SaveAsPrefabAsset(root, ConfirmPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateRewardPrefab()
        {
            var root = NewUIObject("RewardPopup", null, Vector2.zero, ReferenceResolution);
            var canvasGroup = root.AddComponent<CanvasGroup>();
            var rewardPopup = root.AddComponent<RewardPopup>();

            var background = CreateBackground(root.transform);
            var panel = CreatePanel(root.transform);
            var title = CreateText(panel.transform, "Title", "Reward", new Vector2(0f, 150f), 48f);
            var rewardText = CreateText(panel.transform, "RewardText", "Item x0", new Vector2(0f, 30f), 36f);
            var claimButton = CreateButton(panel.transform, "ClaimButton", "Claim", new Vector2(180f, -150f));
            var closeButton = CreateButton(panel.transform, "CloseButton", "Close", new Vector2(-180f, -150f));

            SetReference(rewardPopup, "_backgroundButton", background);
            SetReference(rewardPopup, "_titleText", title);
            SetReference(rewardPopup, "_rewardText", rewardText);
            SetReference(rewardPopup, "_claimButton", claimButton);
            SetReference(rewardPopup, "_closeButton", closeButton);
            SetReference(rewardPopup, "_canvasGroup", canvasGroup);

            PrefabUtility.SaveAsPrefabAsset(root, RewardPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasObject = new GameObject(
                "Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = ReferenceResolution;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif

            var popupRoot = NewUIObject("PopupRoot", canvasObject.transform, Vector2.zero, ReferenceResolution);
            StretchFull(popupRoot.GetComponent<RectTransform>());

            var buttonGroupObject = NewUIObject(
                "ButtonGroup", canvasObject.transform, Vector2.zero, new Vector2(480f, 420f));
            var buttonGroup = buttonGroupObject.AddComponent<CanvasGroup>();
            var verticalLayout = buttonGroupObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 24f;
            verticalLayout.childAlignment = TextAnchor.MiddleCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            var confirmButton = CreateButton(buttonGroupObject.transform, "ConfirmButton", "Confirm (v2)", Vector2.zero);
            var rewardButton = CreateButton(buttonGroupObject.transform, "RewardButton", "Reward", Vector2.zero);
            var priorityButton = CreateButton(buttonGroupObject.transform, "PriorityTestButton", "Priority Test", Vector2.zero);

            var entryObject = new GameObject("PopupSampleEntry");
            var sampleEntry = entryObject.AddComponent<PopupSampleEntry>();
            // すべてシーン内オブジェクトへの参照なので SerializedObject 配線で確実に保存される
            SetReference(sampleEntry, "_popupRoot", popupRoot.transform);
            SetReference(sampleEntry, "_confirmButton", confirmButton);
            SetReference(sampleEntry, "_rewardButton", rewardButton);
            SetReference(sampleEntry, "_priorityTestButton", priorityButton);
            SetReference(sampleEntry, "_buttonGroup", buttonGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // --- UI 構築ヘルパー ---

        private static GameObject NewUIObject(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            return gameObject;
        }

        private static Button CreateBackground(Transform parent)
        {
            var gameObject = NewUIObject("Background", parent, Vector2.zero, ReferenceResolution);
            StretchFull(gameObject.GetComponent<RectTransform>());
            var image = gameObject.AddComponent<Image>();
            // 背景は半透明の黒で、後ろの操作を遮るオーバーレイにする
            image.color = new Color(0f, 0f, 0f, 0.5f);
            return gameObject.AddComponent<Button>();
        }

        private static GameObject CreatePanel(Transform parent)
        {
            var gameObject = NewUIObject("Panel", parent, Vector2.zero, PanelSize);
            var image = gameObject.AddComponent<Image>();
            image.color = Color.white;
            return gameObject;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent, string name, string text, Vector2 anchoredPosition, float fontSize)
        {
            var gameObject = NewUIObject(name, parent, anchoredPosition, new Vector2(700f, 80f));
            var label = gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;

            // TMP 既定フォントが設定済みなら適用する（未セットアップ環境では null になり得る）
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                label.font = defaultFont;
            }

            return label;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var gameObject = NewUIObject(name, parent, anchoredPosition, new Vector2(360f, 80f));
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.5f, 0.9f, 1f);
            var button = gameObject.AddComponent<Button>();

            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 80f;
            layoutElement.preferredHeight = 80f;

            // ConfirmPopup は GetComponentInChildren<TMP_Text> でラベルを参照するため子に配置する
            var buttonLabel = CreateText(gameObject.transform, "Label", label, Vector2.zero, 28f);
            buttonLabel.color = Color.white;
            StretchFull(buttonLabel.GetComponent<RectTransform>());

            return button;
        }

        private static void StretchFull(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        // --- 配線ヘルパー（シーン内参照専用） ---

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(fieldName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectory(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
