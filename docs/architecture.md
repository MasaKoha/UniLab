# UniLab アーキテクチャ設計方針
作成日: 2026-03-28

---

## 概要

UniLab はスマートフォンゲームのアウトゲーム基盤として共通利用できる Unity ライブラリ。
以下の設計方針に従い実装する。

---

## アーキテクチャ方針

### MVP パターン

- View = MonoBehaviour ラッパーのみ。UI 要素の参照と表示更新のみ担当
- Presenter = 純粋 C# クラス。VContainer で注入。MonoBehaviour 継承禁止
- Repository = 純粋 C# クラス。データの取得・保存・変換を担当

### 依存方向

```
View（MonoBehaviour ラッパー）
  ↕ IXxxView インターフェース
Presenter（純粋 C# クラス、VContainer 注入）
  ↕ IXxxRepository インターフェース
Repository（純粋 C# クラス、VContainer 注入）
```

---

## 使用ライブラリ

| ライブラリ | 取得方法 | 用途 |
|---|---|---|
| VContainer | git URL（manifest.json 追加済み） | DI コンテナ |
| R3 | git URL（manifest.json 追加済み） | Reactive Programming |
| UniTask | git URL（manifest.json 追加済み） | 非同期処理 |
| DOTween | UnityPackage（手動インストール） | アニメーション（#if DOTWEEN） |

---

## コーディング規約

### 命名規則

| 対象 | 規則 | 例 |
|---|---|---|
| クラス / 構造体 | PascalCase | `ScreenManagerBase` |
| インターフェース | I + PascalCase | `IScreenManager` |
| パブリックメソッド | PascalCase | `ShowScreen<T>()` |
| プライベートフィールド | _camelCase | `_disposables` |
| SerializeField | _camelCase | `_submitButton` |
| ローカル変数 | camelCase | `screenView` |

### 禁止事項

- MonoBehaviour への Presenter ロジック直書き禁止
- GetComponent<T>() 禁止（SerializeField で事前アサイン必須）
- async void 禁止（async UniTaskVoid / async UniTask に統一）
- LINQ クエリ構文禁止（メソッド構文に統一）
- 変数名・メソッド名の省略形禁止（btn, mgr, cfg 等）
- if / else / for 等のブレース省略禁止
- public メンバーの summary 省略禁止
- Dispose 漏れ禁止（Subscribe は必ず AddTo で管理）

---

## VContainer 規約

- Presenter は `builder.Register<XxxPresenter>(Lifetime.Scoped)` のみ
- `RegisterComponentInHierarchy<T>()` 禁止
- View は `RegisterInstance` でインターフェースとして登録
- LifetimeScope は `AppLifetimeScope`（Boot）→ `SceneLifetimeScope`（各シーン）の2階層

---

## R3 規約

- Subject は必ず IObservable<T> にラップして公開する
- 状態（現在値が必要）→ ReactiveProperty、イベント（発火のみ）→ Subject
- Subscribe のラムダは3行以内。それ以上はメソッド抽出
- MonoBehaviour → AddTo(this)、純粋 C# クラス → AddTo(_disposables)
- Catch を必ず入れてストリームが終了しないようにする
- ObserveEveryValueChanged 禁止

---

## UniTask 規約

- async void 完全禁止
- MonoBehaviour の CancellationToken → GetCancellationTokenOnDestroy()
- Presenter の CancellationToken → CancellationTokenSource を明示的に管理
- UniTask.WhenAll で並列処理を明示的に記述する

---

## エラーハンドリング

| エラー種別 | 対応 |
|---|---|
| 通信エラー・予期しない例外 | Exception（R3 の Catch で補足） |
| ビジネスエラー（バリデーション失敗等） | Result<T> 型 |

---

## モジュール一覧

### 実装済み（改修対象含む）

