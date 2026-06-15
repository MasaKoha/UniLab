# タスク仕様: AssetVault Group アセット自動登録（インクリメンタル）

作成日: 2026-06-16
対象: `Assets/UniLab/AssetVault/Editor/`
実装担当: Codex / レビュー: Claude Code
関連: [asset-vault-usage.md](asset-vault-usage.md) / [task-asset-vault-improvements.md](task-asset-vault-improvements.md)

> 既存の `AssetVaultEditorOperations.SyncAssetResource()`（手動ボタン／MenuItem）は全アセット走査で Addressables を再構成する。
> 本タスクは「Sync 押し忘れ」を防ぐため、**Local/Remote ルート配下のアセット変更を検知して差分だけ自動登録/更新/削除**する仕組みを追加する。
> **全 Sync の自動実行ではなく、変更差分のインクリメンタル処理**である点が肝。

---

## ゴール

- Local/Remote ルート配下でアセットを **追加・移動・削除** したとき、Addressables のエントリ（グループ所属・アドレス・ラベル）を自動で追従させる。
- 既存の手動 Sync（厳密チェック・全体再構成）は**そのまま残す**。自動側は軽量・非ブロッキング。
- **オプトイン**（既定オフ）。設定でオン/オフできる。

## 規約（既存 Sync と完全一致させること）

エントリの所属・アドレス・ラベルは、現状 `SyncCategory` / `RegisterFolder` / `RegisterDirectAssets` / `RegisterAsset` が決めているルールと**1バイトも違えない**:

- グループ名: ルート直下アセット → `Local_<ルート名>` / `Remote_<ルート名>`。サブフォルダ配下 → `Local_<直下サブフォルダ名>`（深いネストは所属に影響せずアドレスのプレフィックスになるだけ）。
- アドレス: `AssetVaultAddressing.CreateAddress(assetPath, categoryRoot)`（ルート相対・拡張子なし）。
- ラベル: `AssetVaultAddressing.CreateLabel(categoryFolder)`（カテゴリフォルダ名。Local/Remote プレフィックスなし）。
  - ここで **categoryFolder** = ルート直下アセットなら categoryRoot 自身、サブフォルダ配下なら「ルート直下の第一階層サブフォルダ」。

---

## 実装内容

### 1. 純粋ロジック追加（テスト対象）
`Assets/UniLab/AssetVault/Editor/AssetVaultAddressing.cs`

```csharp
/// <summary>
/// assetPath が root 直下／配下にあるか判定します。root と完全一致、または "root/" で始まる場合に true。
/// "Assets/Local" に対する "Assets/LocalStuff/x" のような前方一致の誤検出を防ぎます。
/// </summary>
public static bool IsUnderRoot(string assetPath, string root)

/// <summary>
/// assetPath が属する「カテゴリフォルダ」を返します。
/// ルート直下のアセットは categoryRoot 自身を、サブフォルダ配下のアセットは
/// 「categoryRoot 直下の第一階層サブフォルダ」を返します（グループ/ラベルの決定に使う）。
/// 例: ("Assets/Local/Icons/Sub/x.png", "Assets/Local") → "Assets/Local/Icons"
///     ("Assets/Local/x.png",          "Assets/Local") → "Assets/Local"
/// </summary>
public static string ResolveCategoryFolder(string assetPath, string categoryRoot)
```

- 入力は呼び出し側で `NormalizeAssetPath` 済みを前提（区切り "/"）。内部でも防御的に区切り統一してよい。
- `IsUnderRoot` は必ず `root + "/"` 前方一致を使う（境界バグ注意）。

### 2. 登録コアの抽出（DRY）
`Assets/UniLab/AssetVault/Editor/AssetVaultGroupRegistrar.cs`（新規・internal static）

現在 `AssetVaultEditorOperations` の private に埋まっている登録系を、**手動 Sync と自動登録の両方から呼べるよう**切り出す。挙動は変えない。

移設/共用するメソッド（名前は現状踏襲、シグネチャは下記）:

