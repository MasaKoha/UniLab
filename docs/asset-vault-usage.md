# AssetVault 利用ガイド：アセットのロード・利用・解放

`IAssetVaultService` / `IAssetScope` を使って Sprite・GameObject などを読み込み、使い、解放するまでの実コード手引き。
API 仕様の俯瞰は [asset-vault-guide.md](asset-vault-guide.md)、配信設計は [design-unilab-asset-cdn.md](design-unilab-asset-cdn.md) を参照。

---

## 重要: ロードは source-agnostic（Local/Remote を区別しない）

**「どこから読むか（Local 同梱 / Remote CDN）は“アドレス”で確定しており、呼び出し側は指定しない・できない。」**

- どこから読むかは、ビルド時に各アセットの**所属グループの LoadPath** としてカタログに焼かれる（Local グループ→StreamingAssets、Remote グループ→`{BaseUrl}/{ContentPath}/...`）。
- だから `LoadAssetAsync<T>(owner, key)` は **Local/Remote 共通の1本**。アドレスを渡すだけで、Addressables がカタログを見て適切な場所から読む。
- `InitializeAsync(baseUrl, ...)` の `baseUrl` は **Remote グループのアセットにのみ**効く（Remote の LoadPath トークンを埋める）。**Local グループのアセットは baseUrl に関係なく常にローカルから読む**。
- ゆえに **`LoadLocalAssetAsync` / `LoadRemoteAssetAsync` のような API 分割はしない**（行き先はアドレスで決まり、メソッド名では変えられないため、分けると誤解を生む）。
- 「この key は通信に行くか？（事前 DL が要るか）」を知りたい場合は **`GetDownloadSizeAsync(keys)`**（>0 なら未取得の Remote）で判断し、必要なら **`DownloadAsync(labels)`** で先に取得する。

---

## 使い方の選択肢

| 方式 | 書き方 | 解放タイミング | 使うとき |
|---|---|---|---|
| **標準: service 拡張** | `service.LoadAssetAsync<T>(owner, key)` | owner の GameObject 破棄で自動 | 単一コンポーネントが自分用の asset を読む大半のケース |
| **共有/プール: キャッシュ** | `cache.AcquireAsync<T>(key)` → `reference.Dispose()` | 参照0＋TTL/LRU で遅延解放 | 複数所有・再利用で差し替え・churn を避けたい |
| **スロット** | `slot.SetAsync(key)` | 差し替え時に旧解放／Dispose で解放 | 「1スロットに常に1枚、差し替わる」要素（プール要素の表示物） |
| **上位: 明示 Scope** | `scope.LoadAssetAsync<T>(key, ct)` | `scope.Dispose()` で一括 | 画面/シーン単位でまとめたい・共有寿命を制御したい |

> プール連携・共有・動的差し替えは下の「キャッシュ / スロット」を参照。単発の View ロードは「標準」で十分。

迷ったら**標準（service 拡張）**。Scope も Dispose も書かずに済む。

### 標準: `service.LoadAssetAsync`（推奨・最小）

owner は **GameObject / Component どちらも可**（`gameObject` でも `this` でも OK）。

```csharp
public sealed class IconView : MonoBehaviour
{
    [Inject] private readonly IAssetVaultService _assetVault; // DI で注入

    [SerializeField] private Image _image;

    private async UniTask ShowAsync()
    {
        // Scope/Dispose/CancellationToken を書かない。owner(this) の GameObject 破棄で自動 Release。
        // ct 省略時は destroyCancellationToken が使われ、ロード中キャンセルも自動。
        _image.sprite = await _assetVault.LoadAssetAsync<Sprite>(this, "Icons/coin");
        // GameObject を渡してもよい: _assetVault.LoadAssetAsync<Sprite>(gameObject, "Icons/coin")
    }
}
```

- owner（GameObject/Component）に隠しスコープ（`AssetScopeHolder`）が付き、`OnDestroy` で解放される。スコープは `service.CreateScope()` 由来。アプリは触らない。
- `_assetVault.InstantiateAsync(this, key, parent)` も同様（生成物も GameObject 破棄で解放）。
- 前提: 別途 `IAssetVaultService.InitializeAsync` 済みであること（起動シーケンス）。

