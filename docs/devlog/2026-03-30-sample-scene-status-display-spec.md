# Sample Scene Status Display — 実装仕様

## 目的

各機能デモ画面に「現在の状態を視覚的に示すステータスパネル」を追加する。
ボタンを押した結果がログだけでなく画面上の UI で確認できるようにする。

---

## 変更概要

### 新規フィールド（`UniLabSampleScene` クラス内）

```csharp
// Status display for the currently shown feature screen.
// Set by BuildFeatureContent_*, updated by Demo* methods.
private Image _featureStatusImage;
private Text  _featureStatusText;
```

### 新規定数

```csharp
private static readonly Color StatusColorNeutral  = new Color(0.12f, 0.12f, 0.18f);
private static readonly Color StatusColorSuccess  = new Color(0.10f, 0.28f, 0.15f);
private static readonly Color StatusColorWarning  = new Color(0.32f, 0.20f, 0.08f);
private static readonly Color StatusColorError    = new Color(0.28f, 0.10f, 0.10f);
```

### 新規ヘルパー

```csharp
/// <summary>
/// Adds a status display panel (140px tall) to the feature content area.
/// Sets _featureStatusImage and _featureStatusText for later updates.
/// </summary>
private void AddStatusDisplay(RectTransform parent, string initialText, Color initialColor)
```

実装内容:
- `GameObject("StatusPanel", typeof(Image))` を追加、`raycastTarget = false`
- `anchorMin=(0,1), anchorMax=(1,1), pivot=(0.5,1), sizeDelta=(0,140)`
- Image に initialColor を設定
- 内部に `CreateLabel` (font 20px, TextAnchor.UpperLeft, Color.white)、padding offsetMin=(12,8) offsetMax=(-12,-8)
- `_featureStatusImage` と `_featureStatusText` にセット

```csharp
/// <summary>Updates the status panel color and text.</summary>
private void UpdateStatus(string text, Color color)
{
    if (_featureStatusImage != null) _featureStatusImage.color = color;
    if (_featureStatusText  != null) _featureStatusText.text   = text;
}
```

---

## 各機能の変更仕様

### 1. LocalSave

**BuildFeatureContent_LocalSave:**
- `AddFeatureHeader` の後、`AddStatusDisplay` (初期テキスト `RefreshLocalSaveStatus()` 相当、Neutral) を追加
- ボタン行は既存のまま

**新規プライベートメソッド `RefreshLocalSaveStatus()`:**
```
var loaded = LocalSave.Load<SampleSaveData>();
if (loaded.Counter == 0 && loaded.Label == null)
    UpdateStatus("No data saved.", StatusColorNeutral);
else
    UpdateStatus($"Counter : {loaded.Counter}\nLabel   : {loaded.Label}", StatusColorSuccess);
```

**DemoLocalSaveSave:** 既存ロジック後に `RefreshLocalSaveStatus()` を呼ぶ。

**DemoLocalSaveDelete:** 既存ロジック後に `RefreshLocalSaveStatus()` を呼ぶ。

---

### 2. EncryptedStorage

**BuildFeatureContent_EncryptedStorage:**
- `AddStatusDisplay` (初期: `"demo_key: Not found"`, Error) を追加

**DemoEncryptedStorage (Save & Load):**
```
// 既存処理後
UpdateStatus("demo_key: ✓ Saved\nCounter : 99 / Label : secret", StatusColorSuccess);
```

**DemoEncryptedStorageDelete:**
```
// 既存処理後
UpdateStatus("demo_key: — Not found", StatusColorError);
```

---

### 3. TextManager

**BuildFeatureContent_TextManager:**
- `AddStatusDisplay` (初期: `"Language: JA\napp_title = [tap button]"`, Neutral) を追加

**DemoTextManagerJa:**
```
TextManager.SetLanguage("ja");
var text = TextManager.GetText("app_title");
UpdateStatus($"Language : JA\napp_title = {text}", StatusColorSuccess);
Log($"[Text] ja: app_title = {text}");
```

**DemoTextManagerEn:**
```
TextManager.SetLanguage("en");
var text = TextManager.GetText("app_title");
Log($"[Text] en: app_title = {text}");
TextManager.SetLanguage("ja");  // restore
UpdateStatus($"Language : EN\napp_title = {text}", StatusColorSuccess);
```

---

### 4. InputBlock

**BuildFeatureContent_InputBlock:**
- `AddStatusDisplay` (初期: `"Input: Open"`, Success) を追加

**DemoToggleInputBlock:**
- 既存の toggle ロジックを維持しつつ、直後に下記を呼ぶ:
```
if (InputBlockManager.BlockedInput)
    UpdateStatus($"Input: BLOCKED\nDepth: {InputBlockManager.BlockCount}", StatusColorError);
else
    UpdateStatus("Input: Open", StatusColorSuccess);
```
※ `InputBlockManager.BlockCount` が存在しない場合は depth 表示を省略。

---

### 5. Network

**BuildFeatureContent_Network:**
- `AddStatusDisplay` (初期: `$"Reachability: {Application.internetReachability}"`, Neutral) を追加

**DemoNetworkReachability:**
```
var r = Application.internetReachability;
Log($"[Network] Current: {r}");
switch (r)
{
    case NetworkReachability.ReachableViaLocalAreaNetwork:
        UpdateStatus("Reachability: WiFi ✓", StatusColorSuccess); break;
    case NetworkReachability.ReachableViaCarrierDataNetwork:
        UpdateStatus("Reachability: Mobile ✓", StatusColorWarning); break;
    default:
        UpdateStatus("Reachability: Not Reachable ✗", StatusColorError); break;
}
```

---

### 6. OfflineQueue

**BuildFeatureContent_OfflineQueue:**
- `AddStatusDisplay` (初期: `"Queue: 0 items"`, Neutral) を追加

