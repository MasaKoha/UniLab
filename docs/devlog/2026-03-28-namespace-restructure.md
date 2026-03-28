# Namespace 再設計・フォルダ移動計画

作成: 2026-03-28
ステータス: 検討中

---

## 背景と問題意識

### 直近の経緯

`UniLab.LocalSave.LocalSave` / `UniLab.TextManager.TextManager` という「クラス名 == 名前空間末尾」の二重名を解消するために、両クラスを `namespace UniLab` に移動した。これ自体は二重名は消えたが、新たな問題を生んだ。

- `namespace UniLab` にコンポーネント横断のクラスが混在し、どのモジュールに属するか不明瞭になる
- 将来的に同様の「移動」が増えると `namespace UniLab` がゴミ箱になる
- `using UniLab;` 一行で何でも使えてしまい、依存関係が可視化されない

### 根本原因

「クラス名 == 名前空間末尾」問題の正しい解は、**クラスを親名前空間に逃がすことではなく、名前空間名を変えることで衝突を避ける**ことだった。

---

## 現状の名前空間一覧

### 問題あり

| 現状 | 問題 |
|---|---|
| `namespace UniLab` に `LocalSave` / `TextManager` クラス | コンポーネント分離が消えた |
| `UniLab.Feature.Animation/Banner/Indicator/MasterData` | `Feature` プレフィックスが余分。`UniLab.UI.Toast` など Feature なしのものと不統一 |
| `UniLab.Feature.UI.Toast/Loading/Transition/Popup` | `Feature.UI` と `UI` が混在 |
| `UniLab.Popup` (基底クラス群) と `UniLab.Feature.UI.Popup` (実装) | Popup が2つの名前空間に分裂 |
| `UniLab.Common.Display` (InputBlockManager 等) | Display という名は実態を表していない。UI 系なのに Common 配下 |
| `UniLab.Common.Sound` | Audio/Sound が Common 配下 |
| `UniLab.LocalSave.LocalSave` → 移動後は `UniLab.LocalSave` (namespace) + `UniLab.LocalSave` (class) の共存 | コンパイルは通るが概念が混濁する |
| `UniLab.TextManager.*` (namespace) と `UniLab.TextManager` (class) の共存 | 同上 |
| `LocalNofitication` フォルダのスペルミス | namespace は `UniLab.LocalNotification` で正しいが、フォルダ名と乖離 |

### 問題なし（現状維持）

| 名前空間 | 内容 |
|---|---|
| `UniLab.Auth` | 認証サービス群 |
| `UniLab.Common` | Singleton 基底クラス |
| `UniLab.Diagnostics` | EnvironmentConfig, BuildInfoDisplay |
| `UniLab.Network` | API クライアント, OfflineQueue |
| `UniLab.Scene` / `UniLab.Scene.Screen` | シーン・スクリーン管理 |
| `UniLab.Storage` | EncryptedLocalStorage, ILocalStorage |
| `UniLab.Tools.Editor` | エディタツール群 |
| `UniLab.UI` | UniLabButton, VariableGridLayoutGroup |

---

## 提案: 新名前空間設計

### 基本方針

1. **`Feature` プレフィックスを廃止** — すべて `UniLab.<Component>` に統一
2. **二重名の解消は namespace 名変更で行う** — `UniLab.LocalSave.LocalSave` ではなく `UniLab.Persistence.LocalSave` のように、名前空間名を変えてクラス名を保つ
3. **論理的な集約** — 用途が近いものをまとめる（`LocalSave` と `Storage` は `Persistence` に統合）
4. **フォルダ構造 == 名前空間構造** — `namespace A.B.C` なら `A/B/C/` に置く

### 新旧マッピング

| 旧名前空間 | 新名前空間 | 主な変更点 |
|---|---|---|
| `UniLab` (LocalSave / TextManager の仮置き場) | 廃止 | 後述の適切な場所へ移す |
| `UniLab.LocalSave` | `UniLab.Persistence` | namespace 名変更でクラス名衝突を回避 |
| `UniLab.LocalSave.Editor` | `UniLab.Persistence.Editor` | フォルダごと移動 |
| `UniLab.Storage` | `UniLab.Persistence` | LocalSave と統合 |
| `UniLab.TextManager` | `UniLab.Localization` | namespace 名変更 |
| `UniLab.TextManager.Editor` | `UniLab.Localization.Editor` | フォルダごと移動 |
| `UniLab.LocalNotification` | `UniLab.Notification` | `Local` プレフィックス除去、フォルダ名スペルミスも修正 |
| `UniLab.LocalNotification.Platform` | `UniLab.Notification.Platform` | 同上 |
| `UniLab.Feature.Animation` | `UniLab.Animation` | `Feature` 廃止 |
| `UniLab.Feature.Banner` | `UniLab.Banner` | `Feature` 廃止 |
| `UniLab.Feature.Indicator` | `UniLab.Indicator` | `Feature` 廃止 |
| `UniLab.Feature.MasterData` | `UniLab.MasterData` | `Feature` 廃止 |
| `UniLab.Feature.MasterData.Editor` | `UniLab.MasterData.Editor` | 同上 |
| `UniLab.Feature.UI.Toast` | `UniLab.UI.Toast` | `Feature` 廃止 |
| `UniLab.Feature.UI.Loading` | `UniLab.UI.Loading` | `Feature` 廃止 |
| `UniLab.Feature.UI.Transition` | `UniLab.UI.Transition` | `Feature` 廃止 |
| `UniLab.Feature.UI.Popup` | `UniLab.UI.Popup` | `Feature` 廃止 + `UniLab.Popup` と統合 |
| `UniLab.Popup` | `UniLab.UI.Popup` | Feature.UI.Popup と統合 |
| `UniLab.Common.Display` | `UniLab.UI` | InputBlockManager 等は UI 系なので UI 配下へ |
| `UniLab.Common.Input` | `UniLab.Input` | BackKeyInputManager を独立させる |
| `UniLab.Common.Sound` | `UniLab.Audio` | Sound → Audio、Common から独立 |