### ラベル一括ロード: `LoadAssetsAsync<T>(label)`

「まとめてロードしたい単位」を **ラベル**で一括取得する。ラベルは **Sync 時にサブフォルダ名から自動付与**される（規約：1サブフォルダ ＝ 1ラベル）。手動でラベルを振る必要はない。

```csharp
// Local/Icons/ 配下を一括ロード。owner(this) の GameObject 破棄で自動 Release。
IReadOnlyList<Sprite> icons = await _assetVault.LoadAssetsAsync<Sprite>(this, "Icons");
```

- **型 `T` がフィルタ**として働く。`Icons/` に Sprite と Prefab が混在していても `LoadAssetsAsync<Sprite>("Icons")` は Sprite だけ返す（混在はエラーにならない）。全部欲しければ `LoadAssetsAsync<UnityEngine.Object>("Icons")`。
- ラベルは **Local/Remote を区別しない**（source-agnostic）。同名フォルダが両方にあれば両方から読む。Remote の事前 DL が要るかは `GetDownloadSizeAsync` / `DownloadAsync` で判断する（label をそのまま渡せる）。
- 明示 Scope 版は `scope.LoadAssetsAsync<T>(label, ct)`。
- **規約の指針**: ラベルは「型」ではなく「**ロード単位（用途）**」で切る（例 `HomeIcon` / `BattleIcon`）。型分割（`Icons/Sprite`, `Icons/Prefab`）は `T` が捌くので不要。

### 依存アセットと skip フォルダ（`_` 始まり）

`Local/Remote` 配下で **`_` 始まりのフォルダは登録対象外**（Sync・自動登録とも）。「コードから直接ロードしない依存アセット」の置き場に使う。

判断基準は「**address/label でロードする起点か？**」:

- **起点 → 通常フォルダ（登録される）**: prefab / ScriptableObject / 単体 Sprite / `.spriteatlas` 本体。
- **依存のみ → `_` フォルダ（登録されない）**: その起点専用の AnimationClip / AnimatorController / Material / Shader / **SpriteAtlas の元 Sprite** 等。登録されなくても、起点をロードすれば**依存として自動同梱**される（1コピー）。

```
Local/
  Characters/
    hero.prefab          ← 登録（address: Characters/hero, label: Characters）
    _src/                ← 登録されない（hero 専用の依存置き場）
      hero.anim
      hero_mat.mat
  UI/
    icons.spriteatlas    ← 登録（アトラス本体はロード可）
    _atlas/              ← 登録されない（アトラスの元 Sprite。重複・実行時バインド事故を防ぐ）
      coin.png
```

**「登録されない」≠「ビルドに入らない」**: `_` が制御するのは Addressable エントリ化（= 個別ロードの口）だけ。`_` 配下でも、登録済みアセットから参照されていれば依存として同梱される。逆に**どこからも参照されない `_` 配下アセットは、登録も参照もされず＝ビルドに含まれない（ロード手段がない）**点に注意。

**重要な例外 — 共有依存は `_` に入れない**:

- `_`（未登録）にすると、その依存は**参照する各バンドルに重複コピー**される。これは**単一利用の依存にだけ**正しい。
- **複数の起点／複数グループから参照される共有依存**（共通 Material・共有アトラス等）は、`_` ではなく**通常フォルダに置いて登録**する（1バンドルに集約され重複しない）。
- 重複の検出は手で追わず **Addressables Analyze → Check Duplicate Bundle Dependencies**。出たものを `_` から通常フォルダ（共有グループ）へ移す。

> SpriteAtlas: 本体は登録、元 Sprite は `_` で除外、が基本。直接参照されないアトラス（名前解決で使う等）は `SpriteAtlasManager.atlasRequested` を Addressables で配線して動的供給する。

> 以降は **上位: 明示 Scope** 方式の説明。共有寿命や画面単位の一括解放が要るときに使う。

## 明示 Scope の原則（最初に押さえる3点）

1. **ロードは必ず `IAssetScope` 経由**で行う。`Addressables` を直接触らない（拡張も裏で Scope を使う）。
2. **スコープが asset の lifetime を所有する**。画面/シーン単位で 1 スコープを作り、`Dispose()` で**その画面で読んだ全アセットを一括解放**する。個別解放は不要。
3. **解放後（`Dispose()` 後）にアセットへアクセスしない**。Sprite/Prefab は解放でアンロードされ得るため、参照を残して使うとリークや不正参照になる。