```csharp
// 既存 EnsureGroup 相当（グループ生成＋スキーマ構成）
internal static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName, bool isLocal);

// 既存 RegisterAsset 相当。duplicateAddressCollector / registeredGuids は任意（自動側は渡さない）。
// 自動側からは label・group をこのメソッドの外で解決して渡す or 下記 RegisterSingle を使う。
internal static void RegisterAsset(
    AddressableAssetSettings settings, AddressableAssetGroup group, string guid,
    string categoryRoot, string label,
    AssetVaultDuplicateAddressCollector duplicateAddressCollector, HashSet<string> registeredGuids);

// 自動登録用の単発登録。assetPath から isLocal/categoryRoot を受け取り、
// ResolveCategoryFolder→GetGroupName/CreateLabel→EnsureGroup→RegisterAsset まで一括で行う。
// 重複はログ警告のみ（ブロックしない）。
internal static void RegisterSingle(AddressableAssetSettings settings, string assetPath, string categoryRoot, bool isLocal);

// guid のエントリを管理グループから除去する（存在しなければ no-op）。
internal static void RemoveEntry(AddressableAssetSettings settings, string guid);

// 空になった管理グループ（Local_/Remote_）を削除する（既存 PruneStaleEntries の空グループ削除部分を共用）。
internal static void PruneEmptyManagedGroups(AddressableAssetSettings settings);
```

- `EnsureGroup` が依存する `ConfigureBundledAssetGroupSchema` / `ConfigureContentUpdateGroupSchema` / プロファイル変数定数（`LocalBuildPathVariableName` 等）も一緒に Registrar 側へ移すか、`AssetVaultEditorOperations` に `internal` で残して参照させる。**どちらでもよいが二重定義は禁止**。
- `AssetVaultEditorOperations.SyncAssetResource` は移設後の Registrar を呼ぶ形にリファクタし、**出力（登録結果・重複検出・プルーニング）が現状と一致**することを保証する。

### 3. 追加・移動の自動処理
`Assets/UniLab/AssetVault/Editor/AssetVaultAutoRegisterProcessor.cs`（新規）

```csharp
internal sealed class AssetVaultAutoRegisterProcessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
}
```

処理:
1. **早期スキップ**: ①Play 中（`EditorApplication.isPlayingOrWillChangePlaymode`）②設定が無い（`AssetVaultSetupSettings.TryLoad` が false。**自動生成しない**）③トグルがオフ ④Addressables 設定が無い（`AddressableSettingsAccessor.TryGetSettingsSilently`）。いずれかで即 return。
2. ルート（Local 必須／Remote 任意）を取得しキャッシュ的に保持。判定は `IsUnderRoot` で行い、配下外のパスは即無視。
3. バッチ全体を `AssetDatabase.StartAssetEditing()`/`StopAssetEditing()` で囲み、最後に1回だけ `SetDirty + SaveAssets`。
4. **imported**: 各パスが Local/Remote いずれかの配下なら `RegisterSingle`（追加・再インポートとも idempotent）。
5. **moved**（`movedAssets`=新パス, `movedFromAssetPaths`=旧パス, 同インデックス）:
   - 新パスが配下 → `RegisterSingle`（移動で group/address/label が変わるため再登録）。移動後も guid は不変なのでアドレス更新が効く。
   - 新パスが配下外 かつ 旧パスが配下 → 旧パスの guid（移動後も `AssetDatabase.AssetPathToGUID(新パス)` で取得可）で `RemoveEntry`。
6. 処理後に `PruneEmptyManagedGroups`。
7. **再入防止**: 上記の設定変更が再度 import を誘発しないこと（Addressables 設定変更は通常アセット再インポートを起こさないが、保存対象が Local/Remote 配下に入らないよう注意。設定アセットは `Assets/Generated/` 配下なので対象外）。

### 4. 削除の自動処理
`Assets/UniLab/AssetVault/Editor/AssetVaultAutoDeleteProcessor.cs`（新規）

