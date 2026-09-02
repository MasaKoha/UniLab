# 09 RunArchive とスマホ閲覧 設計書

ステータス: 設計。ロードマップ M4
前提: `verification-capture-design.md`（karakuri-client #90、RunArchive と serve_gallery.py の元設計）
依存: なし。01〜08 の成果物を集約する

---

## 目的

成果物が `DebugOutput/` 配下の種別ごとに散っており、**1回の検証で何が出たか**をまとめて見られない。
過去のランとの比較もできない。ラン単位に集約し、PC の前に居なくてもスマホで確認できるようにする。

#90 の設計を、その後に増えた成果物（録画 manifest・フォレンジック・合否 JSON・性能・視覚回帰）へ拡張する。

---

## ラン単位の集約

```
VerificationRuns/run-<yyyyMMdd-HHmmss>/
  meta.json
  scenario-result.json        02
  captures/                   スクリーンショットと監査 JSON
  snapshots/                  01（失敗ステップの証拠）
  recordings/<name>/          動画 + manifest（mp4 は Mac 側で生成）
  forensics/                  03
  performance.json            08
  visual-regression/          07 のレポートと差分画像
  monkey/                     06（回した場合）
  player-log.log              FileLogSink
```

### `meta.json`

```json
{
  "scenario": "full-screen-tour",
  "verdict": "pass",
  "startedAt": "...", "finishedAt": "...", "durationSeconds": 31.2,
  "captures": 20, "audits": 5, "auditFindingsTotal": 0,
  "exceptions": 0, "warnings": 0,
  "recordings": ["full_tour"], "droppedFrames": 0,
  "visualRegression": { "pass": 19, "fail": 1, "noBaseline": 0 },
  "performance": { "frameMsP95": 11.8 },
  "gitCommit": "6a964f9", "unityVersion": "6000.4.6f1"
}
```

`gitCommit` を入れる。ランと修正の対応を後から辿るため（Editor から `git rev-parse HEAD` を読む。取れなければ空）。

### 集約の主体

ランナーが**開始時にランフォルダを作り、各ツールの出力先をそこへ向ける**。
既存の `DebugOutput/` 直下への出力は互換のため残し、`outputDirectory` が未指定のときだけ使う。
`VerificationRuns/` は利用側で gitignore する。

---

## スマホ閲覧（`karakuri/tools/serve_gallery.py`）

#90 の設計を維持する。Python 標準ライブラリのみ、LAN 内限定、認証なし、配信対象は `VerificationRuns/` のみ。

追加する表示:

- ラン一覧に **verdict**（pass / fail）を色で出す。失敗ランが一目で分かる
- ラン詳細で、失敗ステップの**証拠（スクショ・スナップショットの圧縮テキスト）を先頭に**出す
- 録画は未変換なら起動時に ffmpeg で mp4 化する（manifest の `ffmpegCommand` をそのまま実行）
- フォレンジックは `error.txt` の先頭行とスクショをカード表示
- 視覚回帰の差分画像はベースライン・実画像・差分を横並び

### セキュリティ（#90 と同じ）

外部公開しない。バインドは `0.0.0.0` だがルータ外へ晒さない前提を README と起動メッセージに明記する。

---

## 保持

`VerificationRuns/` は放置すると増える。`serve_gallery.py --prune 30` で 30 日より古いランを削除できるようにする。
削除はツール側で勝手にやらない（明示のコマンドのみ）。

---

## 検証方法

- 全画面巡回を 1 回実行し、`run-<ts>/` に上記の全ファイルが揃うこと
- 失敗する `expect` を仕込んだランを作り、ギャラリーの一覧で赤く出て、詳細の先頭に証拠が出ること
- スマホ（同一 Wi-Fi）から動画が再生できること

## スコープ外

- クラウドへのアップロード・外部公開・認証（#90 と同じ）
- 実機（iOS）からの成果物転送（実機検証フェーズで別途）