> `key` は Addressables の **アドレス**（= ルートフォルダ相対・拡張子なし。例 `Characters/hero`）。

---

## 公開 API（ロード関連の抜粋）

```csharp
public interface IAssetVaultService
{
    IAssetScope CreateScope();                       // 画面/シーン単位のスコープを作る
    // …Initialize / Download などは asset-vault-guide.md 参照
}

public interface IAssetScope : IDisposable
{
    // key で asset をロード（Sprite / ScriptableObject / GameObject(プレハブ資産) 等）
    UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken);

    // label を付与した asset 群を一括ロード（T が型フィルタとして働く）
    UniTask<IReadOnlyList<T>> LoadAssetsAsync<T>(string label, CancellationToken cancellationToken);

    // key の GameObject を parent 配下に生成（インスタンス化まで行う）
    UniTask<GameObject> InstantiateAsync(string key, Transform parent, CancellationToken cancellationToken);
}
```

---

## 1. スコープの生成と破棄（土台）

スコープは「画面（Presenter）やシーンの寿命」と一致させる。Presenter なら `Dispose` パターンに乗せる。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Game.Title
{
    /// <summary>
    /// タイトル画面の Presenter。画面寿命に合わせて AssetVault のスコープを所有・解放する。
    /// </summary>
    public sealed class TitlePresenter : System.IDisposable
    {
        private readonly IAssetScope _assetScope;
        private readonly CompositeDisposable _compositeDisposable = new CompositeDisposable();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public TitlePresenter(IAssetVaultService assetVaultService)
        {
            // 画面で 1 つだけスコープを作る。以降のロードはすべてこのスコープ経由。
            _assetScope = assetVaultService.CreateScope();
        }

        public void Dispose()
        {
            // 進行中のロードをキャンセル → スコープ破棄で全 handle を解放。
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _assetScope.Dispose();        // ← ここで読み込んだ Sprite/GameObject がまとめて解放される
            _compositeDisposable.Dispose();
        }
    }
}
```

- **1 画面 = 1 スコープ**。複数画面で使い回さない（解放境界が曖昧になる）。
- `Dispose()` を必ず呼ぶ（MVP の Presenter 破棄、VContainer の LifetimeScope 破棄、`MonoBehaviour.OnDestroy` 等に紐付ける）。

---

## 2. Sprite を読み込んで Image に表示する

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイコン Sprite をロードして Image に適用する。
/// </summary>
private async UniTask LoadIconAsync(Image targetImage, CancellationToken cancellationToken)
{
    // key はアドレス（例: External/Icons/coin.png → "Icons/coin"）
    var sprite = await _assetScope.LoadAssetAsync<Sprite>("Icons/coin", cancellationToken);
    targetImage.sprite = sprite;
}
```

呼び出し側：

```csharp
LoadIconAsync(_view.IconImage, _cancellationTokenSource.Token).Forget();
```

注意：
- `targetImage.sprite` に入れた Sprite は、**スコープ破棄まで有効**。`Dispose()` 後は `targetImage.sprite` を参照したままにしない（破棄前に `null` 代入など）。
- 同じ key を 2 回 `LoadAssetAsync` してもアドレッサブルが参照カウントで管理する。スコープが両方の handle を持ち、Dispose でまとめて解放する。

---

## 3. GameObject の読み込み：2 パターンを使い分ける

### A. その場に生成したい → `InstantiateAsync`（推奨）

生成（Instantiate）まで行い、**生成したインスタンスの解放もスコープが所有**する。

```csharp
/// <summary>
/// 敵プレハブを生成して配置する。生成物の破棄はスコープ Dispose に任せる。
/// </summary>
private async UniTask<GameObject> SpawnEnemyAsync(Transform parent, CancellationToken cancellationToken)
{
    var enemy = await _assetScope.InstantiateAsync("Enemies/slime", parent, cancellationToken);
    return enemy;
}
```

- **生成した GameObject を自分で `Object.Destroy` しない**。`scope.Dispose()` 時に Addressables がインスタンスを破棄＋解放する。
- 画面の途中で個別に消したい一過性の生成物は、このスコープ方式に乗せず別管理を検討（このスコープは「画面寿命で一括解放」用途）。

