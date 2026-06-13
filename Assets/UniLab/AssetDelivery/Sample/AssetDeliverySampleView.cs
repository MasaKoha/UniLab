using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.AssetDelivery.Sample
{
    internal sealed class AssetDeliverySampleView : IAssetDeliverySampleView
    {
        private readonly Subject<Unit> _initializeRequested = new();
        private readonly Subject<Unit> _checkAndDownloadRequested = new();
        private readonly Subject<Unit> _loadAssetRequested = new();
        private readonly Subject<Unit> _clearCacheRequested = new();
        private readonly Slider _progressSlider;
        private readonly Text _stateText;
        private readonly Text _messageText;
        private readonly Image _loadedSpriteImage;
        private readonly Font _font;

        /// <summary>
        /// Gets the event emitted when the Initialize button is clicked.
        /// </summary>
        public Observable<Unit> OnInitializeRequested => _initializeRequested;

        /// <summary>
        /// Gets the event emitted when the Check And Download button is clicked.
        /// </summary>
        public Observable<Unit> OnCheckAndDownloadRequested => _checkAndDownloadRequested;

        /// <summary>
        /// Gets the event emitted when the Load Asset button is clicked.
        /// </summary>
        public Observable<Unit> OnLoadAssetRequested => _loadAssetRequested;

        /// <summary>
        /// Gets the event emitted when the Clear Cache button is clicked.
        /// </summary>
        public Observable<Unit> OnClearCacheRequested => _clearCacheRequested;

        /// <summary>
        /// Builds the generated uGUI sample view under the provided Canvas transform.
        /// </summary>
        public AssetDeliverySampleView(Transform parent)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = CreateRoot(parent);
            CreateButton(root.transform, "Initialize", _initializeRequested);
            CreateButton(root.transform, "Check And Download", _checkAndDownloadRequested);
            CreateButton(root.transform, "Load Asset", _loadAssetRequested);
            CreateButton(root.transform, "Clear Cache", _clearCacheRequested);

            _progressSlider = CreateSlider(root.transform);
            _stateText = CreateText(root.transform, "State: NotInitialized", 18, TextAnchor.MiddleLeft);
            _messageText = CreateText(root.transform, "Message: Ready", 16, TextAnchor.UpperLeft);
            _loadedSpriteImage = CreateLoadedSpriteImage(root.transform);
        }

        /// <summary>
        /// Updates the current delivery state text.
        /// </summary>
        public void SetStateText(string text)
        {
            _stateText.text = $"State: {text}";
        }

        /// <summary>
        /// Updates the normalized download progress.
        /// </summary>
        public void SetProgress(float ratio)
        {
            _progressSlider.value = Mathf.Clamp01(ratio);
        }

        /// <summary>
        /// Updates the loaded sprite preview.
        /// </summary>
        public void SetLoadedSprite(Sprite sprite)
        {
            _loadedSpriteImage.sprite = sprite;
            _loadedSpriteImage.enabled = sprite != null;
        }

        /// <summary>
        /// Updates the latest operation message.
        /// </summary>
        public void SetMessage(string text)
        {
            _messageText.text = $"Message: {text}";
        }

        /// <summary>
        /// Releases streams exposed by the generated view.
        /// </summary>
        public void Dispose()
        {
            _initializeRequested.Dispose();
            _checkAndDownloadRequested.Dispose();
            _loadAssetRequested.Dispose();
            _clearCacheRequested.Dispose();
        }

        private static GameObject CreateRoot(Transform parent)
        {
            var root = new GameObject("AssetDeliverySampleView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);

            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(520f, 680f);

            var image = root.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.10f, 0.92f);

            var layoutGroup = root.GetComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(24, 24, 24, 24);
            layoutGroup.spacing = 12f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            return root;
        }

        private Button CreateButton(Transform parent, string label, Subject<Unit> subject)
        {
            var buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 54f;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.35f, 0.52f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => subject.OnNext(Unit.Default));

            var labelText = CreateText(buttonObject.transform, label, 18, TextAnchor.MiddleCenter);
            var labelRectTransform = labelText.GetComponent<RectTransform>();
            labelRectTransform.anchorMin = Vector2.zero;
            labelRectTransform.anchorMax = Vector2.one;
            labelRectTransform.offsetMin = Vector2.zero;
            labelRectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private Slider CreateSlider(Transform parent)
        {
            var sliderObject = new GameObject("DownloadProgressSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);

            var layoutElement = sliderObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 36f;

            var background = CreateSliderGraphic(sliderObject.transform, "Background", new Color(0.18f, 0.18f, 0.18f));
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);

            var fillAreaRectTransform = fillArea.GetComponent<RectTransform>();
            fillAreaRectTransform.anchorMin = Vector2.zero;
            fillAreaRectTransform.anchorMax = Vector2.one;
            fillAreaRectTransform.offsetMin = new Vector2(4f, 4f);
            fillAreaRectTransform.offsetMax = new Vector2(-4f, -4f);

            var fill = CreateSliderGraphic(fillArea.transform, "Fill", new Color(0.31f, 0.72f, 0.44f));
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.targetGraphic = background;
            slider.fillRect = fill.rectTransform;

            return slider;
        }

        private Image CreateSliderGraphic(Transform parent, string name, Color color)
        {
            var graphicObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            graphicObject.transform.SetParent(parent, false);

            var rectTransform = graphicObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var image = graphicObject.GetComponent<Image>();
            image.color = color;

            return image;
        }

        private Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var layoutElement = textObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 42f;

            var textComponent = textObject.GetComponent<Text>();
            textComponent.font = _font;
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;

            return textComponent;
        }

        private Image CreateLoadedSpriteImage(Transform parent)
        {
            var imageObject = new GameObject("LoadedSpriteImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            imageObject.transform.SetParent(parent, false);

            var layoutElement = imageObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 260f;

            var image = imageObject.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.enabled = false;

            return image;
        }
    }
}
