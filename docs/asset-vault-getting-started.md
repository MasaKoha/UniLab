# AssetVault はじめかた（Getting Started）

導入からアプリで最初の 1 枚を表示するまでを、順番にたどる手引き。
- 仕様・API の俯瞰／運用 → [asset-vault-guide.md](asset-vault-guide.md)
- ロード・利用・解放の詳細コード → [asset-vault-usage.md](asset-vault-usage.md)
- 配信設計（version.json / CDN） → [design-unilab-asset-cdn.md](design-unilab-asset-cdn.md)

AssetVault は Addressables をアプリ層から隠す配信基盤。アプリは `IAssetVaultService` と `IAssetScope` だけを使う。

---

## 全体像（5 ステップ）

```
[1] エディタでセットアップ   … フォルダ → Addressables 構成（Dashboard / Sync）
[2] ビルド                   … New Build（差分は Content Update）
[3] DI 登録                  … Service=Singleton / Scope=Scoped
[4] 起動フロー               … Initialize → 更新確認 → 事前DL
[5] 画面でロード/利用/解放    … CreateScope → LoadAssetAsync → Dispose
```

[1][2] はエディタ作業（初回・アセット変更時）。[3][4][5] が実装。

---

## [1] セットアップ（エディタ）

1. `UniLab > AssetVault > Dashboard` を開く
2. **Setup** の `Open Setup Settings` で設定アセットを開く
3. **Local Folder（必須）** と **Remote Folder（任意）** にフォルダをドラッグ
   - Local = プレイヤー同梱、Remote = CDN 配信。フォルダ名は分類に無関係（スロットで決まる）
   - 各フォルダ直下のサブフォルダが Addressables グループ `Local_<名>` / `Remote_<名>` になる
4. 設定アセットの **`Sync AssetResource`** ボタンを押す
   - グループ・アドレス・プロファイルパスが自動構成される
   - アドレス = フォルダ相対・拡張子なし（例 `External/Icons/coin.png` → `Icons/coin`）
   - 重複アドレスがあると失敗するので解消して再実行

詳細・規約は [asset-vault-guide.md](asset-vault-guide.md) の「同期対象フォルダ」。

## [2] ビルド（エディタ）

Dashboard の **Build**：
- **New Build** … フルビルド。初回・グループ構成変更時。`content_state.bin` が出る（リリースごとに保管）
- **Content Update (Diff)** … 保管した state を基準に差分だけ再ビルド（配信済みアプリ向け）

---

## [3] DI 登録（VContainer）

Service は Singleton、Scope は画面の LifetimeScope で Scoped 登録。Scoped の Dispose ＝ 画面破棄でアセット解放が保証される。

```csharp
// RootLifetimeScope
builder.Register<IAssetVaultService, AddressablesAssetVaultService>(Lifetime.Singleton);

// SceneLifetimeScope（画面）
builder.Register(resolver =>
        resolver.Resolve<IAssetVaultService>().CreateScope(),
    Lifetime.Scoped);

// 共有・オブジェクトプール向けキャッシュ（任意。使う場合のみ）
builder.Register<IAssetVaultCache>(_ => new AssetVaultCache(), Lifetime.Singleton);
```

### 設計方針: DI（VContainer）前提

- **本基盤は DI 注入前提**。`IAssetVaultService` を VContainer で **Singleton ライフタイム**登録し、各 Presenter/View へ `[Inject]` で渡す。これが「単一インスタンス」の正しい実現方法。
- **`AddressablesAssetVaultService` はプレーンな class（非 MonoBehaviour）**。Unity ライフサイクルに依存しない。
- **`SingletonMonoBehaviour` / static Instance は採用しない**（グローバル可変状態でテスト困難・GameObject 寿命に結合・DI と二重管理になるため）。
- **どうしてもグローバル静的アクセス（注入なし呼び出し）が欲しいプロジェクトは、各プロジェクト側で実装する**。基盤は提供しない。実装する場合も MonoBehaviour 化はせず、起動時に1回だけ解決済みインスタンスを保持するプレーンな static ロケータに留めること（例: `MyAppAssetVault.Instance` を bootstrap でセット）。
- フィールド名は `IAssetVaultService` を保ちつつ、慣用的に `_assetVault` と短縮してよい（型名で意味は明確）。

## [4] 起動フロー

起動時に一度だけ初期化し、必要なら更新確認・事前ダウンロード。確認ダイアログや進捗 UI はアプリ層の責務（基盤は判断材料を返すだけ）。

