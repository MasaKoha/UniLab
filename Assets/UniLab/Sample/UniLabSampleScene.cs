using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Animation;
using UniLab.Input;
using UniLab.Localization;
using UniLab.Network;
using UniLab.Persistence;
using UniLab.UI;
using UniLab.UI.Loading;
using UniLab.UI.Toast;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.Sample
{

/// <summary>
/// Self-contained demo for every public UniLab API.
/// Attach to any GameObject in an empty scene and press Play.
///
/// Features marked [Prefab Required] need the corresponding Singleton prefab
/// placed in the scene with its Inspector fields wired:
///   - ToastManager          : _toastPrefab / _toastRoot
///   - LoadingOverlayManager : _overlayRoot
///   - SoundPlayManager      : _audioMixer / mixer groups
/// Toggle the matching bool field in this component's Inspector to enable those demos.
/// All other features work without any additional setup.
/// </summary>
public sealed class UniLabSampleScene : MonoBehaviour
{
    // ----- Inspector -----

    [Header("Prefab-required features (toggle on after wiring the manager prefab)")]
    [SerializeField] private bool _hasToastManager = false;
    [SerializeField] private bool _hasLoadingManager = false;

    // ----- Runtime UI -----

    private Text _logText;
    private readonly List<string> _logLines = new();
    private const int MaxLogLines = 14;
    private Font _font;

    // ----- Disposables -----

    private readonly CompositeDisposable _disposables = new();

    // ----- Feature state -----

    private NetworkReachabilityObservable _networkObservable;
    private OfflineQueue<SamplePendingRequest> _offlineQueue;
    private EncryptedLocalStorage _encryptedStorage;
    private int _localSaveCounter;
    private RectTransform _gridContainer;
    private int _gridChildCount;
    private IDisposable _inputBlockHandle;

    // ----- Lifecycle -----

    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
        InitFeatures();
    }

    private void OnDestroy()
    {
        _inputBlockHandle?.Dispose();
        _disposables.Dispose();
        _networkObservable?.Dispose();
    }

    // ----- Feature initialization -----

    private void InitFeatures()
    {
        _encryptedStorage = new EncryptedLocalStorage();
        _offlineQueue = new OfflineQueue<SamplePendingRequest>();

        // Monitor network reachability changes
        _networkObservable = new NetworkReachabilityObservable(TimeSpan.FromSeconds(5));
        _disposables.Add(
            _networkObservable.OnReachabilityChanged
                .Subscribe(r => Log($"[Network] Reachability → {r}")));

        // Echo InputBlockManager events
        _disposables.Add(InputBlockManager.OnShow.Subscribe(_ => Log("[InputBlock] Blocked")));
        _disposables.Add(InputBlockManager.OnHide.Subscribe(_ => Log("[InputBlock] Unblocked")));

        // Echo back-key presses (Android only; Observable always available)
        _disposables.Add(
            BackKeyInputManager.Instance.OnPressBackKey
                .Subscribe(_ => Log("[BackKey] Back pressed")));

        Log("UniLab Sample ready. Tap any button to demo a feature.");
    }

    // =========================================================
    // Feature demos
    // =========================================================

    // --- LocalSave ---

    private void DemoLocalSaveSave()
    {
        _localSaveCounter++;
        LocalSave.Save(new SampleSaveData { Counter = _localSaveCounter, Label = "hello" });
        var loaded = LocalSave.Load<SampleSaveData>();
        Log($"[LocalSave] Saved {_localSaveCounter}  →  loaded counter={loaded.Counter}");
    }

    private void DemoLocalSaveDelete()
    {
        LocalSave.Delete<SampleSaveData>();
        var loaded = LocalSave.Load<SampleSaveData>();
        Log($"[LocalSave] Deleted. Default load: counter={loaded.Counter}");
    }

    // --- Encrypted Storage ---

    private void DemoEncryptedStorage()
    {
        var data = new SampleSaveData { Counter = 99, Label = "secret" };
        _encryptedStorage.Save("demo_key", data, TimeSpan.FromMinutes(10));
        var loaded = _encryptedStorage.Load<SampleSaveData>("demo_key");
        Log($"[Storage] AES-saved & loaded: counter={loaded.Counter} label={loaded.Label}");
    }

    private void DemoEncryptedStorageDelete()
    {
        _encryptedStorage.Delete("demo_key");
        Log("[Storage] Deleted demo_key. Exists=" + _encryptedStorage.Exists("demo_key"));
    }

    // --- TextManager ---

    private void DemoTextManagerJa()
    {
        TextManager.SetLanguage("ja");
        // Requires LocalizationData asset in Resources; returns [Missing:…] without it.
        var text = TextManager.GetText("app_title");
        Log($"[Text] ja: app_title = {text}");
    }

    private void DemoTextManagerEn()
    {
        TextManager.SetLanguage("en");
        var text = TextManager.GetText("app_title");
        Log($"[Text] en: app_title = {text}");
        TextManager.SetLanguage("ja"); // restore
    }

    // --- InputBlockManager ---

    private void DemoToggleInputBlock()
    {
        if (_inputBlockHandle != null)
        {
            _inputBlockHandle.Dispose();
            _inputBlockHandle = null;
            Log($"[InputBlock] Released. BlockedInput={InputBlockManager.BlockedInput}");
        }
        else
        {
            _inputBlockHandle = InputBlockManager.CreateInputBlock();
            Log($"[InputBlock] Created. BlockedInput={InputBlockManager.BlockedInput}");
        }
    }

    // --- Network ---

    private void DemoNetworkReachability()
    {
        Log($"[Network] Current: {Application.internetReachability}");
    }

    // --- OfflineQueue ---

    private void DemoOfflineQueueEnqueue()
    {
        var key = $"req_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        _offlineQueue.Enqueue(key, new SamplePendingRequest { Endpoint = "/api/demo", Body = "payload" });
        Log($"[OfflineQueue] Enqueued. Count={_offlineQueue.Count}");
    }

    private void DemoOfflineQueueFlush()
    {
        FlushOfflineQueueAsync().Forget();
    }

    private async UniTaskVoid FlushOfflineQueueAsync()
    {
        var flushedCount = 0;
        await _offlineQueue.FlushAsync(async (request, ct) =>
        {
            await UniTask.Delay(100, cancellationToken: ct);
            flushedCount++;
            Log($"[OfflineQueue] Sent: {request.Endpoint}");
        }, destroyCancellationToken);
        Log($"[OfflineQueue] Flush done. Sent={flushedCount} Remaining={_offlineQueue.Count}");
    }

    private void DemoOfflineQueueClear()
    {
        _offlineQueue.Clear();
        Log($"[OfflineQueue] Cleared. Count={_offlineQueue.Count}");
    }

    // --- VariableGridLayoutGroup ---

    private void DemoGridAddItem()
    {
        _gridChildCount++;
        // Variable width: cycles 60/90/120px to show wrapping behavior
        var width = 60f + (_gridChildCount % 3) * 30f;

        var itemGo = new GameObject($"Item{_gridChildCount}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        itemGo.transform.SetParent(_gridContainer, false);

        var image = itemGo.GetComponent<Image>();
        image.color = Color.HSVToRGB((_gridChildCount * 0.13f) % 1f, 0.55f, 0.85f);

        var layoutElement = itemGo.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 44f;

        // Add item index label
        // typeof(Text) in the constructor already adds the Text component.
        var labelGo = new GameObject("Label", typeof(Text));
        labelGo.transform.SetParent(itemGo.transform, false);
        var label = labelGo.GetComponent<Text>();
        label.font = _font;
        label.fontSize = 13;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = _gridChildCount.ToString();
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        LayoutRebuilder.MarkLayoutForRebuild(_gridContainer);
        Log($"[Grid] Added item {_gridChildCount} (w={width})");
    }

    private void DemoGridClear()
    {
        foreach (Transform child in _gridContainer)
        {
            Destroy(child.gameObject);
        }
        _gridChildCount = 0;
        Log("[Grid] Cleared");
    }

    // --- AnimationPlayer ---
    // Actual playback requires an AnimatorController with named states.
    // This demo shows subscription to the observables and the async API.

    private void DemoAnimationPlayerSetup()
    {
        var go = new GameObject("AnimationPlayerDemo");
        go.AddComponent<Animator>(); // AnimationPlayer requires Animator on the same GameObject
        var player = go.AddComponent<AnimationPlayer>();

        _disposables.Add(player.OnPlay.Subscribe(_ => Log("[Animation] OnPlay fired")));
        _disposables.Add(player.OnComplete.Subscribe(_ => Log("[Animation] OnComplete fired")));

        Log("[Animation] AnimationPlayer set up. IsPlaying=" + player.IsPlaying);
        Log("[Animation] Call player.PlayAsync(\"StateName\") with a real AnimatorController to play.");

        // Demonstrate the async call signature (returns immediately — animation name is empty)
        player.PlayAsync().Forget();
    }

    // --- Toast [Prefab Required] ---

    private void DemoToastInfo()    => ShowToast("Info toast from UniLab", ToastType.Info);
    private void DemoToastSuccess() => ShowToast("Operation succeeded!", ToastType.Success);
    private void DemoToastError()   => ShowToast("Something went wrong.", ToastType.Error);

    private void ShowToast(string message, ToastType type)
    {
        if (!_hasToastManager)
        {
            Log("[Toast] Skipped — enable _hasToastManager after adding ToastManager prefab to scene");
            return;
        }

        try
        {
            ToastManager.Instance.Show(message, type);
            Log($"[Toast] Shown ({type}): {message}");
        }
        catch (Exception exception)
        {
            Log($"[Toast] Error: {exception.Message}");
        }
    }

    // --- LoadingOverlayManager [Prefab Required] ---

    private void DemoLoadingOverlay()
    {
        if (!_hasLoadingManager)
        {
            Log("[Loading] Skipped — enable _hasLoadingManager after adding LoadingOverlayManager prefab to scene");
            return;
        }

        ShowLoadingAsync().Forget();
    }

    private async UniTaskVoid ShowLoadingAsync()
    {
        try
        {
            using var handle = LoadingOverlayManager.Instance.Show();
            Log("[Loading] Overlay shown (1.5 s)...");
            await UniTask.Delay(1500, cancellationToken: destroyCancellationToken);
        }
        catch (Exception exception)
        {
            Log($"[Loading] Error: {exception.Message}");
        }
        Log("[Loading] Overlay hidden");
    }

    // --- BackKeyInputManager ---

    private void DemoBackKeyToggleBlock()
    {
        var manager = BackKeyInputManager.Instance;
        manager.SetBlock(!manager.IsBlocked);
        Log($"[BackKey] IsBlocked={manager.IsBlocked} (Android only; Observable always active)");
    }

    // =========================================================
    // Log helper
    // =========================================================

    private void Log(string message)
    {
        Debug.Log(message);
        _logLines.Add(message);
        if (_logLines.Count > MaxLogLines)
        {
            _logLines.RemoveAt(0);
        }
        if (_logText != null)
        {
            _logText.text = string.Join("\n", _logLines);
        }
    }

    // =========================================================
    // UI construction
    // =========================================================

    private void BuildUI()
    {
        // ----- Canvas -----
        var canvasGo = new GameObject("UniLabSampleCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // EventSystem (skip if one already exists)
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // ----- Background -----
        var bg = CreatePanel(canvasGo.transform, "BG", new Color(0.09f, 0.09f, 0.11f));
        SetFullStretch(bg);

        // ----- Header -----
        var header = CreatePanel(canvasGo.transform, "Header", new Color(0.06f, 0.06f, 0.08f));
        SetAnchoredLayout(header, new Vector2(0f, 0.94f), Vector2.one);
        CreateLabel(header, "UniLab Feature Sample", 26, TextAnchor.MiddleCenter, Color.white);

        // ----- Scroll area (main demos) -----
        var scroll = BuildScrollRect(canvasGo.transform, new Vector2(0f, 0.22f), new Vector2(1f, 0.94f));
        var content = scroll.content;

        BuildSectionLocalSave(content);
        BuildSectionEncryptedStorage(content);
        BuildSectionTextManager(content);
        BuildSectionInputBlock(content);
        BuildSectionNetwork(content);
        BuildSectionOfflineQueue(content);
        BuildSectionGrid(content);
        BuildSectionUniLabButton(content);
        BuildSectionAnimationPlayer(content);
        BuildSectionToast(content);
        BuildSectionLoading(content);
        BuildSectionBackKey(content);

        // ----- Log panel -----
        var logPanel = CreatePanel(canvasGo.transform, "LogPanel", new Color(0.04f, 0.04f, 0.06f));
        SetAnchoredLayout(logPanel, Vector2.zero, new Vector2(1f, 0.22f));

        var logGo = new GameObject("LogText");
        logGo.transform.SetParent(logPanel, false);
        _logText = logGo.AddComponent<Text>();
        _logText.font = _font;
        _logText.fontSize = 13;
        _logText.color = new Color(0.6f, 1f, 0.6f);
        _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _logText.verticalOverflow = VerticalWrapMode.Overflow;
        var logRect = logGo.GetComponent<RectTransform>();
        logRect.anchorMin = Vector2.zero;
        logRect.anchorMax = Vector2.one;
        logRect.offsetMin = new Vector2(10f, 6f);
        logRect.offsetMax = new Vector2(-10f, -6f);
    }

    // ----- Section builders -----

    private void BuildSectionLocalSave(RectTransform parent)
    {
        var section = AddSection(parent, "LocalSave  (PlayerPrefs + Base64 JSON)");
        AddButtonRow(section, ("Save / Load", (Action)DemoLocalSaveSave), ("Delete", DemoLocalSaveDelete));
    }

    private void BuildSectionEncryptedStorage(RectTransform parent)
    {
        var section = AddSection(parent, "EncryptedLocalStorage  (AES-256 + TTL)");
        AddButtonRow(section, ("Save & Load", (Action)DemoEncryptedStorage), ("Delete", DemoEncryptedStorageDelete));
    }

    private void BuildSectionTextManager(RectTransform parent)
    {
        var section = AddSection(parent, "TextManager  (FNV-1A localization)");
        AddButtonRow(section, ("GetText [ja]", (Action)DemoTextManagerJa), ("GetText [en]", DemoTextManagerEn));
    }

    private void BuildSectionInputBlock(RectTransform parent)
    {
        var section = AddSection(parent, "InputBlockManager  (ref-counted block)");
        AddButtonRow(section, ("Toggle Block", (Action)DemoToggleInputBlock));
    }

    private void BuildSectionNetwork(RectTransform parent)
    {
        var section = AddSection(parent, "NetworkReachabilityObservable  (polling)");
        AddButtonRow(section, ("Check Now", (Action)DemoNetworkReachability));
    }

    private void BuildSectionOfflineQueue(RectTransform parent)
    {
        var section = AddSection(parent, "OfflineQueue<T>  (persistent request queue)");
        AddButtonRow(section,
            ("Enqueue", (Action)DemoOfflineQueueEnqueue),
            ("Flush", DemoOfflineQueueFlush),
            ("Clear", DemoOfflineQueueClear));
    }

    private void BuildSectionGrid(RectTransform parent)
    {
        var section = AddSection(parent, "VariableGridLayoutGroup  (variable-width wrap)");

        // Grid container — this is the live demo area
        var gridPanelGo = new GameObject("GridPanel", typeof(RectTransform), typeof(Image));
        gridPanelGo.transform.SetParent(section, false);
        gridPanelGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f);

        _gridContainer = gridPanelGo.GetComponent<RectTransform>();
        _gridContainer.anchorMin = new Vector2(0f, 0f);
        _gridContainer.anchorMax = new Vector2(1f, 0f);
        _gridContainer.pivot = new Vector2(0.5f, 0f);
        _gridContainer.sizeDelta = new Vector2(0f, 120f);

        var grid = gridPanelGo.AddComponent<VariableGridLayoutGroup>();
        // Reflect private fields via SetFieldValue for runtime configuration
        SetField(grid, "_spacing", new Vector2(6f, 6f));
        SetField(grid, "_defaultItemSize", new Vector2(80f, 44f));
        SetField(grid, "_usePreferredWidth", true);
        SetField(grid, "_usePreferredHeight", true);
        SetField(grid, "_minItemWidth", 1f);
        SetField(grid, "_minItemHeight", 1f);

        var sizeFitter = gridPanelGo.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddButtonRow(section, ("+ Item", (Action)DemoGridAddItem), ("Clear", DemoGridClear));
    }

    private void BuildSectionUniLabButton(RectTransform parent)
    {
        var section = AddSection(parent, "UniLabButton  (Hold / Decide / State observables)");

        // Create a UniLabButton — requires Image as targetGraphic
        var buttonGo = new GameObject("UniLabButtonDemo", typeof(Image));
        buttonGo.transform.SetParent(section, false);
        var buttonImage = buttonGo.GetComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.45f, 0.75f);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.05f, 0f);
        buttonRect.anchorMax = new Vector2(0.95f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(0f, 48f);

        var uniLabButton = buttonGo.AddComponent<UniLabButton>();
        uniLabButton.targetGraphic = buttonImage;

        CreateLabel(buttonRect, "Press & Hold Me", 16, TextAnchor.MiddleCenter, Color.white);

        // Wire reactive observables — the core UniLabButton feature
        _disposables.Add(uniLabButton.OnHoldAsObservable().Subscribe(_ => Log("[UniLabButton] OnHold")));
        _disposables.Add(uniLabButton.OnDecideAsObservable().Subscribe(_ => Log("[UniLabButton] OnDecide")));
        _disposables.Add(
            uniLabButton.StateAsObservable()
                .Where(state => state != ButtonState.None)
                .Subscribe(state => Log($"[UniLabButton] State → {state}")));
    }

    private void BuildSectionAnimationPlayer(RectTransform parent)
    {
        var section = AddSection(parent, "AnimationPlayer  (async + observables)");
        AddButtonRow(section, ("Setup Demo", (Action)DemoAnimationPlayerSetup));
    }

    private void BuildSectionToast(RectTransform parent)
    {
        var section = AddSection(parent, "ToastManager  [Prefab Required]");
        AddButtonRow(section,
            ("Info", (Action)DemoToastInfo),
            ("Success", DemoToastSuccess),
            ("Error", DemoToastError));
    }

    private void BuildSectionLoading(RectTransform parent)
    {
        var section = AddSection(parent, "LoadingOverlayManager  [Prefab Required]");
        AddButtonRow(section, ("Show 1.5 s", (Action)DemoLoadingOverlay));
    }

    private void BuildSectionBackKey(RectTransform parent)
    {
        var section = AddSection(parent, "BackKeyInputManager  (Android back key)");
        AddButtonRow(section, ("Toggle Block", (Action)DemoBackKeyToggleBlock));
    }

    // ----- UI helpers -----

    /// <summary>Creates a titled section container inside a vertical scroll content.</summary>
    private RectTransform AddSection(RectTransform parent, string title)
    {
        const float sectionPadding = 10f;

        // Section wrapper
        var sectionGo = new GameObject($"Section_{title}", typeof(RectTransform), typeof(Image));
        sectionGo.transform.SetParent(parent, false);
        sectionGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f);
        var sectionRect = sectionGo.GetComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0f, 1f);
        sectionRect.anchorMax = new Vector2(1f, 1f);
        sectionRect.pivot = new Vector2(0.5f, 1f);

        var layout = sectionGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 8);
        layout.spacing = sectionPadding;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = sectionGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title label
        var titleGo = new GameObject("Title", typeof(Text));
        titleGo.transform.SetParent(sectionGo.transform, false);
        var titleText = titleGo.GetComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.8f, 0.85f, 1f);
        titleText.text = title;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0f, 24f);

        return sectionRect;
    }

    private void AddButtonRow(RectTransform section, params (string label, Action onClick)[] buttons)
    {
        var rowGo = new GameObject("ButtonRow", typeof(RectTransform));
        rowGo.transform.SetParent(section, false);
        var rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 48f);

        var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        foreach (var (label, onClick) in buttons)
        {
            CreateButton(rowGo.transform, label, onClick);
        }
    }

    private void CreateButton(Transform parent, string label, Action onClick)
    {
        var buttonGo = new GameObject($"Btn_{label}", typeof(Image));
        buttonGo.transform.SetParent(parent, false);
        buttonGo.GetComponent<Image>().color = new Color(0.22f, 0.42f, 0.68f);

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonGo.GetComponent<Image>();

        var colors = button.colors;
        colors.highlightedColor = new Color(0.3f, 0.55f, 0.85f);
        colors.pressedColor = new Color(0.15f, 0.30f, 0.55f);
        button.colors = colors;

        button.onClick.AddListener(() => onClick());

        CreateLabel(buttonGo.GetComponent<RectTransform>(), label, 15, TextAnchor.MiddleCenter, Color.white);
    }

    private Text CreateLabel(RectTransform parent, string text, int fontSize, TextAnchor alignment, Color color)
    {
        var go = new GameObject("Label", typeof(Text));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = _font;
        label.fontSize = fontSize;
        label.color = color;
        label.text = text;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 0f);
        rect.offsetMax = new Vector2(-4f, 0f);
        return label;
    }

    private RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    private ScrollRect BuildScrollRect(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        // Scroll root
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        SetAnchoredLayout(scrollGo.GetComponent<RectTransform>(), anchorMin, anchorMax);

        // Viewport
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        viewportGo.GetComponent<Image>().color = Color.white;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        scrollRect.viewport = viewportRect;

        // Content
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

        var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(12, 12, 12, 12);
        contentLayout.spacing = 10f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20f;

        return scrollRect;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredLayout(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>Sets a private serialized field via reflection — used to configure components that
    /// expose no public setter for serialized-field values.</summary>
    private static void SetField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(target, value);
    }
}

// ----- Data types used by the sample -----

/// <summary>Minimal serializable save-data for LocalSave / EncryptedLocalStorage demos.</summary>
[Serializable]
public sealed class SampleSaveData
{
    public int Counter;
    public string Label;
}

/// <summary>Minimal serializable payload for OfflineQueue demo.</summary>
[Serializable]
public sealed class SamplePendingRequest
{
    public string Endpoint;
    public string Body;
}

} // namespace UniLab.Sample
