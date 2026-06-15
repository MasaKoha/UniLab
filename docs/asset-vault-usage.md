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

## まとめ（チェックリスト）

- [ ] 画面/シーンごとに `CreateScope()` で 1 スコープを作った
- [ ] ロードは `LoadAssetAsync<T>` / `InstantiateAsync` のみ（`Addressables` 直叩きしない）
- [ ] 生成物は `InstantiateAsync`（推奨）。`LoadAssetAsync<GameObject>` から自前 Instantiate した分は自分で Destroy
- [ ] 画面破棄で `CancellationTokenSource.Cancel()` → `scope.Dispose()` を呼んだ
- [ ] `Dispose()` 後にアセット参照を使っていない
