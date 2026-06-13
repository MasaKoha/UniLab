# 2026-06-13 基盤3点セット設計

## 今日やったこと

Addressable 配信基盤・UnityIAP 課金基盤・ポップアップ基盤 v2 の設計書を作成（実装は未着手）。

- [design-unilab-foundation-overview.md](../design-unilab-foundation-overview.md)
- [design-unilab-asset-vault.md](../design-unilab-asset-vault.md)
- [design-unilab-iap.md](../design-unilab-iap.md)
- [design-unilab-popup-v2.md](../design-unilab-popup-v2.md)

## 主要な設計判断

| 判断 | 理由 |
|---|---|
| 3基盤間の asmdef 参照ゼロ。接続は `UniLab.Integration` orアプリ層のアダプタ | 利用プロジェクトが必要な基盤だけ持ち込めるようにする |
| AssetVault はスコープベースのハンドル管理（`IAssetScope`） | 個別 Release は漏れる。VContainer の Scoped Dispose に紐付けて構造的に解決 |
| IAP は Pending 方式（検証完了まで Confirm しない） | 検証中クラッシュでもトランザクションがストアに残り再処理される |
| IAP の結果は `PurchaseAsync` 戻り値と `OnPurchaseProcessed` の二系統 | Ask to Buy・プロモ購入・未完了トランザクション回収はリクエスト応答型では取りこぼす |
| Popup v2 は `IPopupViewProvider` 注入で View 供給を抽象化 | Popup 基盤が Addressables を知らないための要 |
| Version Defines で `UNILAB_ADDRESSABLES` / `UNILAB_IAP` を自動定義 | 手動 Scripting Define Symbols 追加を不要にする |

## レビューで潰した設計矛盾

doc-reviewer によるレビューを実施し、以下を修正済み。

- `UniLab.Integration` の条件コンパイル矛盾 → 「Addressables 未導入なら AssetVault / Integration をフォルダごと持ち込まない」運用に確定
- `AddressablesPopupViewProvider`（Singleton）が Scoped な `IAssetScope` を掴む captive dependency → `CreateScope()` で専用スコープを自己生成する方式に変更
- 二重付与リスク → 付与は `OnPurchaseProcessed` 一本化、`PurchaseAsync` 戻り値は UI フィードバック専用と断定
- `IAPProductDefinition`（入力定義）と `IAPProduct`（ストア取得済みランタイム情報）の責務を定義
- VContainer 登録例を規約準拠（View は `RegisterInstance`）に修正
- 公開 Observable は OnError を流さない方針を全基盤に明記

## 未決事項（実装前に決める）

1. リモートカタログ配信先: Unity CCD / Supabase Storage / 自前 CDN
2. レシート検証サーバ: topia の ASP.NET Core / Supabase Edge Functions
3. Unity IAP パッケージバージョン: 4.x / 5.x（実装着手時に最新サポート状況を確認）

## 次にやること

1. 設計レビュー（特に Popup v2 の後方互換方針と IAP の付与責務境界）
2. 実装は Popup v2 → AssetVault → IAP の順（overview 参照）
3. Popup v2 着手時: `IPopupService` / `IPopupViewProvider` のインターフェース定義から
4. manifest.json への `com.unity.addressables` 追加は AssetVault 着手時、`com.unity.purchasing` は IAP 着手時