**RefreshOfflineQueueStatus (新規プライベートメソッド):**
```
var count = _offlineQueue.Count;
UpdateStatus(count == 0 ? "Queue: empty" : $"Queue: {count} item(s) pending",
             count == 0 ? StatusColorNeutral : StatusColorWarning);
```

**DemoOfflineQueueEnqueue / DemoOfflineQueueClear:**
- 既存処理後 `RefreshOfflineQueueStatus()` を呼ぶ。

**FlushOfflineQueueAsync:**
- flush 開始時: `UpdateStatus("Flushing...", StatusColorWarning)`
- flush 完了時: `RefreshOfflineQueueStatus()`

---

### 7. Grid

Grid には既存のビジュアルデモ（アイテムが並ぶパネル）があるため、
ステータスパネルは「Items: N」のカウンター表示のみ。

**BuildFeatureContent_Grid:**
- `AddStatusDisplay` (初期: `"Items: 0"`, Neutral) を追加（グリッドパネルの前）

**DemoGridAddItem / DemoGridClear:**
- 既存処理後 `UpdateStatus($"Items: {_gridChildCount}", _gridChildCount == 0 ? StatusColorNeutral : StatusColorSuccess)` を呼ぶ。

---

### 8. UniLabButton

ボタン操作中のリアルタイム状態表示。

**BuildFeatureContent_UniLabButton:**
- `AddStatusDisplay` (初期: `"State: None"`, Neutral) を追加
- `StateAsObservable()` の Subscribe を以下に変更:
```csharp
_featureDisposables.Add(
    uniLabButton.StateAsObservable()
        .Subscribe(state =>
        {
            Log($"[UniLabButton] State → {state}");
            var (text, color) = state switch
            {
                ButtonState.Pressed  => ("State: Pressed",  StatusColorSuccess),
                ButtonState.Holding  => ("State: Holding",  StatusColorWarning),
                ButtonState.Decided  => ("State: Decided ✓", StatusColorSuccess),
                _                    => ("State: None",      StatusColorNeutral),
            };
            UpdateStatus(text, color);
        }));
```
※ `ButtonState` の値名は実際の enum に合わせること。

---

### 9. AnimationPlayer

**BuildFeatureContent_AnimationPlayer:**
- `AddStatusDisplay` (初期: `"IsPlaying: —\n(tap Setup to initialize)"`, Neutral) を追加

**DemoAnimationPlayerSetup:**
- 既存の Subscribe に加え:
```
UpdateStatus("IsPlaying: false\nOnPlay / OnComplete subscribed", StatusColorSuccess);
player.OnPlay.Subscribe(_ => UpdateStatus("IsPlaying: true", StatusColorSuccess))
    を _disposables に追加 (featureDisposables ではなく _disposables — player は _disposables.Add で管理)
player.OnComplete.Subscribe(_ => UpdateStatus("IsPlaying: false", StatusColorNeutral))
    を同様に追加
```
NOTE: `player` は DemoAnimationPlayerSetup 内のローカル変数のため、
Subscribe を `_disposables` に追加するのは不自然。
代わりに `_featureDisposables` に追加し、setupDone フラグで二重 setup を防ぐ。

---

### 10. Toast

**BuildFeatureContent_Toast:**
- `AddStatusDisplay` (初期: `"Last toast: (none)"`, Neutral) を追加

**ShowToast(string message, ToastType type):**
- 成功時: `UpdateStatus($"Last: {type}\n\"{message}\"", StatusColorSuccess)`
- スキップ時: `UpdateStatus("⚠ _hasToastManager is disabled.\nEnable in Inspector.", StatusColorWarning)`

---

### 11. Loading

**BuildFeatureContent_Loading:**
- `AddStatusDisplay` (初期: `"Overlay: Hidden"`, Neutral) を追加

**ShowLoadingAsync:**
- `show` 直後: `UpdateStatus("Overlay: Visible", StatusColorWarning)`
- `using` を抜けた後: `UpdateStatus("Overlay: Hidden", StatusColorNeutral)`
- `catch` 内: `UpdateStatus($"Error: {exception.Message}", StatusColorError)`
- スキップ時 (`!_hasLoadingManager`): `UpdateStatus("⚠ _hasLoadingManager is disabled.\nEnable in Inspector.", StatusColorWarning)`

---

### 12. BackKey

**BuildFeatureContent_BackKey:**
- `AddStatusDisplay` (初期: `"Back Key: Active"`, Success) を追加

**DemoBackKeyToggleBlock:**
- 既存処理後:
```
UpdateStatus(manager.IsBlocked ? "Back Key: BLOCKED" : "Back Key: Active",
             manager.IsBlocked ? StatusColorError : StatusColorSuccess);
```

---

## レイアウト規約（設計ルール）

- ステータスパネルは `AddFeatureHeader` の直後、アクションボタンの前に置く
- 高さ固定 140px（必要に応じて Content の折り返しは自動）
- `raycastTarget = false`（インタラクティブではない）
- 色で状態を伝える: Success=緑, Warning=橙, Error=赤, Neutral=暗色

---

## コーディング規約（Codex 向け）

- ブロック namespace を使用 (`namespace UniLab.Sample { ... }`)
- `{}` は if/for/foreach/switch 全てに付ける
- 変数名の省略形は禁止 (`img` → `image`, `txt` → `text`, `go` → `gameObject`)
  ただし `Unity GameObject 慣習の Go サフィックス` はそのまま維持する（`buttonGo` 等）
- public メンバーには必ず `/// <summary>` を付ける
- 非インタラクティブ Image は `raycastTarget = false`
- Text は常に `raycastTarget = false`
- `_featureDisposables` に Subscribe を追加する際は必ず `.Add()` で管理する