### 変更後の全名前空間一覧

```
UniLab.Animation            AnimationPlayer
UniLab.Audio                SoundPlayManager
UniLab.Auth                 IAuthService, AuthUser, SupabaseAuthService
UniLab.Banner               BannerViewBase, BannerCellBase
UniLab.Common               SingletonMonoBehaviour, SingletonPureClass
UniLab.Common.Utility       StringUtility, AesEncryptionUtility
UniLab.Diagnostics          EnvironmentConfig, BuildInfoDisplay
UniLab.Indicator            UniLabIndicatorManager, IndicatorCellBase, IndicatorManagerBase
UniLab.Input                BackKeyInputManager
UniLab.Localization         TextManager, LocalizedText, KeyHash, LocalizationData, LocalizationEntry
UniLab.Localization.Editor  LocalizationKeyDropdownDrawer, LocalizationImporterWindow, 他 Editor ツール
UniLab.MasterData           MasterManager, MasterBase, MasterCatalog, MasterCalculator
UniLab.MasterData.Editor    MasterLocalViewer
UniLab.Network              ApiClientBase, ApiException, NetworkReachabilityObservable, OfflineQueue
UniLab.Notification         MobileLocalNotification, NotificationPermissionStatus
UniLab.Notification.Platform  Android/iOS/Standalone 実装
UniLab.Persistence          LocalSave, EncryptedLocalStorage, PlayerPrefsWrapper, ILocalStorage
UniLab.Persistence.Editor   LocalSaveEditorWindow
UniLab.Scene                UniLabSceneManagerBase, ISceneManager, SceneMainBase, SceneParameterBase
UniLab.Scene.Editor         SceneNameEnumGenerator
UniLab.Scene.Screen         ScreenManagerBase, ScreenBase, ScreenPresenterBase, IScreenManager, IScreenView
UniLab.Tools.Editor         FavoriteAsset, OpenFolder, BuildChecker
UniLab.Tools.Editor.AssetReferenceFinder  (現状維持)
UniLab.Tools.Editor.MissingChecker        (現状維持)
UniLab.Tools.Editor.ProjectScanCommon     (現状維持)
UniLab.UI                   UniLabButton, VariableGridLayoutGroup, TemplatePrefabs, InputBlockManager, UIInputBlockingManagerBase, SwipeDetector, UISafeArea
UniLab.UI.Editor            UIComponentTemplateMenu
UniLab.UI.Loading           LoadingOverlayManager, ILoadingOverlayManager
UniLab.UI.Popup             PopupBase, PopupBaseWrapper, PopupManagerBase, UniLabPopupManager, ConfirmPopup, IPopupView, IPopupParameter, IPopupManager
UniLab.UI.Toast             ToastManager, ToastView, IToastManager
UniLab.UI.Transition        SceneFadeTransition, ISceneTransition
```

---

## フォルダ移動計画

名前空間と 1:1 対応するようフォルダを整理する。

```
Assets/UniLab/

  変更なし:
  Auth/               → UniLab.Auth
  Common/             → UniLab.Common (Singleton のみ残す)
  Common/Utility/     → UniLab.Common.Utility
  Diagnostics/        → UniLab.Diagnostics (現 Debug/ からリネーム済みのため)
  Network/            → UniLab.Network
  Scene/              → UniLab.Scene
  Tools/              → UniLab.Tools.Editor
  UIComponent/        → UniLab.UI (フォルダ名は UIComponent のまま可、もしくは UI/ にリネーム)

  リネーム:
  Debug/              → Diagnostics/     (namespace UniLab.Diagnostics に合わせる)
  LocalNofitication/  → Notification/    (スペルミス修正 + Local 除去)
  Feature/Animation/  → Animation/       (Feature 廃止)
  Feature/Banner/     → Banner/          (Feature 廃止)
  Feature/Indicator/  → Indicator/       (Feature 廃止)
  Feature/MasterData/ → MasterData/      (Feature 廃止)
  Feature/UI/Toast/   → UIComponent/Toast/ または UI/Toast/
  Feature/UI/Loading/ → UIComponent/Loading/ または UI/Loading/
  Feature/UI/Transition/ → UIComponent/Transition/ または UI/Transition/
  Feature/UI/Popup/   → UIComponent/Popup/ または UI/Popup/
  TextManager/        → Localization/    (namespace UniLab.Localization に合わせる)

  統合・移動:
  LocalSave/ + Storage/ → Persistence/  (両方を UniLab.Persistence に統合)
  Popup/              → UIComponent/Popup/ への統合  (UniLab.UI.Popup に統合)
  Common/Display/     → UIComponent/ への統合         (UniLab.UI に統合)
  Common/Input/       → Input/                       (UniLab.Input として独立)
  Common/Sound/       → Audio/                       (UniLab.Audio として独立)
```

