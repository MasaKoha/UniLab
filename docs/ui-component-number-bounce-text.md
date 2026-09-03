# NumberBounceText

`NumberBounceText` は `TextMeshProUGUI` に整数値の差し替え、跳ね演出、カウントアップを付ける小部品で、Lv 表示、コンボ数、ダメージ、所持数の変化表示に使う。公開 API は `SetValue(int value)`、`Bounce(int value, float durationSeconds = 0.25f, float peakScale = 1.6f)`、`CountUp(int fromValue, int toValue, float durationSeconds)`、`Format`、`IsPlaying` の 5 つ。駆動は R3 の `Observable.EveryUpdate(destroyCancellationToken)` で、`Update` を増やさずに `RectTransform.localScale` を直接更新する。

## 演出と表示

- `SetValue(int value)`: 値を即時反映し、進行中の演出を止める
- `Bounce(int value, float durationSeconds = 0.25f, float peakScale = 1.6f)`: 値を差し替えた直後に `1.0 -> peakScale -> 1.0` の跳ねを再生する。再生中に再度呼んでもスケールを積み上げず、現在スケールから再開する
- `CountUp(int fromValue, int toValue, float durationSeconds)`: 開始値から終了値まで整数で補間し、最後に `Bounce` で着地させる
- `Format`: `"{0}"` を含む表示書式。`"Lv{0}"` や `"Combo {0}"` のように使う
- `IsPlaying`: カウントアップまたは跳ね演出の再生中かを返す

## API

### `void SetValue(int value)`

値を即時反映し、スケールを等倍へ戻す。

### `void Bounce(int value, float durationSeconds = 0.25f, float peakScale = 1.6f)`

値を差し替えて跳ね演出を再生する。`durationSeconds <= 0` のときも 1 フレームで着地する。

### `void CountUp(int fromValue, int toValue, float durationSeconds)`

整数表示を開始値から終了値へ進め、完了時に跳ね演出を再生する。`durationSeconds <= 0` または開始値と終了値が同じときは、終了値へ即時更新して跳ね演出だけを再生する。

### `string Format`

表示書式。未設定や空文字にすると既定の `"{0}"` に戻る。

### `bool IsPlaying`

カウントアップまたは跳ね演出の再生中かを返す。

## Style

専用の style struct は持たない。見た目は `TextMeshProUGUI` 側のフォント、色、アウトライン、マテリアル設定に従う。

## 使用例

```csharp
using UniLab.UI;

public sealed class LevelBadgePresenter
{
    private readonly NumberBounceText _levelText;

    public LevelBadgePresenter(NumberBounceText levelText)
    {
        _levelText = levelText;
    }

    public void Initialize(int level)
    {
        _levelText.Format = "Lv{0}";
        _levelText.SetValue(level);
    }

    public void OnLevelUp(int previousLevel, int nextLevel)
    {
        _levelText.CountUp(previousLevel, nextLevel, 0.4f);
    }

    public void OnComboUpdated(int comboCount)
    {
        _levelText.Format = "Combo {0}";
        _levelText.Bounce(comboCount, 0.2f, 1.5f);
    }
}
```

## 注意点

- 値は整数専用。小数表示が必要なら別部品に分けるべき
- スケール更新は `RectTransform.localScale` を使うため、親側アニメーションと競合させるべきではない
- 書式変更は現在値の再描画を即時に行う
