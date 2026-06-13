# タスク: AssetVault Editor 拡張のリオーガナイズ

ステータス: **計画（未実装・次 PR 予定）**
作成日: 2026-06-13
関連: [design-unilab-asset-cdn.md](design-unilab-asset-cdn.md) / [design-unilab-asset-vault.md](design-unilab-asset-vault.md)

`UniLab.AssetVault.Editor` の拡張を「わかりやすく＋整理」する。散在したメニューと、操作とUIを兼務した構成を、**ダッシュボード＋操作レイヤ分離**に再編する。

---

## 背景・課題

| 現状 | 課題 |
|---|---|
| メニューが Build / Profile / Setup / Sample に散在 | 全体像が見えず発見しづらい |
| 各メニュークラスが「Addressables 操作」と「UI(MenuItem)」を兼務 | ロジックが再利用・テストしにくい |
| `AssetVaultProfileSwitcher`（dev/stg/prod の Addressables Profile 切替） | env は実行時 BaseUrl で切替＝**Profile は1つでよくなり役割が陳腐化** |
| 現状（RemoteLoadPath 値・グループ数・AssetResource 有無）がどこにも表示されない | 状態把握できない |

---

## 方針（確定済み）

1. **統合 EditorWindow ダッシュボードを作る**
2. **MenuItem も残す**（主要操作のショートカットとして。Window からは全操作可能に）
3. **`AssetVaultProfileSwitcher` を廃止し、「実行時デバッグ上書き panel」に置換**

---

## 提案構成

### 1. EditorWindow ダッシュボード
`Window > UniLab > Asset Vault`。セクション分けでボタン＋状態表示:

| セクション | 内容 |
|---|---|
| Setup | ルートパス表示/編集、`Sync AssetResource` 実行、設定アセットを開く |
| Build | `New Build` / `Content Update (Diff)` |
| Sample | プレースホルダ生成 |
| Debug Override | `BaseUrl` / `ContentPath` を入力し Play 前にセット（版違い/別環境ロード。CDN 設計のデバッグ機能の実体化） |
| Status | RemoteLoadPath の現値、Local/Remote グループ数、AssetResource ルートの有無 |

### 2. 操作レイヤの分離（整理の本体）
Addressables 操作を**静的な操作クラス**（例 `AssetVaultEditorOperations`）に抽出し、**EditorWindow と MenuItem の両方が同じ操作を呼ぶ**。UI と操作を分離＝DRY・見通し改善。
- 既存 `AssetVaultBuildMenu` / `AssetVaultSetupMenu` の MenuItem は**薄いラッパ**にして操作クラスへ委譲
- `AddressableSettingsAccessor` は操作クラスから流用

### 3. ProfileSwitcher の置換
`AssetVaultProfileSwitcher`（Addressables Profile 切替）を削除し、代わりに **実行時デバッグ上書き**を提供:
- `AssetVaultRuntime.BaseUrl` / `ContentPath` を `InitializeAsync` 前にセットする UI（Window の Debug Override セクション）
- これにより「prod アプリで dev1 のアセットを見る」「特定の版フォルダを読む」を QA で実現（設計済み機能）

---

## スコープ

**対象**: `UniLab.AssetVault.Editor` 配下
- 新規: `AssetVaultWindow`（EditorWindow）、`AssetVaultEditorOperations`（操作レイヤ）
- 改修: `AssetVaultBuildMenu` / `AssetVaultSetupMenu`（薄いラッパ化）
- 削除/置換: `AssetVaultProfileSwitcher` → デバッグ上書きへ

**非スコープ**
- ランタイム（`IAssetVaultService` / 状態機械 / 差分DL / `AssetVaultRuntime` 等）は不変
- CDN 配信仕様・フォルダ規約の変更はしない（[design-unilab-asset-cdn.md](design-unilab-asset-cdn.md) のまま）

## 実装メモ
- 操作レイヤは static で副作用を持たせすぎない。状態取得（RemoteLoadPath 値・グループ数）は読み取り専用ヘルパーに
- Window は `EditorWindow` + `IMGUI`（軽量）で十分。状態は `OnGUI`/必要時更新
- デバッグ上書きは EditorPrefs 等に保持し、Play 時に `AssetVaultRuntime` へ反映する経路を別途検討（ランタイムにデバッグ専用 API を足さない範囲で）
- 実装は Codex → Claude レビューのループ
