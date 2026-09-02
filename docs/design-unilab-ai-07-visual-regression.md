# 07 視覚回帰 設計書

ステータス: 設計。ロードマップ M4
依存: なし（02 の `expect` に `kind` を足すのは任意）

---

## 目的

01 のスナップショットは**構造**を読むが、**見た目**は読めない。
ルーンスロットの自動縮小（文字が極小 2 行になっていた）はレイアウト監査でも構造でも検出できず、
画像を見て初めて分かった。この種の変化を、ベースライン画像との比較で機械的に拾う。

---

## 方式

| 方式 | 判定 |
|---|---|
| **画素差分（縮小＋許容率）（採用）** | 依存なし・実装が小さい・ピクセルフォントの UI に向く |
| 知覚的ハッシュ（pHash） | 小さな文字の変化を見逃す。不採用 |
| SSIM | 外部依存かそれなりの実装量。v2 で検討 |

### 比較手順

1. ベースラインと対象を同じ解像度へ揃える（違えば即「解像度不一致」として失敗）
2. 双方を **1/2 に縮小**してノイズ（アンチエイリアス・サブピクセル）を潰す
3. 画素ごとに RGB 差の最大値を取り、しきい値（既定 24/255）を超えた画素を「変化」とする
4. 変化画素の割合が許容率（既定 0.5%）を超えたら失敗
5. **無視領域**（時刻表示・乱数で変わる数値など）を矩形で指定でき、そこは比較しない
6. 差分画像を出力する（変化画素を赤で塗り、他は半透明にした 1 枚）

### ベースライン

```
<利用側リポジトリ>/Baselines/<シナリオ名>/<capture名>.png
Baselines/<シナリオ名>/ignore.json          無視領域（capture 名 → 矩形の配列）
```

ベースラインは**利用側リポジトリで git 管理**する（意図した見た目の変更は PR の差分として見える）。
UniLab.AI には置かない。

### 更新

- `VisualRegression/Accept All`（メニュー）: 直近の実行結果をベースラインへ上書き
- `Accept <capture>`: 1 枚だけ
- 意図した変更（今回のルーンスロット幅変更など）は、修正 PR の中でベースラインも更新する

---

## 出力

`DebugOutput/visual-regression/<run-timestamp>/`

| ファイル | 内容 |
|---|---|
| `report.json` | capture ごとの結果（pass / fail / no-baseline / size-mismatch）、変化率、差分画像のパス |
| `<capture>-diff.png` | 差分の可視化 |
| `<capture>-actual.png` | 比較に使った実画像（縮小前） |

`no-baseline` は失敗にしない。「初回」として記録し、Accept を促す。

---

## API（Editor 専用）

比較は Editor 側で行う（`Texture2D.LoadImage` と画素ループ。ランタイム負荷を持ち込まない）。

```csharp
public static class VisualRegression
{
    /// <summary>撮影結果ディレクトリとベースラインを比較し、レポートのパスを返す。</summary>
    public static string Compare(string capturesDirectory, string baselinesDirectory, VisualRegressionOptions options);

    public static void AcceptAll(string capturesDirectory, string baselinesDirectory);
    public static void Accept(string captureName, string capturesDirectory, string baselinesDirectory);
}
```

シナリオ完了後に自動で回す場合は、02 の結果 JSON に `visualRegression` としてレポートパスを載せる。
02 に `kind: "visualMatch"` を足せば、ステップ単位の合否にも組み込める（M4 で判断）。

---

## 無視領域の考え方

比較を壊す変動要素は、無視するより**シナリオ側で固定する**のが先。
ゴールドや転生ポイントは 05 のシード固定と開始セーブの固定で一致させる。
それでも変わるもの（実時間表示など）だけを `ignore.json` に書く。無視領域が増えるのは設計の負け。

---

## 検証方法

- 現在の全画面巡回の撮影 20 枚をベースラインとして Accept し、再実行で 20 枚すべて pass になること
- ルーンスロット幅を意図的に 150 へ戻して実行し、編成タブの capture が fail、差分画像でスロット部分が赤くなること
- 資産バーの数値を変えて実行し、無視領域を設定すると pass に変わること

## スコープ外

- アニメーション途中の比較（撮影は settle 後の静止画に限る）
- 3D シーンの比較（可能だが、光源・パーティクルの揺らぎで許容率の調整が要る。3D 導入時に再検討）
