# UniLab.AssetVault 利用ガイド

Addressables の生 API をアプリ層から隠蔽する配信基盤 `UniLab.AssetVault` の概要・使い方・運用を1枚にまとめたガイド。

- 設計の詳細仕様: [design-unilab-asset-vault.md](design-unilab-asset-vault.md)
- 全体方針（疎結合・C# 9 制約など）: [design-unilab-foundation-overview.md](design-unilab-foundation-overview.md)

---

## 概要

アプリ層は `IAssetVaultService` と `IAssetScope` のみを参照する。`UnityEngine.AddressableAssets` への依存は基盤アセンブリ内に閉じており、配信先（CCD / Supabase Storage / 自前 CDN）の違いはランタイムコードに現れない。

主な機能:

- リモートカタログの更新チェック（差分検知）と適用
- ラベル単位の差分ダウンロードサイズ取得・事前ダウンロード
- R3 Observable による進捗・状態通知
- スコープ単位のハンドル一括解放（Release 漏れの構造的防止）
- バンドルキャッシュのクリア

---

## アセンブリ構成

| アセンブリ | 役割 |
|---|---|
| `UniLab.AssetVault` | ランタイム本体（IF・Model・実装） |
| `UniLab.AssetVault.Editor` | Dashboard・ビルド/セットアップ操作・状態取得 |
| `UniLab.AssetVault.Debug` | デバッグ環境上書き（dev ビルドのみ。`UNITY_EDITOR \|\| DEVELOPMENT_BUILD`） |

参照: `Logger` / `R3.Unity` / `UniTask` / `UniTask.Addressables` / `Unity.Addressables` / `Unity.ResourceManager`。

> **C# 9 制約**: 本プロジェクトは Unity 6000.4（C# 9 まで）。`record` / `record struct` は使用不可。値型は `readonly struct` で実装している。

---

## 公開 API

### IAssetVaultService

| メンバー | 用途 |
|---|---|
| `State` | `ReadOnlyReactiveProperty<AssetVaultState>`。ローディング UI の出し分けに購読する |
| `OnDownloadProgress` | `Observable<DownloadProgress>`。`DownloadAsync` 実行中のみ発火。OnError は流さない |
| `InitializeAsync` | 起動時に1回。Addressables 初期化 + カタログロード |
| `CheckForUpdatesAsync` | カタログ更新確認 → 更新があれば適用し `CatalogUpdateInfo` を返す |
| `GetDownloadSizeAsync` | ラベル群の未取得分サイズ。0 ならダウンロード不要 |
| `DownloadAsync` | ラベル群の事前ダウンロード。進捗は `OnDownloadProgress` で通知 |
| `CreateScope` | 画面/シーン単位の `IAssetScope` を生成 |
| `ClearCacheAsync` | バンドルキャッシュのクリア |

### IAssetScope（`IDisposable`）

| メンバー | 用途 |
|---|---|
| `LoadAssetAsync<T>` | キー指定でアセットをロード。ハンドルはスコープが追跡 |
| `InstantiateAsync` | キー指定で GameObject を生成。ハンドルはスコープが追跡 |
| `Dispose` | スコープ内の全ハンドルを一括 Release |

### 状態遷移

```
NotInitialized → Initializing → Ready
Ready → Downloading → Ready
Initializing/Downloading → Failed（例外時）
Failed → Initializing（リトライ）
```

キャンセル（`OperationCanceledException`）は失敗扱いにせず素通しし、State は健全な状態（Initialize 時は `NotInitialized`、Download 時は `Ready`）へ戻す。

---

## 使い方

### 1. DI 登録（VContainer）

サービスは Singleton、スコープは画面の LifetimeScope で `Scoped` 登録する。Scoped Dispose で画面破棄＝アセット解放が保証される。

```csharp
// RootLifetimeScope
builder.Register<IAssetVaultService, AddressablesAssetVaultService>(Lifetime.Singleton);

// SceneLifetimeScope
builder.Register(resolver =>
        resolver.Resolve<IAssetVaultService>().CreateScope(),
    Lifetime.Scoped);
```

### 2. 起動フロー

確認ダイアログ・進捗 UI はアプリ層の責務。基盤は判断材料（サイズ・進捗）を返すだけ。

```csharp
await _assetVault.InitializeAsync(cancellationToken);

var update = await _assetVault.CheckForUpdatesAsync(cancellationToken);
if (update.HasUpdate)
{
    var labels = new[] { "preload" };
    var sizeBytes = await _assetVault.GetDownloadSizeAsync(labels, cancellationToken);
    if (sizeBytes > 0)
    {
        // ここでアプリ層が「○○MB ダウンロードします」確認 UI を出す
        await _assetVault.DownloadAsync(labels, cancellationToken);
    }
}
```

### 3. 状態・進捗の購読

```csharp
_assetVault.State
    .Subscribe(state => _loadingView.SetVisible(state == AssetVaultState.Downloading))
    .AddTo(_disposables);

_assetVault.OnDownloadProgress
    .Subscribe(progress => _progressBar.Value = progress.Ratio)
    .AddTo(_disposables);
```

### 4. アセットのロード（必ずスコープ経由）

```csharp
var logo = await _assetScope.LoadAssetAsync<Sprite>("title_logo", cancellationToken);
var character = await _assetScope.InstantiateAsync("title_character", parent, cancellationToken);
// 画面の LifetimeScope 破棄時にハンドルは自動 Release
```

### エラーの扱い

