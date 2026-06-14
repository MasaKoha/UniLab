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
| `UniLab.AssetVault.Editor` | ビルドメニュー・Profile 切り替え |
| `UniLab.AssetVault.Sample` | 動作確認サンプル（MVP・uGUI コード生成） |
| `UniLab.AssetVault.Sample.Editor` | サンプル用プレースホルダ生成メニュー |

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

---

## サンプル

`Assets/UniLab/AssetVault/Sample/` に DI なし単体・uGUI コード生成の動作確認サンプルがある。

1. 空 GameObject に `AssetVaultSampleBootstrap` を付ける（`_downloadLabel` / `_assetKey` を Inspector で変更可）
2. Play → State 初期表示は `NotInitialized`
3. **Initialize → Check And Download → Load Asset → Clear Cache** の順に操作

### プレースホルダ生成（初回のみ）

リポジトリには Addressables 設定とプレースホルダ Sprite を同梱済みで、クローン後そのまま Play で動く。設定をやり直したい場合はメニューから再生成できる:

- **`UniLab/AssetVault/Sample/Generate Placeholder Asset`**
  - Addressables 設定を生成/取得
  - 市松模様 256×256 PNG を `Sample/Generated/sample_sprite.png` に生成し Sprite としてインポート
  - address `sample_sprite` / label `sample` で Default Group に登録
  - Play Mode Script を「Use Asset Database (fastest)」に切り替え

> サンプル既定の Play Mode（Use Asset Database）ではバンドル/カタログが存在しないため、**Initialize と Load Asset のみ**が意味を持つ。Download・Clear Cache の実体を試すには下記「実ビルド配信モード」に切り替える。

---

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

QA で「prod アプリで dev のアセットを見る」「特定の版フォルダを読む」を試す場合は、`UniLab > AssetVault > Dashboard` ダッシュボードの **Debug Override** セクションで環境プリセットをドロップダウンから選び、Enable Override を有効化する。Play 突入時に選択プリセットの `BaseUrl` / `ContentPath` が `AssetVaultRuntime` へ反映される。

プリセットは `AssetVaultDebugEnvironmentSettings`（`Assets/Generated/UniLab/` 配下、未追跡）の ScriptableObject が持つ。初回は Development / Staging / Production の雛形がシードされるので、`Edit Presets` ボタンで開いて実際の CDN ホストに書き換える。有効/無効と選択プリセット名は EditorPrefs に保持され、開発者ごとに独立する。

### ダッシュボード

`UniLab > AssetVault > Dashboard` で、Setup（AssetResource 同期・設定アセットを開く）/ Build（New Build・Content Update）/ Sample（プレースホルダ生成）/ Debug Override / Status（RemoteLoadPath 現値・Local/Remote グループ数・AssetResource フォルダ有無）を一望・操作できる。各操作は MenuItem からも実行可能。
