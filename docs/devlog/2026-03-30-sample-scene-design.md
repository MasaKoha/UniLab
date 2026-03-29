# Sample Scene Design — 2026-03-30

## ナビゲーション構成

メニュー画面 → 機能詳細画面の2パネル切り替え。シーンロードなし。

```
UniLabSampleCanvas (Canvas)
  BG                   (Image, raycastTarget=false)
  MenuPanel            (RectTransform) ← active on menu
    ScrollView         (Image raycastTarget=false, ScrollRect)
      Viewport         (Image raycastTarget=true ← ScrollRect のドラッグ受け口)
        Content        (VerticalLayoutGroup + ContentSizeFitter)
          MenuBtn_*    (Image raycastTarget=true + Button)  ← ボタンのみ true
  FeaturePanel         (RectTransform) ← active on feature
    ScrollView         ... (同上構成)
  LogPanel             (Image, raycastTarget=false)
  Header               (Image, raycastTarget=false) ← 最後のsibling = raycast最優先
    BackButton         (Image raycastTarget=true + Button)
    Label              (Text, raycastTarget=false)
```

**Header は必ず Canvas の最後の sibling にすること。**
Unity UGUI は sibling index が高いほど raycast 優先度が高いため、
他のパネルより後に AddChild することで BackButton が確実にクリックを受け取れる。

## raycastTarget 設計原則

> **ボタン・インタラクティブ要素の Image だけ `raycastTarget = true`（デフォルト）。
> それ以外（背景 Image・Text・仕切り・装飾）はすべて `raycastTarget = false` に明示する。**

| 要素 | raycastTarget | 理由 |
|------|:---:|---|
| Button の Image (`targetGraphic`) | `true` | クリック判定に必要 |
| 背景パネル Image | `false` | 視覚のみ。裏面への raycast を妨げない |
| Text (Label) | `false` | テキストは入力を受け取らない |
| ScrollRect root Image (透明) | `false` | 透明背景。Viewport Image が代わりに受け取る |
| Viewport Image (ScrollRect 内) | `true` | 空白領域でのドラッグ・スクロールを受け取る |

### コード規約

```csharp
// NG: Image を追加しただけ（raycastTarget = true のまま）
var bg = new GameObject("BG", typeof(Image));

// OK: 背景・装飾は明示的に false
var image = go.GetComponent<Image>();
image.raycastTarget = false;

// OK: ボタン背景は true のままでよい（Button.targetGraphic として使うため）
var button = go.AddComponent<Button>();
button.targetGraphic = go.GetComponent<Image>(); // raycastTarget = true (default)
```

Codex にコードを生成させる際は、上記ルールをプロンプトに明記すること。

## 機能一覧 (FeatureId)

| Id | カテゴリ色 |
|----|-----------|
| LocalSave | #3A6BC8 |
| EncryptedStorage | #3A6BC8 |
| TextManager | #2E8B57 |
| InputBlock | #8B3A8B |
| Network | #8B3A3A |
| OfflineQueue | #8B3A3A |
| Grid | #6B4E2A |
| UniLabButton | #6B4E2A |
| AnimationPlayer | #6B4E2A |
| Toast | #6B4E2A |
| Loading | #6B4E2A |
| BackKey | #6B4E2A |
