# レビュー: AssetVault Editor リオーガナイズ

レビュー実施: 2026-06-14（Claude Code / senior-game-engineer 併用）
対象: `Assets/UniLab/AssetVault/Editor/` の新規・改修ファイル
修正担当: Codex → Claude Code（途中終了分を引き継ぎ完了）

ステータス: **R1〜R4 すべて修正済み（2026-06-14、Claude Code）**

> トリアージ済み。senior-game-engineer の指摘 9 件のうち、pre-existing・スコープ過剰・非問題を除外し、**今回直すべき 4 件**に絞った。各項目に「修正方針」を明記しているので、その通りに修正すること。

---

## 対処必須

### R1. Debug Override の初期化順序コメントが誤り＋ドメインリロード無効時の値リーク
`AssetVaultDebugOverride.cs:43-59`

**問題**
- 43-44 行のコメント「アプリの InitializeAsync より前に値が入る」は保証されない。`EnteredPlayMode` は最初のシーンの `Awake` 後に発火し得るため、アプリ初期化との前後は保証なし。誤解を招く断定。
- Enter Play Mode Options で **ドメインリロード無効**の場合、static フィールドが Play セッション間で保持される。「一度 Override を有効化して Play → 停止 → Override を無効化して再 Play」で、無効時は早期 return するため `AssetVaultRuntime.BaseUrl/ContentPath` に**前回のデバッグ値が残留**する。

**修正方針**
- `EnteredPlayMode` で `Enabled == false` の場合に early return せず、`AssetVaultRuntime.BaseUrl = null; AssetVaultRuntime.ContentPath = null;` を実行して残留値をクリアする。アプリ側がこの後 config から本番値を設定するため null クリアは安全。
- コメントを実態に合わせて修正する。「`EnteredPlayMode`（ドメインリロード後）で反映する。ドメインリロード無効時の前回値リークを防ぐため、無効時は null クリアする。アプリ側が config から値を設定する場合はそちらが優先される（順序はアプリ責務）」程度の正確な記述にする。over-claim を消すこと。

### R2. `GetStatus()` だけ settings 取得経路が非統一
`AssetVaultEditorOperations.cs:161`

**問題**
他の操作は `AddressableSettingsAccessor.TryGetSettings` 経由なのに、`GetStatus` だけ `AddressableAssetSettingsDefaultObject.Settings` をフルパス直叩き。未初期化時にエラーログを出したくない意図は妥当だが経路がバラバラ。

**修正方針**
`AddressableSettingsAccessor` にログを出さない取得 `TryGetSettingsSilently(out AddressableAssetSettings settings)`（`/// <summary>` 付き）を追加し、`GetStatus` はそれを使う。フルパス参照を解消。

### R3. グループ名プレフィックス判定がカルチャ依存
`AssetVaultEditorOperations.cs:168-169`

**問題**
`group.Name.StartsWith(LocalGroupPrefix)` がカルチャ依存オーバーロード。プレフィックス判定は序数比較すべき。

**修正方針**
`StartsWith(LocalGroupPrefix, System.StringComparison.Ordinal)` に変更（Remote 側も同様）。

---

## 対処推奨

### R4. RemoteLoadPath / RemoteBuildPath だけ強制上書きする非対称に Why コメントがない
`AssetVaultEditorOperations.cs:121-127`

**問題**
LocalBuild/LocalLoad は `EnsureProfileValue`（無ければ作る）のみなのに、Remote 2つだけ `SetValue` で毎回上書き。意図（AssetVaultRuntime トークン／ServerData 規約に毎回追従させる）がコメントされておらず、誤って消されるリスク。
※これは旧 `AssetVaultSetupMenu` から移管した既存挙動。挙動は変えず Why コメントのみ追加する。

**修正方針**
`SetValue` 2行の直前に Why コメントを追加。「RemoteLoadPath/RemoteBuildPath は AssetVaultRuntime のトークン規約・ServerData 規約に毎回追従させるため、既存値があっても上書きする」旨。挙動は変更しない。

---

## 今回は見送り（理由付き）

- **MenuItem ラッパで `bool` を握り潰し → DisplayDialog 化**: 旧実装も Console ログのみ。スコープ過剰。失敗通知は `Debug.LogError` のままでよい。
- **`FindAssets("")` の二重スキャン perf**: 旧 SetupMenu からの移管。Editor 操作で非ホットパス。最適化不要。
- **`RefreshStatus` 内 `Repaint()` の冗長性**: 機能的に無害。現状維持。
- **`AssetVaultStatus` 7 引数コンストラクタ**: ダッシュボード表示用の単純データ。許容範囲。
- **Sample メニューパスの二重管理**: 疎結合方針（core → Sample 逆依存回避）のトレードオフとして許容。必要なら将来コメントで補強。

---

## 修正後の確認

- Unity でコンパイルが通ること（新規ファイルの .meta 生成含む）
- `UniLab > AssetVault > Dashboard` が開き、各セクションが動作すること
- Debug Override 有効→無効の往復で残留値が消えること（ドメインリロード無効環境で確認推奨）
