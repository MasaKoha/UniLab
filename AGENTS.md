# AGENTS.md

## 作業前に読むファイル

- リポジトリ固有の方針は `CLAUDE.md` を読む
- Unity / C# の実装・レビューでは `unity-csharp-standards` スキルを使い、次の正本に従う
  - `/Users/masakoha/.claude/rules/coding-principles.md`
  - `/Users/masakoha/.claude/rules/unity-csharp.md`

AI デバッグ・自動プレイツール群は Testify リポジトリ（`/Users/masakoha/GitHub/pisuke-root/Testify`）へ移管済み。運用は Testify の `CLAUDE.md` / `AGENTS.md` を読む。

## Codex 固有事項

- Unity を起動しない
- `dotnet build` を実行しない。コンパイル確認は依頼者が行う
- 起動済み Unity の Testify メールボックス経由の操作・観測は可とする
- using 漏れは型定義と namespace を検索し、出力前に解消する
- git 操作は依頼に明示がある場合だけ行う
- 完了報告には「変更ファイル一覧」と「未実行の確認事項」を必ず書く
