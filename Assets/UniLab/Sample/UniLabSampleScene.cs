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
/// Toggle the matching bool field in this component's Inspector to enable those demos.
/// All other features work without any additional setup.
/// </summary>
public sealed class UniLabSampleScene : MonoBehaviour
{
    // ----- Inspector -----

    [Header("Prefab-required features (toggle on after wiring the manager prefab)")]
    [SerializeField] private bool _hasToastManager = false;
    [SerializeField] private bool _hasLoadingManager = false;

    // ----- Screen state -----

    private enum FeatureId
    {
        LocalSave,
        EncryptedStorage,
        TextManager,
        InputBlock,
        Network,
        OfflineQueue,
        Grid,
        UniLabButton,
        AnimationPlayer,
        Toast,
        Loading,
        BackKey,
    }

    // ----- Runtime UI -----

    private Text _logText;
    private readonly List<string> _logLines = new();
    private const int MaxLogLines = 14;
    private Font _font;

    private GameObject _menuPanel;
    private GameObject _featurePanel;
    private RectTransform _featurePanelContent;
    private GameObject _backButton;

    // ----- Disposables -----

    private readonly CompositeDisposable _disposables = new();

    // Per-feature subscriptions (e.g. UniLabButton) — cleared on each ShowFeature call
    private readonly CompositeDisposable _featureDisposables = new();

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
        _featureDisposables.Dispose();
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
    // Navigation
    // =========================================================

    private void ShowMenu()
    {
        _menuPanel.SetActive(true);
        _featurePanel.SetActive(false);
        _backButton.SetActive(false);
    }

    private void ShowFeature(FeatureId id)
    {
        // Release previous feature-specific subscriptions before rebuilding content
        _featureDisposables.Clear();

        _menuPanel.SetActive(false);
        _featurePanel.SetActive(true);
        _backButton.SetActive(true);

        // Clear previous content
        foreach (Transform child in _featurePanelContent)
        {
            Destroy(child.gameObject);
        }
        _gridContainer = null;
        _gridChildCount = 0;

        switch (id)
        {
            case FeatureId.LocalSave:          BuildFeatureContent_LocalSave(_featurePanelContent);         break;
            case FeatureId.EncryptedStorage:   BuildFeatureContent_EncryptedStorage(_featurePanelContent);  break;
            case FeatureId.TextManager:        BuildFeatureContent_TextManager(_featurePanelContent);        break;
            case FeatureId.InputBlock:         BuildFeatureContent_InputBlock(_featurePanelContent);         break;
            case FeatureId.Network:            BuildFeatureContent_Network(_featurePanelContent);            break;
            case FeatureId.OfflineQueue:       BuildFeatureContent_OfflineQueue(_featurePanelContent);       break;
            case FeatureId.Grid:               BuildFeatureContent_Grid(_featurePanelContent);               break;
            case FeatureId.UniLabButton:       BuildFeatureContent_UniLabButton(_featurePanelContent);       break;
            case FeatureId.AnimationPlayer:    BuildFeatureContent_AnimationPlayer(_featurePanelContent);    break;
            case FeatureId.Toast:              BuildFeatureContent_Toast(_featurePanelContent);              break;
            case FeatureId.Loading:            BuildFeatureContent_Loading(_featurePanelContent);            break;
            case FeatureId.BackKey:            BuildFeatureContent_BackKey(_featurePanelContent);            break;
        }
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

        // ----- Menu panel -----
        BuildMenuPanel(canvasGo.transform);

        // ----- Feature panel -----
        BuildFeaturePanel(canvasGo.transform);

        // ----- Log panel -----
        BuildLogPanel(canvasGo.transform);

        // ----- Header (last = highest sibling index = top of raycast priority) -----
        BuildHeader(canvasGo.transform);

        // Start at menu screen
        ShowMenu();
    }

    private void BuildHeader(Transform canvasTransform)
    {
        var header = CreatePanel(canvasTransform, "Header", new Color(0.06f, 0.06f, 0.08f));
        SetAnchoredLayout(header, new Vector2(0f, 0.94f), Vector2.one);

        // Back button — hidden on menu, shown on feature screens
        var backButtonGo = new GameObject("BackButton", typeof(Image));
        backButtonGo.transform.SetParent(header, false);
        backButtonGo.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.32f);