### B. プレハブ資産だけ欲しい（自前で複数 Instantiate する等）→ `LoadAssetAsync<GameObject>`

```csharp
// プレハブ「資産」をロード（インスタンス化はしない）
var prefab = await _assetScope.LoadAssetAsync<GameObject>("Bullets/normal", cancellationToken);

// 自分で Instantiate する場合、生成インスタンスの寿命は「自分の責任」
var bullet = Object.Instantiate(prefab, parent);
// … 使い終わったら自分で Object.Destroy(bullet);
// プレハブ資産そのもの（prefab）は scope.Dispose() で解放される
```

- こちらは **生成インスタンスはスコープ管理外**。`Object.Instantiate` した分は自分で `Destroy` する。スコープが解放するのは「プレハブ資産の handle」だけ。
- 迷ったら **A（InstantiateAsync）** を使う方が解放漏れしにくい。

---

## 4. 解放（Release）の仕方

**個別解放 API は無い。スコープの `Dispose()` が唯一の解放手段。**

```csharp
_assetScope.Dispose();
// → このスコープで LoadAssetAsync / InstantiateAsync した全 handle を Addressables.Release で解放
//   - LoadAssetAsync 分: 参照カウントを減らし、0 でアンロード
//   - InstantiateAsync 分: 生成インスタンスを破棄して解放
```

やってはいけないこと：
- ❌ `Dispose()` 後に Sprite / プレハブ / 生成 GameObject を使う
- ❌ `InstantiateAsync` で作った GameObject を手動 `Object.Destroy`（二重解放・警告の原因）
- ❌ スコープを跨いでアセット参照を共有（解放境界が壊れる）

---

## 5. キャンセルと例外

- 各 API は `CancellationToken` を取る。画面破棄時は `CancellationTokenSource.Cancel()` でロード中処理を中断する（上の Presenter 例）。
- ロード失敗時は `AssetVaultException` が飛ぶ（キャンセルは `OperationCanceledException`）。表示の出し分けは呼び出し側で行う。

```csharp
try
{
    var sprite = await _assetScope.LoadAssetAsync<Sprite>("Icons/coin", cancellationToken);
    _view.IconImage.sprite = sprite;
}
catch (System.OperationCanceledException)
{
    // 画面破棄などによる正常キャンセル。握りつぶしてよい。
}
catch (AssetVaultException exception)
{
    // ロード失敗（キー不一致・ネットワーク・カタログ不整合等）。フォールバック表示やリトライ導線へ。
    Debug.LogError(exception);
}
```

---

## 6. Remote アセットは事前ダウンロードが要る場合がある

Remote（CDN 配信）アセットは、未キャッシュだと初回 `LoadAssetAsync` で都度 DL になる。まとまった量は **`DownloadAsync` で事前取得**してから使うと UX が安定する（label 単位）。詳細は [asset-vault-guide.md](asset-vault-guide.md) の「差分配信の運用」。

```csharp
var labels = new[] { "stage1" };
var size = await assetVaultService.GetDownloadSizeAsync(labels, cancellationToken);
if (size > 0)
{
    // 必要なら size を見て確認ダイアログ → DownloadAsync（進捗は OnDownloadProgress を購読）
    await assetVaultService.DownloadAsync(labels, cancellationToken);
}
// 以降のロードはキャッシュから即時
```

### 推奨: 再利用部品で「起動 〜 更新 〜 事前DL」を束ねる

毎回 `GetDownloadSize → 確認 → Download` を手書きせず、用途別の再利用部品を使う。いずれも `IAssetVaultService` をコンストラクタ注入し、進捗は `OnProgress`（R3 `Observable<DownloadProgress>`）で流す。リトライは指数バックオフ（0.5→最大8秒、`AssetVaultRetryPolicy` に集約）で、`OperationCanceledException` は素通しする。

| 部品 | 役割 | 入口メソッド |
|---|---|---|
| `AssetVaultDownloadController` | サイズ確認 → ユーザー確認 → リトライ付き DL → 進捗通知 | `EnsureDownloadedAsync` |
| `AssetVaultUpdateController` | 上記に「カタログ更新確認＋適用」を前置（配信後の差し替え対応） | `RunUpdateAsync` |
| `AssetVaultBootstrapper` | さらに「初期化（リトライ付き）」を前置。アプリ起動時の入口 | `StartAsync` |

