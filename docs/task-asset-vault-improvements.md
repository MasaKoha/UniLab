# タスク: AssetVault 改善バックログ

作成日: 2026-06-14
対象: `Assets/UniLab/AssetVault/`（Editor / Debug 中心）
出所: Codex レビュー（codex-cli 0.139.0）＋ Claude Code 分析
関連: [asset-vault-guide.md](asset-vault-guide.md) / [design-unilab-asset-cdn.md](design-unilab-asset-cdn.md) / [review-asset-vault-editor-reorg.md](review-asset-vault-editor-reorg.md)

> リオーガナイズ PR #8 を develop へマージ済み（マージコミット `679d62a`）。本書はその後の残改善タスク一覧。

## 完了済み（PR #8 まで）
- ダッシュボード化（`UniLab > AssetVault > Dashboard`）＋操作レイヤ分離（`AssetVaultEditorOperations`）
- 同期を Local/Remote 2スロット方式へ（`AssetVaultSetupSettings`、Local 必須・Remote 任意、Sync は設定 Inspector に集約）
- item1 古い `.asset` 再生成 / item2 Debug 選択メニュー / item4 stale エントリ・空グループ掃除 / item5 重複アドレス失敗化 / item9（純粋ロジック抽出＋EditMode テスト）
- Debug Override の dev ビルド対応（`UniLab.AssetVault.Debug`、release ストリップ、BaseUrl のみ上書き、選択はコード/メニュー経由）
- Sample 削除、旧 `AssetVaultProfileSwitcher` 廃止、ドキュメント更新

---

## 🔴 高（堅牢性・データ事故）
1. Debug Override 適用責務の分離 — 無効時 `AssetVaultRuntime.BaseUrl = null` がアプリ設定値を消し得る。「上書きする時だけ触る」設計に。
2. Play 中の `Activate`/`Deactivate` 即 `SaveAssets` — QA の一時切替が永続化。Play 中は揮発ストアへ退避。
3. ビルド/プロファイル API の例外・戻り値ハンドリング — `BuildPlayerContent`/`ContentUpdate`/`SetVariableByName`/`CopyAsset`/`CreateFolder`/`DeleteAsset` の失敗を捕捉・通知。
4. `Activate` の入力検証 — 存在しない/空/重複名を設定時に弾く（今は起動時まで失敗が遅延）。

## 🟠 中（正しさ・運用）
5. 既存 `Local_`/`Remote_` グループの無条件再設定 — 手動調整した schema を上書きし得る。
6. 大量アセットで `StartAssetEditing`/進捗/キャンセルが無い — Editor が固まる。
7. `ResolveSelectedPreset` の未選択→先頭フォールバック — Override 有効時に意図しない環境を向くリスク。「未選択は適用しない」へ。
8. 入力検証（Inspector）— プリセット空名/重複名/空URL/URL形式、Local/Remote 同一・入れ子、`DefaultAsset` にファイル混入。
9. address 正規化方針 — 大小文字/空白/日本語/同名別拡張子の扱い。
10. Dashboard を開くだけで `GetOrCreate` が asset 生成 — 読み取り UI の副作用。`TryLoad` と分離。
11. Build ボタンの事前条件未反映 — content state 不在でも押せる。`DisabledScope`＋理由表示。
12. エラー通知の不統一 — `Debug.LogError`+bool と `AssetVaultException` の混在を整理。

## 🟢 低（保守性・軽微）
13. Sync に Undo 記録なし。
14. グループ名がフォルダ末尾名のみ（衝突・意味不明名の余地）。
15. `Presets` が内部 `List` 実体を `IReadOnlyList` で返す（`AsReadOnly`）。
16. `AddressableSettingsAccessor` static 直参照（テスト差し替え不可）。
17. item9 続き: `AddressableAssetSettings` 注入で `SyncAssetResource` の統合テスト。
18. BuildProcessor: 一時 `Resources` フォルダ自体が残る／`callbackOrder` 固定。
19. `Debug.asmdef` の `autoReferenced=true`（明示参照化で境界明確化）。
20. `UNILAB_ADDRESSABLES` define があるがコードが `#if` で守られていない。
21. Window Status が外部変更で自動更新されない。
22. ログ文言の英日混在。

## ⏸ 意図的に見送り（対応不要）
- Remote プロファイル毎回上書きの非対称（Why コメント済み）
- ボタン説明文の常時表示（要望どおり）
- Status のグループ数を prefix で数える（所有マーカーとして許容）

---

## おすすめ着手順
事故りやすさ順で **1 → 2 → 3 → 7 → 10**。次いで 8・11（UX の安全弁）、16・17（テスト基盤）。