```csharp
// baseUrl = env→URL（アプリ config が解決）。初期化前に BaseUrl を確定し、版は version.json 解決に任せる。
// Debug Override 有効時はそちらが優先。Local 専用なら baseUrl は空でよい。
await _assetVault.InitializeAsync(config.AssetBaseUrl, cancellationToken);

var update = await _assetVault.CheckForUpdatesAsync(cancellationToken);
if (update.HasUpdate)
{
    var labels = new[] { "preload" };
    var sizeBytes = await _assetVault.GetDownloadSizeAsync(labels, cancellationToken);
    if (sizeBytes > 0)
    {
        // 「○○MB ダウンロードします」確認 UI を出してから
        await _assetVault.DownloadAsync(labels, cancellationToken);
    }
}
```

状態・進捗はリアクティブに購読：

```csharp
_assetVault.State
    .Subscribe(state => _loadingView.SetVisible(state == AssetVaultState.Downloading))
    .AddTo(_disposables);

_assetVault.OnDownloadProgress
    .Subscribe(progress => _progressBar.Value = progress.Ratio)
    .AddTo(_disposables);
```

## [5] 画面でロード・利用・解放

### 標準: service 拡張（Scope を書かない・推奨）

owner は GameObject / Component どちらも可。

```csharp
public sealed class IconView : MonoBehaviour
{
    [Inject] private readonly IAssetVaultService _assetVault;
    [SerializeField] private Image _image;

    private async UniTask ShowAsync()
    {
        // Scope/Dispose/CancellationToken 不要。owner(this) の GameObject 破棄で自動 Release。
        _image.sprite = await _assetVault.LoadAssetAsync<Sprite>(this, "Icons/coin");
    }
}
```

### 上位: 明示 Scope（画面単位で一括・共有寿命）

```csharp
_assetScope = assetVaultService.CreateScope();
var icon = await _assetScope.LoadAssetAsync<Sprite>("Icons/coin", cancellationToken);
// InstantiateAsync も同様。画面破棄で _assetScope.Dispose() → 一括解放
```

**詳細（使い分け、2 パターンの GameObject、解放の落とし穴、キャンセル/例外）は [asset-vault-usage.md](asset-vault-usage.md) を参照。**

---

## 環境・版の切り替え（QA）

- 環境は `InitializeAsync(baseUrl, ct)` に渡す baseUrl（env→URL はアプリ config）で切替。版は `version.json` 解決。
- QA で別環境を見る場合は **Debug Override**：プリセットの BaseUrl のみ上書き（版は version.json 任せ）。有効化・選択はコード／`UniLab/AssetVault/Debug/Select Environment...` メニュー経由。development ビルドのみ有効・release はストリップ。詳細は [asset-vault-guide.md](asset-vault-guide.md) の「環境切り替え」。

---

## つまずきポイント

| 症状 | 原因/対処 |
|---|---|
| `Sync` が中断する | Local Folder 未設定（必須）。設定アセットで指定する |
| `Sync` が失敗（重複アドレス） | 別アセットが同一アドレス（フォルダ相対・拡張子なし）に衝突。フォルダ構成を見直す |
| ロードで `AssetVaultException` | キー不一致／カタログ未更新／ネットワーク。key=アドレスを確認、`CheckForUpdatesAsync` 済みか確認 |
| Remote が初回だけ遅い | 未キャッシュ。`DownloadAsync` で事前取得 |
| `Dispose` 後に絵が消える/エラー | 解放後にアセット参照を使っている。参照を残さない |
| 生成物が二重破棄/警告 | `InstantiateAsync` の生成物を手動 `Destroy` している。スコープに任せる |

---

## 最小サンプル（Presenter）

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Title
{
    /// <summary>タイトル画面。AssetVault からロゴをロードして表示し、破棄で解放する。</summary>
    public sealed class TitlePresenter : IDisposable
    {
        private readonly IAssetScope _assetScope;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public TitlePresenter(IAssetVaultService assetVaultService, Image logoImage)
        {
            _assetScope = assetVaultService.CreateScope();
            LoadAsync(logoImage, _cancellationTokenSource.Token).Forget();
        }

        private async UniTask LoadAsync(Image logoImage, CancellationToken cancellationToken)
        {
            try
            {
                logoImage.sprite = await _assetScope.LoadAssetAsync<Sprite>("title_logo", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 画面破棄による正常キャンセル
            }
            catch (AssetVaultException exception)
            {
                Debug.LogError(exception);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _assetScope.Dispose();
        }
    }
}
```