#### アプリ起動: `AssetVaultBootstrapper`（推奨の入口）

初期化 → カタログ更新確認 → 初期必須アセットの事前DL を1呼び出しで行う。

```csharp
var bootstrapper = new AssetVaultBootstrapper(_assetVaultService); // 実機は DI 注入
bootstrapper.OnProgress
    .Subscribe(progress => _view.SetProgress(progress.Ratio))
    .AddTo(_compositeDisposable);

var result = await bootstrapper.StartAsync(
    baseUrl: _config.BaseUrl,                         // Local 専用なら空文字
    initialDownloadLabels: new[] { "boot" },          // 起動時に必須なラベル。無ければ空でよい
    confirmAsync: size => ConfirmDownloadDialogAsync(size), // 「○○MB DL しますか？」。null なら確認なし
    maxRetryCount: 2,
    cancellationToken: cancellationToken);

if (!result.IsReady)
{
    // 初期化失敗 or 初期DL失敗。result.Initialized / result.UpdateResult.DownloadResult を見てリトライ導線・エラー画面へ。
    return;
}
// 起動準備完了 → ゲーム本編へ
```

- 戻り値 `AssetVaultBootstrapResult`: `Initialized`（初期化成否）/ `UpdateResult`（`CatalogUpdated` + `DownloadResult`）/ `IsReady`（本編へ進んでよいか）。
- 全体ローディング UI は `bootstrapper.State`（= `service.State`: Initializing / Downloading / Ready / Failed）を購読して出す。

#### 更新だけ / DL だけ

```csharp
// 初期化済みで、カタログ更新確認＋差分DLだけしたい
var update = await new AssetVaultUpdateController(_assetVaultService)
    .RunUpdateAsync(new[] { "stage2" }, ConfirmDownloadDialogAsync, maxRetryCount: 2, cancellationToken);

// 確認＋リトライ付きで DL だけ
var download = await new AssetVaultDownloadController(_assetVaultService)
    .EnsureDownloadedAsync(new[] { "stage2" }, ConfirmDownloadDialogAsync, maxRetryCount: 2, cancellationToken);
// download: NothingToDownload / Completed / CanceledByUser / Failed
```

---

## 7. 共有・オブジェクトプール（キャッシュ / スロット）

プールや「再利用ごとに表示が変わる要素」では、スコープの手動 Dispose 管理が辛くなる。`IAssetVaultCache`（参照カウント＋TTL/LRU）と `AssetSlot<T>`（1スロット差し替え）を使う。

### DI 登録

```csharp
// RootLifetimeScope
builder.Register<IAssetVaultCache>(_ => new AssetVaultCache(), Lifetime.Singleton);
// 設定を変えるなら: new AssetVaultCache(new AssetVaultCacheSettings(ttlSeconds: 15f, capacity: 128))
```

### キャッシュ: Acquire / Release（共有・refcount）

```csharp
var reference = await _cache.AcquireAsync<Sprite>("Icons/coin", ct); // 参照+1（無ければロード）
_image.sprite = reference.Value;
...
reference.Dispose(); // 参照-1。0 でも TTL 猶予中はキャッシュ保持→再取得は即時、未使用は後で解放
```

- 同じ key は共有（複数所有しても実体1つ）。
- 参照0でも **TTL（既定10秒）** の間は保持＝出入りの激しいプールで再ロード（churn）しない。
- **LRU（既定64件）** で溜め込みすぎを防止。設定は `AssetVaultCacheSettings`。

### Prewarm: 事前ロードで本番を即時化

ロード待ちを「暇な瞬間（ローディング画面・遷移）」へ前倒しする。`PrewarmAsync<T>(keys)` で cache に載せて **pin**（保持）し、以降の `AcquireAsync` を即時（cache ヒット）にする。

```csharp
// ローディング画面で先読み（pin される＝TTL で消えない）
await _cache.PrewarmAsync<Sprite>(new[] { "Icons/coin", "Icons/gem" }, ct);
...
// 本番：cache ヒットで即時、hitch なし
var reference = await _cache.AcquireAsync<Sprite>("Icons/coin", ct);
...
// そのシーン/バトルを抜けるとき、pin を解放（以降は TTL/LRU 管理に戻る）
_cache.ReleasePrewarm();
```

