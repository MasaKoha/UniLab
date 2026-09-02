# 03 例外時フォレンジック 設計書

ステータス: 設計。ロードマップ M2
依存: 01 UI スナップショット

---

## 目的

例外やエラーログが出た**その瞬間**の状況を、自動で一式保存する。
現状は `FileLogSink` でログは残るが、「そのとき画面はどうだったか」「何を押した直後か」は残らない。
AI が原因へ辿るには、ログと状況が同じ時刻で揃っている必要がある。

---

## 動作

`Application.logMessageReceivedThreaded` を購読し、`LogType.Exception` と `LogType.Error` で発火する。

保存先: `DebugOutput/forensics/<yyyyMMdd-HHmmss>-<連番>/`

| ファイル | 内容 |
|---|---|
| `error.txt` | ログ種別・メッセージ・スタックトレース |
| `context.json` | フレーム番号・実時間・アクティブシーン・録画中ならフレーム番号と録画名・シナリオ実行中ならステップ番号と直前の操作 |
| `snapshot.json` | 01 の UI スナップショット |
| `screenshot.png` | `ScreenCapture.CaptureScreenshot`（次フレームで書かれる） |
| `hierarchy.json` | `SceneHierarchyDumper` の出力（結線漏れの確認用） |

`context.json` の「録画中のフレーム番号」が動画との対応を作る。
「動画の 4.1 秒で壊れた」→ manifest のマーカー → フォレンジックの `context.json` → スタックトレース、が一本で繋がる。

### スレッド

`logMessageReceivedThreaded` はワーカースレッドからも呼ばれる。
Unity API を触る収集はメインスレッドでしか行えないため、**発生をキューに入れ、次の `Update` で収集する**。
このため画面は「例外の次のフレーム」になる。実用上は問題にならないが、`context.json` に明記する。

### 抑制

- 同一ラン内の保存は既定 **20 件まで**。超えた分は件数だけ数えて `error.txt` を書かない（ログ洪水で数百フォルダができるのを防ぐ）
- 同じスタックトレースの再発は **1 件目だけ**保存し、以降は `context.json` の `repeatCount` を増やす
- `FileLogSink` の書き込み失敗など、フォレンジック自身が出すログは購読対象から除外する（再帰を防ぐ）

---

## API

```csharp
/// <summary>例外・エラーログの瞬間の状況を自動保存する。Boot で1回だけ生成し Initialize を呼ぶ。</summary>
public sealed class ExceptionForensics : IDisposable
{
    public void Initialize(string outputRootDirectory = null, int maxCaptureCount = 20);

    /// <summary>このラン中に保存した件数と抑制した件数。02 の結果 JSON へ転記する。</summary>
    public int CapturedCount { get; }
    public int SuppressedCount { get; }
}
```

`FileLogSink` と同じライフサイクル（Boot で生成、Dispose で購読解除）。karakuri では `BootLifetimeScope` に並べる。

### シナリオランナーとの連携

- ランナーは自分の現在ステップと直前の操作を `ForensicsContext.Current` に書き込む（静的な1箇所）
- 02 の結果 JSON は `exceptions` にフォレンジックのフォルダパスを列挙する
- `noException` の失敗時、証拠パスとしてフォルダを指す

---

## なぜスクリーンショットが1フレーム遅れてよいか

例外が出た瞬間の描画は、その例外を投げた処理の**前**の状態である可能性が高い（処理が完了していないため）。
次フレームの絵は「例外の結果として画面がどうなったか」を示し、こちらのほうが診断に役立つ場合が多い。
どちらが要るかは場合によるため、録画中であれば動画側に前フレームが残っていることを `context.json` に示す。

---

## 検証方法

- `PopupService` の破棄後アクセス（修正済み）を一時的に戻し、Play 終了時にフォレンジックが1件保存されること
- `context.json` に録画フレーム番号が入り、manifest のマーカーと突き合わせて動画の該当秒が特定できること
- 同じ例外を連続 100 回投げても保存フォルダが 1 つで `repeatCount=99` になること

## スコープ外

- 例外の自動修正（10 の領域）
- ネイティブクラッシュ（Unity のクラッシュハンドラに任せる）
