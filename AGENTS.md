# AGENTS.md

## Code Style Rules

- Keep control flow flat. Avoid deep nesting by preferring guard clauses, early `return`, early `continue`, and small method extraction.
- Always use braces `{}` for control statements (`if`, `else`, `for`, `foreach`, `while`, etc.), even for single-line bodies.
- Prefer `var` for local variable declarations when the type is obvious from the right-hand side.
- Follow existing editor naming conventions in this repository:
  - private fields (including `[SerializeField]`) use `_camelCase`
  - method names / public members / type names use `PascalCase`
  - local variables and parameters use `camelCase` (prefer `var` when applicable)
- Do not use `[FormerlySerializedAs]` or similar attribute-based migration when renaming `[SerializeField]` fields. Rename the serialized field directly in Prefab/scene data so references are not detached.
- Do not add null checks for variables declared with `[SerializeField]`. Assume Inspector always has a valid instance assigned.
- Do not write end-of-line comments. If a comment is needed, write it on the line immediately above the target code.
- Remove unused namespaces/usings and unused variables when editing code.

## UniLab.AI の実装で使うモデル（Codex を呼ぶ側への指針）

UniLab.AI（`Assets/UniLab.AI/`）のツール群は設計書（`docs/design-unilab-ai-*.md`）が先に固まっている。
実装を Codex に委譲するときは、**ツールごとに下表のプロファイル**で起動する（`~/.codex/<name>.config.toml` に定義済み）。
迷ったら `std`。1 回で通らず手戻りが出たら 1 段上げる。

| ツール | 既定プロファイル | 理由 |
|---|---|---|
| 01 UI 状態スナップショット | `std` | 収集規則と JSON 形が設計書で確定 |
| 02 シナリオ expect と合否 | **指定なし（最上位）** | 既存ランナーの改修と他ツールの結線を含む構造変更 |
| 03 例外フォレンジック | `std` | 小規模・設計確定 |
| 04 入力ボキャブラリ | `std` | InputSystem の API 呼び出しが中心 |
| 05 決定的リプレイ | **指定なし** | 決定性の担保に設計判断が残る（`captureFramerate`・アンカー） |
| 06 モンキーテスター | `std` | 01/03/04 の組み合わせ |
| 07 視覚回帰 | `std` | 画像比較のアルゴリズムは設計書で確定 |
| 08 性能計測 | `std`（軽微な追従は `light`） | ProfilerRecorder の薄いラッパ |
| 09 RunArchive | `std` | ファイル集約と index 生成 |
| 10 LLM 駆動の目標プレイ | **指定なし** | ループ設計・詰み検出・シナリオ化に判断が要る |
| 11 入力可視化オーバーレイ | `std` | 描画とイベント購読 |
| 設計書の追記・整形・README | `light` | 機械的 |
| 原因不明のバグ調査 | `deep` | 横断的な依存の洗い出し |

複数ツールを同時に走らせるときは**ファイルの所有範囲を明示して衝突を避ける**（同じ `UiScenarioRunner.cs` を 2 バッチが触らない）。
既存ランナー・録画器に触る結線は 02 のバッチに集約する。