- **Remote アセットは先に `DownloadAsync(labels)`**（バイトをディスクへ）→ `PrewarmAsync`（メモリへ展開）の順。Prewarm は通信の一段上。
- **メモリ予算のため範囲を区切る**: そのシーン/バトルの集合だけ prewarm → 終わったら `ReleasePrewarm`。全部 prewarm は禁物。
- pin 中は TTL/LRU で破棄されない。`ReleasePrewarm` を呼ばないと載りっぱなしになる。

### ランタイム診断: `GetStats()`

cache の占有状況スナップショットを取得する（デバッグ表示・リーク調査用）。

```csharp
var stats = _cache.GetStats();
// EntryCount / ReferencedEntryCount / PinnedEntryCount / TotalReferenceCount / UnreferencedEntryCount
var memoryBytes = _cache.EstimateMemoryBytes(); // ロード済みアセットの概算メモリ（Profiler、診断の目安）
```

> Play 中はこれらを **`UniLab/AssetVault/Cache Stats` ウィンドウ**で可視化できる（件数＋概算メモリ＋`Trim` ボタン）。`AssetVaultCache` を生成すると自動でレジストリ登録され、ウィンドウに出る（`UNITY_EDITOR || DEVELOPMENT_BUILD` 限定、リリース非搭載）。規約検査は Dashboard の **Conventions** で別途行う。

### スロット: 1枚を差し替える要素（プール要素向け）

```csharp
public sealed class PooledEnemy : MonoBehaviour
{
    [Inject] private readonly IAssetVaultCache _cache;
    [SerializeField] private Image _image;
    private AssetSlot<Sprite> _icon;

    private void Awake() => _icon = new AssetSlot<Sprite>(_cache);

    // 再利用のたびに呼ぶだけ。旧解放→新取得を内部で行い、溜まらない。
    public async UniTask SetupAsync(string spriteKey, CancellationToken ct)
    {
        _image.sprite = await _icon.SetAsync(spriteKey, ct);
    }

    private void OnDestroy() => _icon.Dispose(); // プール破棄時に解放
}
```

### プール本体（プレハブ資産はプール寿命で保持）

```csharp
// プレハブ“資産”は LoadAssetAsync<GameObject>(owner=プール) で1回ロード（InstantiateAsync は使わない）
_prefab = await _assetVault.LoadAssetAsync<GameObject>(this, key, ct);
// インスタンスは自前 Instantiate でプール管理。Get/Return はアクティブ切替のみ（再ロードしない）
// プール GameObject 破棄で _prefab は自動 Release
```

### 落とし穴

- **動的差し替えの要素に `service.LoadAssetAsync(this, key)`（auto-holder）を使わない**。holder のスコープは GameObject 寿命まで解放されず、差し替えるたびに溜まる（実質リーク）。動的は **スロット or キャッシュ**を使う。
- プールのインスタンス生成は `InstantiateAsync` ではなく `LoadAssetAsync<GameObject>` ＋ 自前 `Instantiate`。
- アセットの所有者を1つに決める（プールでプリロード or 要素でスロット、の二重所有を避ける）。

---

## 8. Editor: Asset Vault Dashboard（操作一覧）

`UniLab/AssetVault/Dashboard` を開くと、Addressables 構成の確認・ビルド・規約チェックをまとめて行える。

> 注: Dashboard の **UI 表示テキストは英語**にしている。Unity 6.5 の Editor 動的フォントアトラスが大量の日本語グリフで溢れ、ウィンドウ下部の文字が欠ける不具合があるため、常時描画テキスト・ツールチップ・規約メッセージは ASCII に統一した。各操作の日本語説明はこの節で管理する。

