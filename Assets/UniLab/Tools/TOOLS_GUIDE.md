# UniLab/Tools 使い方ガイド

Unity エディタ上でプロジェクト品質管理・ワークフロー効率化を行うエディタツール群。

---

## メニュー構成

```
UniLab/
├── Language Setting/
│   ├── Japanese
│   └── English
├── Tools/
│   ├── Build Check/
│   │   ├── iOS
│   │   └── Android
│   ├── Asset Favorite/
│   │   └── Open Window
│   ├── Open Folder/
│   │   ├── Open PlayerPrefs
│   │   └── PersistentDataPath
│   ├── Missing Checker/
│   │   ├── Open Window
│   │   └── Settings
│   ├── Asset Reference Finder/
│   │   ├── Open Window
│   │   └── Settings
│   ├── Unreferenced Asset Finder/
│   │   ├── Open Window
│   │   └── Settings
│   ├── Script Usage Checker/
│   │   └── Open Window
│   └── Hierarchy Favorite/
│       └── Open Window
└── Clear All Highlights

右クリックメニュー:
  Assets > UniLab > Find References In Project
  GameObject > UniLab > Add to Favorites
```

---

## 各ツール詳細

### 1. Build Checker

**メニュー**: `UniLab/Tools/Build Check/iOS` or `Android`

プラットフォーム別のビルド検証をワンクリックで実行する。実際にデプロイせず、ビルドエラーの有無だけを確認する。成功/失敗をコンソールに出力する。

---

### 2. Asset Reference Finder（アセット参照検索）

**メニュー**: `UniLab/Tools/Asset Reference Finder/Open Window`
**右クリック**: `Assets > UniLab > Find References In Project`

選択したアセットを**参照している側**を逆引き検索する。

- ドラッグ&ドロップで最大20アセットを同時検索
- 参照元ツリーを深度2まで展開可能
- Project ウィンドウ上で参照元を黄色 + "R" マーカーでハイライト
- CSV エクスポート対応

**Settings**: 検索対象フォルダ、対象拡張子、ハイライト色を設定可能

---

### 3. Missing Checker（Missing 参照チェッカー）

**メニュー**: `UniLab/Tools/Missing Checker/Open Window`

プロジェクト全体の壊れた参照（Missing Reference）を一括検出する。

- Missing Script（null コンポーネント）を検出
- フィールド単位で `{ComponentType}.{PropertyPath}` を報告
- Hierarchy ウィンドウに黄色 + ⚠ マーカーでハイライト
- Project ウィンドウにも親フォルダごとハイライト
- CSV エクスポート対応

**Settings**: 検索フォルダ、拡張子フィルタ、Hierarchy/Project のハイライト色

---

### 4. Unreferenced Asset Finder（未使用アセット検索）

**メニュー**: `UniLab/Tools/Unreferenced Asset Finder/Open Window`

どこからも参照されていないアセットを検出する。

- パス / サイズ / 拡張子でソート可能
- チェックボックスで複数選択 → 一括削除 or 隔離フォルダへ移動
- Resources / StreamingAssets / ビルドシーンは除外可能
- Project ウィンドウ上で赤色ハイライト
- CSV エクスポート対応

**Settings**: 検索フォルダ、拡張子フィルタ、除外設定、ハイライト色

---

### 5. Script Usage Checker（スクリプト使用状況チェッカー）

**メニュー**: `UniLab/Tools/Script Usage Checker/Open Window`

MonoBehaviour スクリプトがシーン・Prefab で使われているかを分析する。

- 「使用中」「未使用」のタブ表示
- 使用箇所（シーンパス / Prefab パス）を展開表示
- スクリプト名で検索フィルタ
- CSV エクスポート対応

**注意**: `AddComponent<T>()` 等の動的利用は検出しない（静的配置のみ）

---

### 6. Asset Favorite（アセットお気に入り）

**メニュー**: `UniLab/Tools/Asset Favorite/Open Window`

よく使うアセットをカテゴリ別に登録して素早くアクセスする。

- ドラッグ&ドロップでアセットを登録
- カスタムカテゴリを作成・変更可能
- 登録アセットをワンクリックで選択 / ダブルクリックで開く
- JSON で永続化（`Application.persistentDataPath/UniLab/Editor/FavoriteAssetsWindow.json`）

---

### 7. Hierarchy Favorite（ヒエラルキーお気に入り）

**メニュー**: `UniLab/Tools/Hierarchy Favorite/Open Window`
**右クリック**: `GameObject > UniLab > Add to Favorites`

ヒエラルキー上の GameObject をブックマークする。

- シーン別にグルーピング表示
- メモ欄で各エントリに注釈を付加
- 削除・未ロードの GameObject は "Missing" 表示
- GlobalObjectId でシリアライズ（保存/リロードに耐える）

**要件**: シーンが保存済みであること（GlobalObjectId 生成に必要）

---

### 8. Open Folder（フォルダを開く）

**メニュー**: `UniLab/Tools/Open Folder/`

- **Open PlayerPrefs**: OS 固有の PlayerPrefs 保存先を Finder/Explorer で開く
- **PersistentDataPath**: `Application.persistentDataPath` を開く

---

### 9. Clear All Highlights（ハイライト一括クリア）

**メニュー**: `UniLab/Clear All Highlights`

Asset Reference Finder / Missing Checker / Unreferenced Asset Finder の全ハイライトを一括解除する。

---

## 言語切り替え

**メニュー**: `UniLab/Language Setting/Japanese` or `English`

全ツールの UI ラベルを日本語/英語に切り替える。EditorPrefs に保存されるため次回起動時も維持される。

---

## 設定ファイル

| 種別 | 保存先 |
|---|---|
| Missing Checker Settings | `Assets/Generated/UniCore/MissingCheckerSettings.asset` |
| Asset Reference Finder Settings | `Assets/Generated/UniCore/AssetReferenceFinderSettings.asset` |
| Unreferenced Asset Finder Settings | `Assets/Generated/UniCore/UnreferencedAssetFinderSettings.asset` |
| Asset Favorite データ | `persistentDataPath/UniLab/Editor/FavoriteAssetsWindow.json` |
| Hierarchy Favorite データ | `persistentDataPath/UniLab/Editor/HierarchyFavorite.json` |
| 言語設定 | EditorPrefs `UniLab.EditorToolLabels.Language` |

---

## Codex / Claude Code との連携

これらのツールは Codex / Claude Code から直接実行することはできない（Editor GUI 操作が必要）。ただし以下の活用が可能:

- **ビルドチェック結果の解析**: コンソールログを貼り付けて原因分析を依頼
- **Missing Reference の修正**: Missing Checker の出力（パス + フィールド名）を共有し、修正コードを生成
- **未使用アセットの整理**: CSV エクスポート結果を共有し、削除対象の判断を支援
- **スクリプト使用状況の確認**: Grep / Glob で動的利用パターンを補完調査