---

## 実施順序（推奨）

影響範囲が小さく、依存が少ないものから着手する。

### Phase 1: 単純リネーム（依存なし）

1. `Debug/` → `Diagnostics/` フォルダリネーム ＋ namespace は既に `UniLab.Diagnostics` なのでコード変更不要
2. `LocalNofitication/` → `Notification/` フォルダリネーム ＋ `UniLab.LocalNotification` → `UniLab.Notification` に変更
3. `Feature/Animation/` → `Animation/` ＋ `UniLab.Feature.Animation` → `UniLab.Animation`
4. `Feature/Banner/` → `Banner/` ＋ namespace 変更
5. `Feature/Indicator/` → `Indicator/` ＋ namespace 変更

### Phase 2: Feature.UI の整理

6. `Feature/UI/Toast/` → `UIComponent/Toast/` ＋ `UniLab.UI.Toast`
7. `Feature/UI/Loading/` → `UIComponent/Loading/` ＋ `UniLab.UI.Loading`
8. `Feature/UI/Transition/` → `UIComponent/Transition/` ＋ `UniLab.UI.Transition`
9. `UniLab.Popup` + `Feature/UI/Popup/` を統合 → `UIComponent/Popup/` ＋ `UniLab.UI.Popup`

### Phase 3: LocalSave / TextManager の本命修正

10. `LocalSave/` + `Storage/` → `Persistence/` に統合
    - `namespace UniLab.LocalSave` → `UniLab.Persistence`
    - `namespace UniLab.LocalSave.Editor` → `UniLab.Persistence.Editor`
    - `namespace UniLab.Storage` → `UniLab.Persistence`
    - `LocalSave` クラスと `TextManager` クラスを `namespace UniLab` から各所に戻す
11. `TextManager/` → `Localization/`
    - `namespace UniLab.TextManager` → `UniLab.Localization`
    - `namespace UniLab.TextManager.Editor` → `UniLab.Localization.Editor`

### Phase 4: Common の分解

12. `Common/Display/` → `UIComponent/` への統合 ＋ `UniLab.Common.Display` → `UniLab.UI`
13. `Common/Input/` → `Input/` ＋ `UniLab.Common.Input` → `UniLab.Input`
14. `Common/Sound/` → `Audio/` ＋ `UniLab.Common.Sound` → `UniLab.Audio`

### Phase 5: MasterData

15. `Feature/MasterData/` → `MasterData/` ＋ `UniLab.Feature.MasterData` → `UniLab.MasterData`

---

## 未決事項・検討ポイント

1. **`UniLab.Common` の残存範囲**
   Phase 4 後は `SingletonMonoBehaviour` / `SingletonPureClass` だけが残る。これらは `UniLab.Common` に置くか、`UniLab` 直下に移すか。

2. **`UniLab.UI` への統合範囲**
   `InputBlockManager` / `SwipeDetector` / `UISafeArea` を `UniLab.UI` に入れると namespace が広くなりすぎる可能性がある。`UniLab.UI.Utility` 等のサブ namespace を作るか要検討。

3. **フォルダ名の英語表記**
   `UIComponent/` フォルダを `UI/` にリネームするかどうか。namespace は `UniLab.UI` なのでフォルダ名も `UI/` に揃えた方がシンプル。ただし Unity の `Resources/` や `Assets/` 等の特殊フォルダとの混在に注意。

4. **Assembly Definition (.asmdef) の対応**
   フォルダ移動時、.asmdef ファイルの参照も追従が必要。現状の .asmdef 構成を確認してから移動着手すること。

5. **`UniLabSampleScene.cs` の namespace**
   現在 `UniLab.Sample`。変更後は多数の `using` 変更が生じるが、Sample なので最後に更新すれば良い。

---

## 今回の暫定状態（作業中断時点）

- `LocalSave` クラス: `namespace UniLab` に仮置き中
- `TextManager` クラス: `namespace UniLab` に仮置き中
- `LocalSave.cs` 物理ファイル: `Assets/UniLab/LocalSave/` に存在（Phase 3 で `Persistence/` へ移動予定）
- `TextManager.cs` 物理ファイル: `Assets/UniLab/TextManager/Runtime/` に存在（Phase 3 で `Localization/` へ移動予定）

Phase 3 を最優先で実施することで、現在の `namespace UniLab` への仮置きが解消できる。