| セクション / ボタン | 日本語の説明 |
|---|---|
| **Setup → Open Setup Settings** | 設定アセット `AssetVaultSetupSettings` を開く。Local/Remote フォルダの指定と Sync AssetResource はその Inspector で行う。 |
| **Build → New Build** | Addressables を新規フルビルド（content state を作り直す）。初回、またはグループ構成・規約を変更したときに実行する。**重複アドレス等の致命的な規約違反があるとビルドは中断**される（後述のビルド前ゲート）。 |
| **Build → Content Update (Diff)** | 前回の content state からの差分だけをビルドする。配信済みアプリ向けにアセットを追加・更新するときに使う（先に New Build が必要）。 |
| **Debug Override** | 環境プリセットの BaseUrl で `AssetVaultRuntime.BaseUrl` を上書きする（development ビルドのみ有効、release では無効）。有効化・プリセット選択は UI からは行えず、`AssetVaultDebugEnvironmentSettings.Activate` / `Deactivate` をコードから呼ぶ。`Edit Presets` でプリセット（表示名・BaseUrl）を編集する設定アセットを開く。 |
| **Status → Refresh** | 現在の Addressables 構成（RemoteLoadPath・Local/Remote グループ数・AssetResource フォルダ有無）を再取得して表示する。 |
| **Conventions → Check Conventions** | 管理グループの規約違反（重複アドレス・孤立ラベル・依存アセットのエントリ化）を検査する。Addressables 標準の Analyze を補う、AssetVault 規約の健全性チェック。 |

### 規約チェックの3種別とビルド前ゲート

`Check Conventions` で検出する違反は3種類。重大度は `AssetVaultViolation.IsError` に一元化され、Dashboard 表示（Error=赤 / Warning=黄）とビルド前ゲートが同じ判定を共有する。

| 違反種別 | 重大度 | 内容と対処 |
|---|---|---|
| **DuplicateAddress** | Error | 同一アドレスが複数エントリに付いている。実行時ロードを壊すため**ビルドを中断**する。アドレスの衝突を解消する。 |
| **OrphanLabel** | Warning | どのエントリも使っていない孤立ラベル（自動登録の入れ替えで残った等）。ビルドは止めないが警告ログを出す。 |
| **DependencyRegisteredAsEntry** | Warning | 他エントリの依存でもあるアセットが自身もエントリ登録されている。`_` skip フォルダか共有グループ化を検討する（重複バンドル防止）。 |

`New Build` / `Content Update` は実行前に必ずこのチェックを通す。**Error が1件でもあれば中断**し Console に違反一覧を出力、**Warning は記録のみでビルドは続行**する。

### Cache Stats ウィンドウ（Play 中のキャッシュ診断）

`UniLab/AssetVault/Cache Stats` を開くと、Play 中の `AssetVaultCache` の占有を可視化できる（Dashboard とは別ウィンドウ）。

- 表示: Entry / Referenced / Pinned / Unreferenced / Total Reference Count ＋ Estimated Memory（Profiler 概算）。
- **Trim** ボタンで TTL 期限切れ・LRU 超過の未参照エントリを即時解放（解放挙動の手元確認用）。
- `AssetVaultCache` のコンストラクタが自動でレジストリ登録するため、アプリ側のコード追加は不要。`UNITY_EDITOR || DEVELOPMENT_BUILD` 限定でリリースビルドには含まれない。

### CI / batchmode からのビルド

`AssetVaultCiBuild.BuildNewForCi` / `BuildContentUpdateForCi`（`-executeMethod` で呼ぶ）が、規約ゲート付きビルドを実行し、Error 違反・ビルド失敗時に `EditorApplication.Exit(1)` で終了する。CI で規約違反を自動検出したいときに使う（例: `unity -batchmode -quit -executeMethod UniLab.AssetVault.Editor.AssetVaultCiBuild.BuildNewForCi`）。

---

## まとめ（チェックリスト）

- [ ] 起動は `AssetVaultBootstrapper.StartAsync`（初期化→更新確認→初期DL）を通し、`result.IsReady` を確認した
- [ ] 画面/シーンごとに `CreateScope()` で 1 スコープを作った
- [ ] ロードは `LoadAssetAsync<T>` / `InstantiateAsync` のみ（`Addressables` 直叩きしない）
- [ ] 生成物は `InstantiateAsync`（推奨）。`LoadAssetAsync<GameObject>` から自前 Instantiate した分は自分で Destroy
- [ ] 画面破棄で `CancellationTokenSource.Cancel()` → `scope.Dispose()` を呼んだ
- [ ] `Dispose()` 後にアセット参照を使っていない
