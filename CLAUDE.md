# CLAUDE.md — UniLab

Unity 共通ライブラリ。karakuri など複数プロジェクトから `Assets/UniLab/` を rsync で取り込む。
グローバル規約（`~/.claude/CLAUDE.md`）に加えて、このリポジトリ固有の方針を書く。

## 役割分担

- **Claude Code は設計・レビュー・検証の司令塔**。実装は Codex（`codex exec`）に委譲する
- Codex は `karakuri/tools/codex_run.sh`（ウォッチドッグ）経由で起動し、**`cd /Users/masakoha/GitHub/UniLab &&` をコマンド内に明示**する
- Codex 側の規約とモデル選定は `AGENTS.md` にある（Codex はこれを自動で読む）。プロンプトに再掲しない

## UniLab.AI は Testify へ移った（2026-09-05）

AI デバッグ・自動プレイツール群（旧 `Assets/UniLab.AI/`）は独立リポジトリ **Testify** に切り出した。
`/Users/masakoha/GitHub/pisuke-root/Testify`（GitHub `MasaKoha/Testify`、public）。設計書もそちらの `docs/design/` にある。
このリポジトリには置かない。UniLab 本体は Testify に依存せず、Testify も UniLab に依存しない。

## 同期

karakuri-client への取り込み: `rsync -a --delete --exclude '*.meta' Assets/UniLab/ <client>/Assets/UniLab/`。
削除したファイルの `.meta` は client 側に残るので、対応する `.cs` が無い `.meta` を消す。

## Git

- デフォルトブランチ `develop`。直接コミット禁止。ブランチ → PR → squash マージ
- 複数の Codex バッチが同じ作業ツリーを編集する場合は、**全バッチ完了後に 1 つの PR** にまとめる（途中コミットは統合作業と衝突する）

## ハマりどころ（実際に踏んだ罠）

### JsonUtility はネストしたオブジェクト型フィールドを「無くても既定インスタンス」で埋める

- `[Serializable] class Step { public MonkeyOptions monkey; }` を `JsonUtility.FromJson` すると、JSON に `"monkey"` が無くても
  `monkey` は **null ではなく new された既定値**になる。`if (step.monkey != null)` で「指定あり」を判定すると全ステップが該当する
  （2026-09-02: 全画面ツアーの全ステップがモンキーテスト扱いになり、警告も例外も出さずに 60 秒ずつ止まった）
- 「キーの有無」が意味を持つフィールドは、`UiScenarioJsonPresence` のように**生 JSON を見てキーの存在を判定**し、無ければ null に戻す。
  配列は空配列になるので `Length > 0` で判定できる
- 無音で止まる構造は作らない。coroutine の各ステップに実時間の上限と例外捕捉を置き、phase をログに出す（`UiScenarioRunner.ExecuteStepWithTimeoutCoroutine`）

### エディタでの撮影は `ScreenCapture.CaptureScreenshot`

- `WaitForEndOfFrame` → `ReadPixels` は Game View が再描画されない状況で永久に戻らず、描画フレーム外の `ReadPixels` は失敗する。
  `ScreenCapture.CaptureScreenshot` は非同期に次フレーム末で書くので、その直後に読まない