| モジュール | 主要クラス | 状態 |
|---|---|---|
| SceneManager | `UniLabSceneManagerBase`・`ScreenManagerBase`・`SceneMainBase`・`ScreenBase` | 大幅改修（scenemanager-refactor.md 参照） |
| Popup | `PopupManagerBase`・`PopupBase` | そのまま流用 |
| LocalSave | `LocalSave` | そのまま流用 |
| MasterData | `MasterManager`・`MasterCatalog`・`MasterBase` | そのまま流用 |
| LocalNotification | `MobileLocalNotification` | そのまま流用 |
| Sound | `SoundPlayManager` | そのまま流用 |
| UIComponent | `UISafeArea`・`UniLabButton` | そのまま流用 |
| Utility | `AesEncryptionUtility`・`StringUtility` | そのまま流用 |

---

### 拡張・新規追加予定

#### UniLab.UI

| クラス | 概要 |
|---|---|
| `UniLabDialogManager` | 確認ダイアログ・アラート共通管理（PopupManagerBase 改良） |
| `ToastManager` | 画面下部トースト通知 |
| `LoadingOverlayManager` | 全画面ローディング（スピナー） |
| `SceneFadeTransition` | シーン遷移フェードエフェクト |
| `CoachMarkManager` | チュートリアル用コーチマーク |
| `ErrorDialogPresenter` | 通信エラー・強制ログアウト等の統一エラーUI |
| `KeyboardLayoutAdjuster` | 入力フィールド表示時のレイアウト自動調整 |

#### UniLab.Network

| クラス | 概要 |
|---|---|
| `ApiClientBase` | リトライ・タイムアウト・トークン自動更新付き HTTP クライアント基底 |
| `NetworkReachabilityObservable` | ネットワーク状態監視（R3 Observable） |
| `OfflineQueue<T>` | オフライン時のリクエストキューイング |

#### UniLab.Auth

| クラス | 概要 |
|---|---|
| `IAuthService` | 認証サービスインターフェース |
| `SupabaseAuthService` | Supabase Auth 実装（#if UNILAB_SUPABASE） |

#### UniLab.Storage

| クラス | 概要 |
|---|---|
| `EncryptedLocalStorage` | 暗号化ローカルストレージ（LocalSave + AesEncryptionUtility 改良） |
| `PlayerPrefsWrapper<T>` | 型安全 PlayerPrefs ラッパー |

#### UniLab.IAP

| クラス | 概要 |
|---|---|
| `IIAPService` | IAP サービスインターフェース |
| `UnityIAPService` | Unity IAP 実装（サブスク・レシート検証・リストア） |

#### UniLab.Analytics

| クラス | 概要 |
|---|---|
| `IAnalyticsService` | 分析サービスインターフェース |
| `FirebaseAnalyticsService` | Firebase Analytics 型安全ラッパー（#if UNILAB_FIREBASE） |
| `AdjustService` | Adjust イベント送信ラッパー（#if UNILAB_ADJUST） |

#### UniLab.App

| クラス | 概要 |
|---|---|
| `BootSequenceManager` | 初期化処理の順序制御（マスターDL → 認証 → シーン遷移） |
| `ForceUpdateChecker` | バージョン比較・強制アップデートダイアログ表示 |
| `AppReviewManager` | SKStoreReviewController / Google Play Review API ラッパー |
| `PermissionManager` | 通知・カメラ・トラッキング許可の統一管理 |
| `DeepLinkHandler` | iOS Universal Links / Android App Links 対応 |

#### UniLab.Debug

| クラス | 概要 |
|---|---|
| `DebugMenuSystem` | 開発用デバッグメニュー（環境切り替え・強制処理等） |
| `EnvironmentConfig` | dev / staging / prod 環境設定 |
| `BuildInfoDisplay` | バージョン・ビルド情報表示 |

#### UniLab.Logger

| クラス | 概要 |
|---|---|
| `IUniLabLogger` | ロガーインターフェース |
| `UniLabLogger` | Debug.Log ラッパー。Conditional 属性でリリースビルド自動除去 |

---

## Scripting Define Symbols

Player Settings > Other Settings > Scripting Define Symbols に手動追加する。

| シンボル | 対象 |
|---|---|
| `DOTWEEN` | DOTween（Setup 実行で自動追加） |
| `UNILAB_FIREBASE` | Firebase Analytics・Crashlytics・FCM 全て |
| `UNILAB_ADJUST` | Adjust |
| `UNILAB_SUPABASE` | Supabase C# SDK |

