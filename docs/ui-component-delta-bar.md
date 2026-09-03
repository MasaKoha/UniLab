# DeltaBarView

`DeltaBarView` は `MaskableGraphic` 1 枚で基準線から左右に伸びる差分バーを描く部品で、装備変更前後の「攻 +4 / 速 -2」のような比較表示に使う。公開 API は `Initialize()`、`SetValue(float signedNormalizedValue)`、`SetStyle(DeltaBarStyle)`、`AnimateTo(float signedNormalizedValue, float durationSeconds, RadarChartEasing easing)`、`GetBarEndLocalPosition()` の 5 つで、ラベルや数値は呼び出し側が別配置する。アロケーション方針は値保持だけに留め、`SetValue` と `OnPopulateMesh(VertexHelper)` のホットパスでは再確保しない。

## 演出と色

- `AnimateTo(float signedNormalizedValue, float durationSeconds, RadarChartEasing easing)`: 現在値から目標値へ補間する。`IsAnimating` で進行中か分かる。駆動は R3 の `Observable.EveryUpdate(destroyCancellationToken)` で、`Update` を持たず、アニメーション中の追加アロケーションは無い。進行中に `SetValue` / `AnimateTo` を呼ぶと前の補間を止めて差し替える
- `DeltaBarStyle.PositiveColor` / `NegativeColor`: 正負でバー色を分ける
- `DeltaBarStyle.ZeroColor`: 値 0 のときに見せる基準線色。バー本体は描かない
- `DeltaBarStyle.BaselineColor` / `BaselineThickness` / `BaselinePosition`: 基準線の色・太さ・位置。`BaselinePosition` は 0〜1 で、既定は中央の `0.5`
- `DeltaBarStyle.BackgroundColor` / `OutlineColor` / `OutlineThickness`: 背景と外枠

## API

### `void Initialize()`

単一バーを描く初期状態を確定する。`SetValue` が先に来た場合も落とさず、暗黙に同じ初期化を行う。

### `void SetValue(float signedNormalizedValue)`

値を -1〜+1 に Clamp して保持する。正値は基準線から右へ、負値は左へ伸ばす。

### `void SetStyle(DeltaBarStyle style)`

正負色、基準線、外枠をまとめて差し替える。

### `void AnimateTo(float signedNormalizedValue, float durationSeconds, RadarChartEasing easing)`

現在値から目標値へ補間し、毎フレーム再描画する。`durationSeconds <= 0` または `RadarChartEasing.None` のときは即時反映する。

### `bool IsAnimating`

値アニメーションの再生中かを返す。

### `Vector2 GetBarEndLocalPosition()`

バー先端の局所座標を返す。差分値ラベルや矢印の配置基準に使う。

## Style

`DeltaBarStyle` は `readonly struct` で、以下を持つ。

- `PositiveColor`
- `NegativeColor`
- `ZeroColor`
- `BackgroundColor`
- `BaselineColor`
- `BaselineThickness`
- `BaselinePosition`
- `OutlineColor`
- `OutlineThickness`

既定値は `DeltaBarStyle.Default` で取得できる。

## 使用例

```csharp
using UniLab.UI;
using UnityEngine;

public sealed class EquipmentDeltaPresenter
{
    private readonly DeltaBarView _deltaBarView;

    public EquipmentDeltaPresenter(DeltaBarView deltaBarView)
    {
        _deltaBarView = deltaBarView;
    }

    public void Initialize()
    {
        _deltaBarView.Initialize();
        _deltaBarView.SetStyle(new DeltaBarStyle(
            positiveColor: new Color(0.3f, 0.85f, 0.4f, 1f),
            negativeColor: new Color(1f, 0.35f, 0.35f, 1f),
            zeroColor: new Color(1f, 1f, 1f, 0.75f),
            backgroundColor: new Color(1f, 1f, 1f, 0.08f),
            baselineColor: new Color(1f, 1f, 1f, 0.45f),
            baselineThickness: 2f,
            baselinePosition: 0.5f,
            outlineColor: Color.white,
            outlineThickness: 1f));
        _deltaBarView.SetValue(0.4f);
    }

    public Vector2 GetDeltaLabelPosition()
    {
        return _deltaBarView.GetBarEndLocalPosition();
    }
}
```

## 注意点

- 値の単位や上限は持たない。-1〜+1 への正規化は呼び出し側が責任を持つ
- ラベルや数値は描かない。必要な位置は `GetBarEndLocalPosition` で取得する
- `BaselinePosition` を左右に寄せると、伸びる最大長も同じ比率で変わる