        var backButtonRect = backButtonGo.GetComponent<RectTransform>();
        backButtonRect.anchorMin = new Vector2(0f, 0f);
        backButtonRect.anchorMax = new Vector2(0f, 1f);
        backButtonRect.pivot = new Vector2(0f, 0.5f);
        backButtonRect.offsetMin = new Vector2(12f, 8f);
        backButtonRect.offsetMax = new Vector2(132f, -8f);

        var backButton = backButtonGo.AddComponent<Button>();
        backButton.targetGraphic = backButtonGo.GetComponent<Image>();
        backButton.onClick.AddListener(ShowMenu);

        CreateLabel(backButtonRect, "← Back", 24, TextAnchor.MiddleCenter, Color.white);

        _backButton = backButtonGo;

        // Header title
        CreateLabel(header, "UniLab Feature Sample", 32, TextAnchor.MiddleCenter, Color.white);
    }

    private void BuildMenuPanel(Transform canvasTransform)
    {
        var menuPanelGo = new GameObject("MenuPanel", typeof(RectTransform));
        menuPanelGo.transform.SetParent(canvasTransform, false);
        SetAnchoredLayout(menuPanelGo.GetComponent<RectTransform>(), new Vector2(0f, 0.18f), new Vector2(1f, 0.94f));
        _menuPanel = menuPanelGo;

        var scroll = BuildScrollRect(menuPanelGo.transform, Vector2.zero, Vector2.one);
        var content = scroll.content;

        foreach (FeatureId featureId in Enum.GetValues(typeof(FeatureId)))
        {
            AddMenuButton(content, featureId);
        }
    }

    private void AddMenuButton(RectTransform parent, FeatureId featureId)
    {
        var buttonGo = new GameObject($"MenuBtn_{featureId}", typeof(Image));
        buttonGo.transform.SetParent(parent, false);
        buttonGo.GetComponent<Image>().color = GetCategoryColor(featureId);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.sizeDelta = new Vector2(0f, 100f);

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonGo.GetComponent<Image>();

        var colors = button.colors;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        button.colors = colors;

        var capturedId = featureId;
        button.onClick.AddListener(() => ShowFeature(capturedId));

        CreateLabel(buttonRect, featureId.ToString(), 28, TextAnchor.MiddleCenter, Color.white);
    }

    private static Color GetCategoryColor(FeatureId featureId)
    {
        switch (featureId)
        {
            case FeatureId.LocalSave:
            case FeatureId.EncryptedStorage:
                return new Color(0.22f, 0.35f, 0.55f);
            case FeatureId.TextManager:
                return new Color(0.40f, 0.25f, 0.55f);
            case FeatureId.InputBlock:
            case FeatureId.UniLabButton:
            case FeatureId.Toast:
            case FeatureId.Loading:
                return new Color(0.55f, 0.35f, 0.15f);
            case FeatureId.Network:
            case FeatureId.OfflineQueue:
                return new Color(0.20f, 0.45f, 0.30f);
            case FeatureId.Grid:
            case FeatureId.AnimationPlayer:
            case FeatureId.BackKey:
                return new Color(0.20f, 0.40f, 0.45f);
            default:
                return new Color(0.22f, 0.22f, 0.28f);
        }
    }

    private void BuildFeaturePanel(Transform canvasTransform)
    {
        var featurePanelGo = new GameObject("FeaturePanel", typeof(RectTransform));
        featurePanelGo.transform.SetParent(canvasTransform, false);
        SetAnchoredLayout(featurePanelGo.GetComponent<RectTransform>(), new Vector2(0f, 0.18f), new Vector2(1f, 0.94f));
        _featurePanel = featurePanelGo;

        var scroll = BuildScrollRect(featurePanelGo.transform, Vector2.zero, Vector2.one);
        _featurePanelContent = scroll.content;
    }

    private void BuildLogPanel(Transform canvasTransform)
    {
        var logPanel = CreatePanel(canvasTransform, "LogPanel", new Color(0.04f, 0.04f, 0.06f));
        SetAnchoredLayout(logPanel, Vector2.zero, new Vector2(1f, 0.18f));

        var logGo = new GameObject("LogText");
        logGo.transform.SetParent(logPanel, false);
        _logText = logGo.AddComponent<Text>();
        _logText.font = _font;
        _logText.fontSize = 20;
        _logText.color = new Color(0.6f, 1f, 0.6f);
        _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _logText.verticalOverflow = VerticalWrapMode.Overflow;
        var logRect = logGo.GetComponent<RectTransform>();
        logRect.anchorMin = Vector2.zero;
        logRect.anchorMax = Vector2.one;
        logRect.offsetMin = new Vector2(10f, 6f);
        logRect.offsetMax = new Vector2(-10f, -6f);
    }

    // =========================================================
    // Feature content builders
    // =========================================================

    private void BuildFeatureContent_LocalSave(RectTransform parent)
    {
        AddFeatureHeader(parent, "LocalSave", "PlayerPrefs + Base64 JSON による軽量ローカル保存。");
        AddFeatureButtonRow(parent,
            ("Save / Load", (Action)DemoLocalSaveSave),
            ("Delete", DemoLocalSaveDelete));
    }

    private void BuildFeatureContent_EncryptedStorage(RectTransform parent)
    {
        AddFeatureHeader(parent, "EncryptedStorage", "AES-256 暗号化 + TTL キャッシュ付きローカルストレージ。");
        AddFeatureButtonRow(parent,
            ("Save & Load", (Action)DemoEncryptedStorage),
            ("Delete", DemoEncryptedStorageDelete));
    }

    private void BuildFeatureContent_TextManager(RectTransform parent)
    {
        AddFeatureHeader(parent, "TextManager", "FNV-1A ハッシュベースのローカライズシステム。");
        AddFeatureButtonRow(parent,
            ("GetText [ja]", (Action)DemoTextManagerJa),
            ("GetText [en]", DemoTextManagerEn));
    }

    private void BuildFeatureContent_InputBlock(RectTransform parent)
    {
        AddFeatureHeader(parent, "InputBlock", "参照カウント方式の入力ブロック管理。");
        AddFeatureButtonRow(parent,
            ("Toggle Block", (Action)DemoToggleInputBlock));
    }

    private void BuildFeatureContent_Network(RectTransform parent)
    {
        AddFeatureHeader(parent, "Network", "R3 Observable による通信状態の監視。");
        AddFeatureButtonRow(parent,
            ("Check Now", (Action)DemoNetworkReachability));
    }

    private void BuildFeatureContent_OfflineQueue(RectTransform parent)
    {
        AddFeatureHeader(parent, "OfflineQueue", "オフライン中のリクエストを永続化キューに積み、復帰時に送信。");
        AddFeatureButtonRow(parent,
            ("Enqueue", (Action)DemoOfflineQueueEnqueue),
            ("Flush", DemoOfflineQueueFlush),
            ("Clear", DemoOfflineQueueClear));
    }

    private void BuildFeatureContent_Grid(RectTransform parent)
    {
        AddFeatureHeader(parent, "Grid", "可変幅アイテムを折り返しながら並べるレイアウトグループ。");
        AddFeatureButtonRow(parent,
            ("+ Item", (Action)DemoGridAddItem),
            ("Clear", DemoGridClear));

        // Grid demo area — rebuilt each time this feature is shown
        var gridPanelGo = new GameObject("GridPanel", typeof(RectTransform), typeof(Image));
        gridPanelGo.transform.SetParent(parent, false);
        gridPanelGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f);

        _gridContainer = gridPanelGo.GetComponent<RectTransform>();
        _gridContainer.anchorMin = new Vector2(0f, 1f);
        _gridContainer.anchorMax = new Vector2(1f, 1f);
        _gridContainer.pivot = new Vector2(0.5f, 1f);
        _gridContainer.sizeDelta = new Vector2(0f, 160f);

        var grid = gridPanelGo.AddComponent<VariableGridLayoutGroup>();
        SetField(grid, "_spacing", new Vector2(6f, 6f));
        SetField(grid, "_defaultItemSize", new Vector2(80f, 44f));
        SetField(grid, "_usePreferredWidth", true);
        SetField(grid, "_usePreferredHeight", true);
        SetField(grid, "_minItemWidth", 1f);
        SetField(grid, "_minItemHeight", 1f);

        var sizeFitter = gridPanelGo.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void BuildFeatureContent_UniLabButton(RectTransform parent)
    {
        AddFeatureHeader(parent, "UniLabButton", "Hold / Decide / State の Rx Observable を持つ拡張ボタン。");

        var buttonGo = new GameObject("UniLabButtonDemo", typeof(Image));
        buttonGo.transform.SetParent(parent, false);
        var buttonImage = buttonGo.GetComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.45f, 0.75f);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.sizeDelta = new Vector2(0f, 80f);

        var uniLabButton = buttonGo.AddComponent<UniLabButton>();
        uniLabButton.targetGraphic = buttonImage;

        CreateLabel(buttonRect, "Press & Hold Me", 22, TextAnchor.MiddleCenter, Color.white);

        // Wire reactive observables — use _featureDisposables so they are released on screen transition
        _featureDisposables.Add(uniLabButton.OnHoldAsObservable().Subscribe(_ => Log("[UniLabButton] OnHold")));
        _featureDisposables.Add(uniLabButton.OnDecideAsObservable().Subscribe(_ => Log("[UniLabButton] OnDecide")));
        _featureDisposables.Add(
            uniLabButton.StateAsObservable()
                .Where(state => state != ButtonState.None)
                .Subscribe(state => Log($"[UniLabButton] State → {state}")));
    }

    private void BuildFeatureContent_AnimationPlayer(RectTransform parent)
    {
        AddFeatureHeader(parent, "AnimationPlayer", "Animator の非同期再生と OnPlay / OnComplete Observable のラッパー。");
        AddFeatureButtonRow(parent,
            ("Setup Demo", (Action)DemoAnimationPlayerSetup));
    }

    private void BuildFeatureContent_Toast(RectTransform parent)
    {
        AddFeatureHeader(parent, "Toast", "[Prefab Required] _hasToastManager を有効化してから使用。");
        AddFeatureButtonRow(parent,
            ("Info", (Action)DemoToastInfo),
            ("Success", DemoToastSuccess),
            ("Error", DemoToastError));
    }

    private void BuildFeatureContent_Loading(RectTransform parent)
    {
        AddFeatureHeader(parent, "Loading", "[Prefab Required] _hasLoadingManager を有効化してから使用。");
        AddFeatureButtonRow(parent,
            ("Show 1.5 s", (Action)DemoLoadingOverlay));
    }

    private void BuildFeatureContent_BackKey(RectTransform parent)
    {
        AddFeatureHeader(parent, "BackKey", "Android バックキーの Observable ラッパー。IsBlocked でブロック可能。");
        AddFeatureButtonRow(parent,
            ("Toggle Block", (Action)DemoBackKeyToggleBlock));
    }

    // =========================================================
    // Feature screen layout helpers
    // =========================================================

    private void AddFeatureHeader(RectTransform parent, string title, string description)
    {
        // Feature title
        var titleGo = new GameObject("FeatureTitle", typeof(Text));
        titleGo.transform.SetParent(parent, false);
        var titleText = titleGo.GetComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.text = title;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 52f);

        // Description
        var descGo = new GameObject("Description", typeof(Text));
        descGo.transform.SetParent(parent, false);
        var descText = descGo.GetComponent<Text>();
        descText.font = _font;
        descText.fontSize = 20;
        descText.color = new Color(0.7f, 0.7f, 0.7f);
        descText.text = description;
        descText.alignment = TextAnchor.UpperLeft;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Overflow;
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.sizeDelta = new Vector2(0f, 48f);

        // Divider
        var dividerGo = new GameObject("Divider", typeof(Image));
        dividerGo.transform.SetParent(parent, false);
        dividerGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
        var dividerRect = dividerGo.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.sizeDelta = new Vector2(0f, 2f);
    }

    private void AddFeatureButtonRow(RectTransform parent, params (string label, Action onClick)[] buttons)
    {
        var rowGo = new GameObject("ButtonRow", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        var rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 80f);

        var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        foreach (var (label, onClick) in buttons)
        {
            CreateFeatureButton(rowGo.transform, label, onClick);
        }
    }

    private void CreateFeatureButton(Transform parent, string label, Action onClick)
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

        CreateLabel(buttonGo.GetComponent<RectTransform>(), label, 22, TextAnchor.MiddleCenter, Color.white);
    }

    // =========================================================
    // UI helpers
    // =========================================================

    // Labels are purely visual — never used as raycast targets.
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
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 0f);
        rect.offsetMax = new Vector2(-4f, 0f);
        return label;
    }

    // Background panels are purely visual — never used as raycast targets.
    // Button GameObjects are created separately and keep the default raycastTarget = true.
    private RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private ScrollRect BuildScrollRect(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        // Scroll root: transparent Image is visual only; raycastTarget = false.
        // The Viewport Image below keeps raycastTarget = true so that ScrollRect
        // receives drag/scroll events when the content area is empty.
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image));
        scrollGo.transform.SetParent(parent, false);
        var scrollRootImage = scrollGo.GetComponent<Image>();
        scrollRootImage.color = new Color(0f, 0f, 0f, 0f);
        scrollRootImage.raycastTarget = false;
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
