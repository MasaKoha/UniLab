# CLAUDE.md — UniLab

Unity 共通ライブラリ。karakuri など複数プロジェクトから `Assets/UniLab/` を取り込む。
グローバル規約に加えて、このリポジトリ固有の方針を書く。

## 作業前に読む規約

- `/Users/masakoha/.claude/rules/coding-principles.md`
- `/Users/masakoha/.claude/rules/unity-csharp.md`

## 役割分担

- Claude Code は設計・レビュー・検証の司令塔とし、実装は Codex（`codex exec`）へ委譲する
- Codex は `karakuri/tools/codex_run.sh` 経由で起動し、`cd /Users/masakoha/GitHub/UniLab &&` をコマンド内に明示する
- Codex 側の規約は `AGENTS.md` にあるため、プロンプトへ再掲しない

## ライブラリ設計

- Singleton を使わない。共有状態が依存関係から見えなくなり、利用側で差し替えられないため
- 外部依存は DI で注入する。ライブラリ内で具象実装を探索・生成しない
- 公開 API は利用側から意図が読める最小の契約にする。製品固有の型や機能を持ち込まない
- 実装は MVP を採用し、View・Presenter・Model の責務を分離する
- Unity の参照は `[SerializeField]` または初期化時の注入で明示し、実行時に探索しない

## Testify への移管

AI デバッグ・自動プレイツール群は Testify リポジトリ（`/Users/masakoha/GitHub/pisuke-root/Testify`）へ移管済み。運用は Testify の `CLAUDE.md` / `AGENTS.md` を読む。

## 同期

- karakuri-client への取り込みは `rsync -a --delete Assets/UniLab/ <client>/Assets/UniLab/` を使う
- 上流である UniLab の `.meta` を維持し、下流と GUID を揃える
- 方向別の正本は `/Users/masakoha/GitHub/pisuke-root/karakuri/karakuri-client/docs/dependency-sync.md` とし、詳細手順をここへ重複させない

## Git

- デフォルトブランチ `develop` への直接コミットを禁止し、ブランチから PR を作成して squash マージする
- 複数の Codex バッチが同じ作業ツリーを編集する場合は、全バッチ完了後に 1 つの PR へまとめる
