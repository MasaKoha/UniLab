# 08 性能計測 設計書

ステータス: 設計。ロードマップ M4
依存: なし。02 の `expect` にしきい値の `kind` を足せる

---

## 目的

「重い」「カクつく」「メモリが増える」を、シナリオのステップ単位で数字にする。
録画の `droppedFrameCount` は録画負荷の指標であって、ゲーム自体の負荷ではない。分けて測る。

---

## 計測項目

`Unity.Profiling.ProfilerRecorder`（`UnityEngine.CoreModule`。追加依存なし）で毎フレーム取る。

| 項目 | 取り方 |
|---|---|
| フレーム時間 | `Time.unscaledDeltaTime`（ms） |
| GC 割り当て | `ProfilerRecorder("GC.Alloc")` の毎フレーム値（bytes） |
| GC 回数 | `GC.CollectionCount(0)` の差分 |
| ドローコール | `ProfilerRecorder("Draw Calls Count")` |
| 総メモリ | `Profiler.GetTotalAllocatedMemoryLong()`（ステップ末に1回） |
| SetPass | `ProfilerRecorder("SetPass Calls Count")` |

ドローコール系は開発ビルドか Editor でのみ有効。取れない環境では `-1` を入れる。

### 集計

ステップごとに: 平均・p95・最大フレーム時間、GC 合計 bytes、GC 回数、ドローコール平均・最大、ステップ末の総メモリ。
シナリオ全体: 同じ集計の合算、および**ステップ間の総メモリの単調増加**（リークの兆候）。

---

## 出力

`DebugOutput/performance/<シナリオ名>-<timestamp>.json`

```json
{
  "scenario": "full-screen-tour",
  "steps": [
    { "index": 8, "label": "TabButton2",
      "frameCount": 31, "frameMsAvg": 8.1, "frameMsP95": 12.4, "frameMsMax": 33.9,
      "gcAllocBytes": 18432, "gcCollections": 0,
      "drawCallsAvg": 42, "drawCallsMax": 58,
      "totalMemoryBytes": 412000000 }
  ],
  "summary": { "frameMsP95": 11.8, "gcAllocBytesTotal": 220000, "memoryGrowthBytes": 1200000, "memoryMonotonicGrowthSteps": 3 }
}
```

---

## API

```csharp
/// <summary>シナリオ実行中の性能をステップ単位で集計する。ランナーが開始・区切り・停止を呼ぶ。</summary>
public sealed class PerformanceRecorder : IDisposable
{
    public void Start();
    public void MarkStep(int stepIndex, string label);
    public PerformanceReport Stop();
}
```

シナリオ直下に `"recordPerformance": true` で有効化。

### 02 との連携

`expect` に足せる `kind`:

| kind | 意味 |
|---|---|
| `frameMsP95Below` | このステップの p95 フレーム時間が値未満 |
| `gcAllocBelow` | このステップの GC 割り当て合計が値未満 |
| `noGcCollection` | このステップ中に GC が走っていない（ホットパス検証用） |

---

## 録画との干渉

録画（04 のパイプライン）は GPU 読み戻しとワーカーのエンコードを行い、それ自体が負荷になる。
**性能計測と録画は同時に有効にしない**のを既定とし、両方指定されたら警告を出す。
どうしても同時に要る場合は、計測値に `recordingActive: true` を付けて区別できるようにする。

---

## 検証方法

- 全画面巡回で計測し、各タブの p95 が 16.7ms（60fps）以内であることを確認する（現状の基準値を得る）
- 意図的に `Update` で `new byte[1024*1024]` するコンポーネントを置き、そのステップだけ `gcAllocBytes` が跳ねること
- 工房のタブを 50 回往復するシナリオで `memoryMonotonicGrowthSteps` が増え続けないこと（リーク検知の実効性）

## スコープ外

- CPU プロファイラのサンプリング（Unity Profiler に任せる）
- GPU 時間（`ProfilerRecorder` で取れる環境もあるが、Metal での安定性を確認してから）
