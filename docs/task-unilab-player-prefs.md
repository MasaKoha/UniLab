# 実装タスク: UniLabPlayerPrefs

設計書: `docs/design-unilab-player-prefs.md` を必ず読んでから実装すること。

---

## 作成するファイル

### 1. `Assets/UniLab/Persistence/UniLabPlayerPrefsKeySourceAttribute.cs`

- namespace: `UniLab.Persistence`
- `[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]`
- クラス名: `UniLabPlayerPrefsKeySourceAttribute`
- `/// <summary>` を付ける

### 2. `Assets/UniLab/Persistence/Editor/UniLabPlayerPrefsEditorWindow.cs`

- namespace: `UniLab.Persistence.Editor`
- クラス名: `UniLabPlayerPrefsEditorWindow`（`EditorWindow` 継承）
- `[MenuItem("UniLab/PlayerPrefs/管理")]` でウィンドウを開く
- `/// <summary>` を public メンバーすべてに付ける

**責務:**

- 全削除セクション: `PlayerPrefs.DeleteAll()` を確認ダイアログ付きで実行
- キー指定削除セクション: テキストフィールドで任意キーを削除
- 既知のキーセクション: スキャンしたキーを一覧表示・個別削除
- キースキャン: `UniLabPlayerPrefsKeySource` 付き型を全アセンブリから探索し、`public static const string` フィールドを収集
- キャッシュ: スキャン結果を `List<string>` でキャッシュ。`AssemblyReloadEvents.afterAssemblyReload` で invalidate
- アセンブリフィルタ: 以下プレフィックスのアセンブリは除外
  - `UnityEngine`, `UnityEditor`, `Unity.`, `System`, `mscorlib`, `Mono.`, `netstandard`

**スキャンロジックは private static なヘルパーメソッドとしてウィンドウクラス内に実装する。**
別クラスには切り出さない。

---

## 変更するファイル

なし。既存ファイルには一切触れない。

---

## 制約

- ブロック namespace を使うこと（`namespace Foo { ... }`）。ファイルスコープ namespace 禁止
- `if` / `foreach` 等は必ず `{}` で囲む
- 変数名・メソッド名の省略形禁止（`btn`, `mgr`, `cfg` 等）
- `[SerializeField]` は使わない（EditorWindow なので不要）
- using は必要なものだけ。未使用 using は書かない
- `.meta` ファイルは作成しない（Unity が自動生成する）
