# 11 入力可視化オーバーレイ 設計書

ステータス: 設計。ロードマップ M3（04 と同時）
依存: なし（04 の注入入力も実機入力も同じ経路で拾う）。01・07 は本オーバーレイを除外する必要がある

---

## 目的

動画や静止画を見たとき、**「そのとき何を押したか」が画面に写っている**ようにする。

- ゲームパッド: どのボタンを押したかを模式図で光らせる
- キーボード: 押したキー名を表示する
- マウス: ポインタの位置・クリック（左右）・ドラッグの軌跡・スクロールを表示する
- タッチ: 指ごとの接触位置を円で表示する

動画で「画面が変わらない」場面を見たとき、押していないのか・押したのに反応しないのかが区別できないと診断できない。
入力が写っていれば、AI も人間も「入力→反応」の対応を動画だけで追える。

---

## 描画

### 方式

`ScreenSpaceOverlay` の専用 Canvas を UniLab.AI が生成し、最前面（`sortingOrder` = 32767）に置く。
録画は合成後のバックバッファを読むため、**オーバーレイは自動的に動画に写る**。追加の合成処理は要らない。

| 方式 | 判定 |
|---|---|
| **ゲーム内の Overlay Canvas（採用）** | 録画・スクショに自然に写る。ゲームと同じ座標系で位置が正確 |
| 録画後に ffmpeg で合成 | 入力ログと映像の時刻合わせが要り、ズレる。ポインタ座標の変換も要る。不採用 |
| Editor の Game View 上に IMGUI で描く | ビルドで使えない。実機検証で使えないのは致命的。不採用 |

### ゲームに影響を与えない

- Canvas に `GraphicRaycaster` を**付けない**。オーバーレイは一切の入力を受け取らない
- `CanvasGroup.blocksRaycasts = false`、`interactable = false`
- 生成する GameObject のルートに `UiOverlayMarker` コンポーネントを付ける。
  01 スナップショットと `UiLayoutAuditor` はこのマーカー配下を**除外**する（観測器が観測結果に混ざらない）
- アセットを持ち込まない。矩形は `Texture2D.whiteTexture`、円は起動時に 1 枚だけ生成する小さな円テクスチャ、文字は TMP の既定フォント

### ゲームパッドの模式図（右下、既定）

```
        [LB]              [RB]
      ┌────────────────────────┐
      │  ↑          (Y)        │
      │←   →    (X)   (B)      │
      │  ↓          (A)        │
      │   [select] [start]     │
      │   (LS)       (RS)      │
      └────────────────────────┘
```

押されているボタンを塗り、離しても **最低 300 ミリ秒（設定可）は点灯を残す**。
1 フレームだけの押下は動画の 1 コマにしか写らず見落とすため。スティックは傾きを点で示す。

### キーボード（左下）

押されたキー名を「Enter」「Q」「Esc」のチップで表示し、同じく 300 ミリ秒残す。同時押しは横に並べる。

### マウス

- ポインタ位置に矢印を描く（OS のカーソルは録画に写らないため、必ず描く）
- クリック時に押下ボタンごとの色でリング（左＝白、右＝黄、中＝青）を広げて消す
- ボタンを押したまま移動したら軌跡を線で残し、離したら 500 ミリ秒で消す（ドラッグの可視化）
- スクロールは上下の矢印を短く出す

### タッチ

指ごとに半透明の円を描き、`touchId` を小さく添える。マルチタッチも各指が見える。

### 操作ラベル（任意）

シナリオランナーが実行中なら、画面上端に現在のステップ（`step8 submit=FacilityCard1/Select`）を 1 行出せる。
動画の該当秒と manifest のマーカーの対応が、動画だけで読める。既定はオフ（画面を汚すため）。

---

## 入力の取得

`InputSystem.onEvent` を購読し、全デバイスの状態変化を拾う。**実機入力も 04 の注入入力も同じ経路**で来るため区別しない。
Input System が無い環境では `Input.GetKey` 系のポーリングへ落とす（`#if ENABLE_INPUT_SYSTEM`）。

デバイス種別ごとの表示部品は、**そのデバイスから初めて入力が来たときに生成**する。
パッドを使わないプロジェクトでパッドの図を出さない。

---

## 設定

```csharp
public sealed class InputOverlayOptions
{
    public bool showGamepad = true;
    public bool showKeyboard = true;
    public bool showPointer = true;
    public bool showTouch = true;
    public bool showStepLabel = false;
    public OverlayCorner gamepadCorner = OverlayCorner.BottomRight;
    public float scale = 1f;
    public float opacity = 0.85f;
    public float minimumVisibleSeconds = 0.3f;
}
```

```csharp
public static class InputOverlay
{
    public static void Show(InputOverlayOptions options = null);
    public static void Hide();
    public static bool IsVisible { get; }
}
```

シナリオ直下に `"inputOverlay": true`（または options のオブジェクト）で有効化。録画と一緒に使う想定。

---

## 07 視覚回帰との関係

オーバーレイは撮影に写るため、**ベースライン比較を汚す**。
既定はオフとし、`recordStart` のある録画シナリオでだけ推奨する。
静止画の回帰用シナリオでオンにした場合は、07 の無視領域にオーバーレイの矩形を自動追加する
（`InputOverlay` が自分の占有矩形を公開し、07 がそれを読む）。

---

## 検証方法

- パッドで A を 1 フレーム押す注入を行い、録画の少なくとも 9 フレーム（300 ミリ秒 × 30fps）で A が点灯していること
- マウスで左ドラッグし、録画に軌跡が写り、離した後に消えること
- オーバーレイ表示中に 01 のスナップショットを取り、オーバーレイの要素が 1 つも含まれないこと
- オーバーレイ表示中に `UiLayoutAuditor` を回し、検出 0 件のままであること（重なりとして拾われない）
- オーバーレイ表示中にゲームのボタンをクリックし、オーバーレイが入力を奪っていないこと

## スコープ外

- 入力履歴のタイムライン表示（画面下に流れるバー等）。まずは「今何を押しているか」に限る
- コントローラ種別ごとのボタン刻印（Xbox / PlayStation / Switch）。記号は汎用の A/B/X/Y 位置で表す