- ネットワーク断・カタログ取得失敗・キー不在は `AssetVaultException`（`InnerException` に Addressables の元例外）
- 「更新なし」「サイズ0」などビジネス正常系は例外でなく戻り値で表現する

## 差分配信の運用

Addressables 標準の Content Update Workflow に乗せている。差分は**カタログ層**と**バンドル層**の2段で効く。

### グループ分割（前提）

| グループ | 用途 | 変更可否 |
|---|---|---|
| Cannot Change Post Release | アプリ同梱の静的コンテンツ | リリース後変更不可 |
| Can Change Post Release | サーバ配信・差し替え対象 | 差分配信対象 |

### ビルド（`UniLab/AssetVault/Build` メニュー）

- **New Build**: フルビルド。`addressables_content_state.bin` が出力される。**これをリリースごとに保管する**のが差分管理の生命線（CI でアーティファクト保存推奨）
- **Update a Previous Build (Diff)**: 保管した `content_state.bin` を基準に、変更アセットのバンドルと新カタログだけを再ビルド

### 配信先

Profile の RemoteLoadPath が指す配信先に、新カタログ + 変更バンドルだけをアップロードする。配信先 URL はランタイムが知らない（Profile で抽象化）。

### ランタイムの差分検知

1. `CheckForUpdatesAsync` がローカルとリモートのカタログ hash を比較 → 差分があればカタログを更新
2. `GetDownloadSizeAsync` が未キャッシュ分（＝実際に落とす分）だけのサイズを返す
3. `DownloadAsync` がコンテンツハッシュ化されたバンドルのうち変更分だけ取得（未変更分はキャッシュ流用）

中断耐性: `DownloadAsync` 失敗時も途中までのキャッシュは保持され、再呼び出しで差分から再開される。

### 実ビルド配信モード（Download/Clear Cache を試す）

1. `UniLab/AssetVault/Build > New Build`
2. Addressables Groups ウィンドウの Play Mode Script を「Use Existing Build (requires built groups)」に切替
3. Play → Download でバンドルがキャッシュされ、Clear Cache が機能する

### 環境切り替え

環境（dev / staging / prod）は Addressables Profile ではなく、実行時に `AssetVaultRuntime.BaseUrl`（env → URL のマッピングはアプリ config が持つ）で切り替える。Profile は1つで足りるため、旧 `UniLab/AssetVault/Profile` メニューは廃止した。

QA で「prod アプリで dev のアセットを見る」を試す場合は、Debug Override で環境の `BaseUrl` だけを上書きする。**ContentPath（版）は上書きせず version.json 解決に任せる**（＝その環境の最新公開版を読む）。

有効化・プリセット選択は UI では行えず、**コードからのみ**設定する。QA/開発コードで `AssetVaultDebugEnvironmentSettings.Activate("Staging")` を呼べば有効化＋選択、`Deactivate()` で無効化となる。Dashboard の **Debug Override** セクションは `Edit Presets`（設定アセットを開く）のみを提供する。

プリセット（表示名・BaseUrl）・有効/無効・選択名は `AssetVaultDebugEnvironmentSettings`（ScriptableObject、`Assets/UniLab/AssetVault/Debug/` 配下、gitignore 対象）に保持する。Inspector ではプリセット一覧のみ編集でき、有効/選択は読み取り専用表示。初回は Development / Staging / Production の雛形がシードされるので、`Edit Presets` で開いて実際の CDN ホストに書き換える。

この機能は専用アセンブリ `UniLab.AssetVault.Debug`（`defineConstraints: UNITY_EDITOR || DEVELOPMENT_BUILD`）に属し、**Editor Play と development ビルドでのみ有効**、release ビルドではコードごとストリップされる。適用は `AssetVaultDebugBootstrap`（`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`）が行い、アプリ初期化前に `AssetVaultRuntime.BaseUrl` のみへ反映する（ContentPath は触らない。厳密な前後はアプリ責務）。

設定アセットの正本は Resources の外に置くため、何もしなければプレイヤービルドに同梱されない。`AssetVaultDebugBuildProcessor` が **development ビルド時のみ** Resources へ一時複製し、ビルド後に除去する。これにより release ビルドにはコードもアセットも含まれない。

### ダッシュボード

`UniLab > AssetVault > Dashboard` で、Setup（設定アセットを開く）/ Build（New Build・Content Update）/ Debug Override / Status（RemoteLoadPath 現値・Local/Remote グループ数・Local/Remote フォルダパス）を一望・操作できる。各ボタンには説明文を併記。各操作は MenuItem からも実行可能。

#### 同期対象フォルダ（Sync AssetResource）

同期対象は設定アセット `AssetVaultSetupSettings` の **Local フォルダ（必須）** と **Remote フォルダ（任意）** の2スロットで指定する（いずれも `DefaultAsset` 参照、フォルダ名は分類に影響しない）。`Sync AssetResource` は各フォルダ配下を走査し、サブフォルダ単位で Addressables グループ（`Local_<名>` / `Remote_<名>`）を生成する。Local が未設定の場合は中断し、Remote が未設定の場合は Remote 同期だけスキップする。

操作は `Open Setup Settings`（Dashboard または `UniLab/AssetVault/Setup/Open Setup Settings`）で設定アセットを開き、**その Inspector でフォルダ指定と Sync ボタンを完結**させる。入口の重複を避けるため、Sync は設定 Inspector の1か所のみに置く（Dashboard・MenuItem には Sync を置かない）。