削除は `OnPostprocessAllAssets` の `deletedAssets` だと削除後で guid を引けないため、**削除前**フックで確実に取る:

```csharp
internal sealed class AssetVaultAutoDeleteProcessor : UnityEditor.AssetModificationProcessor
{
    private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
}
```

- 早期スキップ条件は §3 と同じ。
- `assetPath` が Local/Remote 配下なら `AssetPathToGUID`→`RemoveEntry`。
- 戻り値は `AssetDeleteResult.DidNotDelete`（Unity 既定の削除を妨げない）。
- 空グループの掃除は削除完了後に走る §3 の `OnPostprocessAllAssets`→`PruneEmptyManagedGroups` に任せる（このフック時点ではまだ削除前）。
- 注: Addressables 本体も削除エントリを掃除する場合があるが、`RemoveEntry` は no-op 安全なので二重でも問題ない。

### 5. トグル（設定）
`Assets/UniLab/AssetVault/Editor/AssetVaultSetupSettings.cs`

```csharp
[Tooltip("オンにすると Local/Remote 配下のアセット追加・移動・削除を検知して Addressables を自動登録/更新します。既定オフ。")]
[SerializeField] private bool _autoRegisterOnAssetChange;

/// <summary>アセット変更時に自動登録を行うか。既定 false（オプトイン）。</summary>
public bool AutoRegisterOnAssetChange => _autoRegisterOnAssetChange;
```

`Assets/UniLab/AssetVault/Editor/AssetVaultSetupSettingsEditor.cs`
- 既存 Inspector にトグルを表示（フォルダ指定の近く）。オンのとき「手動 Sync 不要になる」旨の `HelpBox` を出すと親切。

### 6. テスト
`Assets/Editor/Tests/AssetVault/AssetVaultAddressingTest.cs` に追加:
- `IsUnderRoot`: 一致・配下・配下外・境界（`Assets/Local` と `Assets/LocalStuff/x` が false）。
- `ResolveCategoryFolder`: ルート直下→root 自身 / サブフォルダ直下→第一階層 / 深いネスト→第一階層。

> プロセッサ本体（AssetDatabase イベント依存）は EditMode 単体テストが難しいため、純粋ロジックのみテスト。動作は下記の手動確認で担保。

---

## 対象外（やらないこと）
- 依存アセットの登録スキップ規約（`_` 始まりフォルダ）・共有依存グループ化（重複バンドル対策）。
- 自動側での重複アドレスのブロッキング検出（ログ警告に留める。厳密検出は手動 Sync の責務）。
- 大量インポート時の進捗バー／キャンセル（バッチ処理＋必要なら「件数が多いので手動 Sync 推奨」ログのみ）。

## C# 規約（厳守）
- ブロック namespace（`namespace Foo { ... }`）。ファイルスコープ禁止。
- `if/for/foreach` は1行でも必ず `{}`。省略名禁止（`cfg`/`mgr` 等）。
- public/internal メンバーに `/// <summary>`。コメントは日本語・Why 中心。
- 不要な null チェックを足さない。ネスト最大3段（早期 return で平坦化）。

## 受け入れ条件 / 手動確認
1. トグル OFF: 従来どおり。アセット追加でエントリは自動生成されない（手動 Sync のみ反映）。
2. トグル ON:
   - Local/Remote のサブフォルダに Sprite を追加 → Addressables Groups に `Local_<sub>` グループでアドレス・ラベル付きエントリが**即**追加される。
   - そのアセットを別サブフォルダへ移動 → グループ・アドレス・ラベルが移動先に追従する。
   - ルート配下から外（例 `Assets/` 直下）へ移動 → エントリが除去される。
   - アセットを削除 → エントリが除去され、空になった管理グループが消える。
3. その結果が、同じ状態で**手動 Sync を実行した場合と一致**すること（最重要：自動とSyncで差が出ない）。
4. Play 中は自動処理が走らない。
5. 既存 EditMode テスト（`AssetVaultAddressingTest` 含む）が green。
