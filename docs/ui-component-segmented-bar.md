# SegmentedBarView

`SegmentedBarView` は `MaskableGraphic` 1 枚で区切り付きのバーを描く部品で、HP/MP のような「10 目盛り中 7.4」の表示に使う。公開 API は `Initialize(int segmentCount)`、`SetValue(float normalizedValue)`、`SetStyle(SegmentedBarStyle)`、`AnimateTo(float normalizedValue, float durationSeconds, RadarChartEasing easing)`、`SetGlow(float intensity)`、`PlayCharge(float durationSeconds, float shakeAmplitudePixels)`、`PlayBurst(float durationSeconds)`、`GetSegmentLocalRect(int index)`、`GetFilledSegmentLocalRect(int index)` で、ラベルや数値は呼び出し側が別配置する。アロケーション方針は `Initialize` 時の内部バッファ確保に限定し、`SetValue` と `OnPopulateMesh(VertexHelper)` のホットパスでは再確保しない。

## 演出と色

- `AnimateTo(float normalizedValue, float durationSeconds, RadarChartEasing easing)`: 現在値から目標値へ補間する。`IsAnimating` で進行中か分かる。駆動は R3 の `Observable.EveryUpdate(destroyCancellationToken)` で、`Update` を持たず、アニメーション中の追加アロケーションは無い。進行中に `SetValue` / `AnimateTo` を呼ぶと前の補間を止めて差し替える
- `SetGlow(float intensity)`: 0〜1 に Clamp し、塗り色を `FillColor` もしくはグラデーション評価色から `GlowColor` へ寄せる。シェーダーなしで「白く光る」代用演出を作る
- `PlayCharge(float durationSeconds, float shakeAmplitudePixels)`: 発光量を 0 から 1 へ上げながら、`RectTransform.anchoredPosition` を横方向に微振動させる。終了時は元位置へ戻る
- `PlayBurst(float durationSeconds)`: 発光量を 1 から 0 へ落としつつ、現在値を 0 に戻す。次の `AnimateTo` を呼び出し側がつなぐ前提
- `SegmentedBarStyle.FillStartColor` / `FillEndColor`: 左右グラデーション。両方が透明なら `FillColor` 単色にフォールバックする
- `SegmentedBarStyle.BackgroundColor`: 未充填セグメントの色
- `SegmentedBarStyle.GlowColor`: 発光代用で塗り色を寄せる先。既定値は白
- `SegmentedBarStyle.SeparatorColor` / `SeparatorThickness` / `SegmentSpacing`: 区切り線と隙間。セグメント 1 個の長さが 1px 未満なら視認性より頂点増加の損失が大きいため区切り線を描かない
- `SegmentedBarStyle.Vertical`: `true` で縦積み表示に切り替える

## API

### `void Initialize(int segmentCount)`

セグメント数を確定し、内部バッファを確保する。1 未満は 1 に丸める。`SetValue` が先に来た場合も落とさず、暗黙に 1 セグメントで初期化する。

### `void SetValue(float normalizedValue)`

値を 0〜1 に Clamp して保持する。`0.74` を 10 セグメントに入れると、7 個を全充填し 8 個目だけ 40% 充填する。

### `void SetStyle(SegmentedBarStyle style)`

色・線幅・隙間・縦横方向をまとめて差し替える。

### `void AnimateTo(float normalizedValue, float durationSeconds, RadarChartEasing easing)`

現在値から目標値へ補間し、毎フレーム再描画する。`durationSeconds <= 0` または `RadarChartEasing.None` のときは即時反映する。

### `void SetGlow(float intensity)`

発光量を 0〜1 に Clamp して保持する。0 で通常色、1 で `GlowColor` に達する。

### `void PlayCharge(float durationSeconds, float shakeAmplitudePixels)`

発光を溜めながら横方向に微振動させる。演出完了時は元の `anchoredPosition` に戻る。

### `void PlayBurst(float durationSeconds)`

発光を減衰させながら値を 0 に戻す。`durationSeconds <= 0` のときは即時に消灯して 0 に戻す。

### `bool IsAnimating`

値補間、溜め、弾けのいずれかが再生中かを返す。

### `Rect GetSegmentLocalRect(int index)`

指定セグメントの局所矩形を返す。セグメント上にラベルやアイコンを置く位置計算の基準に使う。

### `Rect GetFilledSegmentLocalRect(int index)`

指定セグメントの現在の塗り矩形を返す。部分充填率を外部の数値ラベルやテストで扱いたいときに使う。

## Style

`SegmentedBarStyle` は `readonly struct` で、以下を持つ。

- `FillColor`
- `FillStartColor`
- `FillEndColor`
- `BackgroundColor`
- `GlowColor`
- `SeparatorColor`
- `SeparatorThickness`
- `OutlineColor`
- `OutlineThickness`
- `SegmentSpacing`
- `Vertical`

既定値は `SegmentedBarStyle.Default` で取得できる。

## 使用例

```csharp
using UniLab.UI;
using UnityEngine;

public sealed class StatusGaugePresenter
{
    private readonly SegmentedBarView _segmentedBarView;

    public StatusGaugePresenter(SegmentedBarView segmentedBarView)
    {
        _segmentedBarView = segmentedBarView;
    }

    public void Initialize()
    {
        _segmentedBarView.Initialize(10);
        _segmentedBarView.SetStyle(new SegmentedBarStyle(
            fillColor: new Color(0.2f, 0.8f, 0.4f, 1f),
            fillStartColor: Color.clear,
            fillEndColor: Color.clear,
            backgroundColor: new Color(1f, 1f, 1f, 0.12f),
            glowColor: Color.white,
            separatorColor: new Color(1f, 1f, 1f, 0.6f),
            separatorThickness: 1f,
            outlineColor: Color.white,
            outlineThickness: 1f,
            segmentSpacing: 2f));
        _segmentedBarView.SetValue(0.74f);
    }

    public Vector2 GetValueLabelPosition()
    {
        var filledRect = _segmentedBarView.GetFilledSegmentLocalRect(7);
        return new Vector2(filledRect.xMax, filledRect.center.y);
    }
}
```

## 演出のつなぎ方

満タン直前の溜めを見せたい場合は `PlayCharge(durationSeconds, shakeAmplitudePixels)` を呼び、終了後に `PlayBurst(durationSeconds)` をつなぐ。`PlayBurst` は値を 0 に戻すため、その後の再充填は `AnimateTo` で開始する。

## 注意点

- セグメント数や値の単位は持たない。割合への正規化は呼び出し側が責任を持つ
- ラベルや数値は描かない。必要な位置は `GetSegmentLocalRect` / `GetFilledSegmentLocalRect` で取得する
- `MaskableGraphic` 派生なので、親の `Mask` / `RectMask2D` の影響を受ける
