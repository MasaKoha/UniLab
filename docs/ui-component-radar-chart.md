# RadarChartView

`RadarChartView` は `MaskableGraphic` 1 枚で外枠・軸線・値多角形を描く汎用レーダーチャート部品で、能力値比較やプロフィール可視化に使う。公開 API は `Initialize(int axisCount)`、`SetValues(ReadOnlySpan<float>)`、`SetStyle(RadarChartStyle)`、`GetVertexLocalPosition(int axisIndex, float radiusScale)` の 4 つで、軸数 3〜12 とラベル配置を呼び出し側から明示制御できる。アロケーション方針は `Initialize` 時の内部バッファ確保に限定し、`SetValues` と `OnPopulateMesh(VertexHelper)` のホットパスでは再確保しない。

## 演出と色（追加）

- `AnimateTo(ReadOnlySpan<float> target, float durationSeconds, RadarChartEasing easing = OutBack)`: 現在値から目標値へ補間。`PlayGrowFromCenter(float durationSeconds)`: 全軸 0 から現在値へ伸びる（開いた瞬間用）。`IsAnimating` で進行中か分かる。駆動は R3 の `Observable.EveryUpdate(destroyCancellationToken)` で、`Update` を持たず、アニメーション中の追加アロケーションは無い。進行中に `SetValues` / `AnimateTo` を呼ぶと前の補間を止めて差し替える
- `SetAxisColors(ReadOnlySpan<Color>)`: 頂点ごとの色。値多角形の塗り・縁・軸線に頂点色として反映。未設定なら Style の単色
- `RadarChartStyle.FillCenterColor` / `FillEdgeColor`: 中心→外周の放射グラデーション（頂点カラー）。未設定（透明）なら `FillColor` 単色にフォールバック
- イージング: `None` / `Linear` / `OutCubic` / `OutBack`

## 値が外枠を越える表現

`MaximumNormalizedValue`（既定 1.5）まで 1 を超える値を受け付ける。値多角形は外枠の外側へはみ出して描かれるため、「上限を振り切った」ことを見た目で示せる。外枠と軸ラベルは矩形内に収める前提なので、はみ出しぶんの余白は呼び出し側が確保する。0 未満は 0 に丸める。
