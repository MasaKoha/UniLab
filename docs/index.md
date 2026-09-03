---
layout: default
title: Home
---

# UniLab

Unity プロジェクト向けの共通ライブラリ・エディタツール群。

## Documentation

- [Tools Guide](TOOLS_GUIDE.md) — エディタツールの使い方ガイド
- [AssetVault 利用ガイド](asset-vault-guide.md) — 概要・使い方・サンプル・差分配信運用の統合ガイド
- [RadarChartView](ui-component-radar-chart.md) — 1 枚メッシュで描く汎用レーダーチャート部品

## Design Docs

- [基盤3点セット 全体設計方針](design-unilab-foundation-overview.md) — AssetVault / IAP / Popup v2 の疎結合方針と asmdef 構成
- [UniLab.AssetVault](design-unilab-asset-vault.md) — Addressable 配信基盤（差分チェック・事前ダウンロード）
- [AssetVault CDN 配信設計](design-unilab-asset-cdn.md) — URL 規約・バージョニング（version.json）・アセット配置・S3/CloudFront
- [UniLab.IAP](design-unilab-iap.md) — UnityIAP 課金基盤（レシート検証注入・Pending 方式）
- [Popup v2](design-unilab-popup-v2.md) — ポップアップ基盤の汎用化（優先度キュー・View 供給抽象化）
- [UniLab.AI](design-unilab-ai-tools.md) — AI エージェント向け検証ツール群（別リポジトリへ切り出し可能な境界・実時間録画・条件待ちランナー）
- [UniLab.AI ロードマップ](design-unilab-ai-roadmap.md) — 自律デバッグ・自律プレイに向けた 10 ツールの全体設計と共通方針
  - [01 UI 状態スナップショット](design-unilab-ai-01-ui-snapshot.md) — 画面を構造化データで読む。すべての「目」
  - [02 シナリオ expect と合否](design-unilab-ai-02-scenario-expect.md) — シナリオをテストにする
  - [03 例外時フォレンジック](design-unilab-ai-03-exception-forensics.md) — 例外の瞬間の状況を一式保存
  - [04 入力ボキャブラリ](design-unilab-ai-04-input-vocabulary.md) — Input System 仮想デバイスによる生入力注入
  - [05 決定的リプレイ](design-unilab-ai-05-deterministic-replay.md) — シード固定と入力記録・再生
  - [06 モンキーテスター](design-unilab-ai-06-monkey-tester.md) — 不変条件つきランダム操作
  - [07 視覚回帰](design-unilab-ai-07-visual-regression.md) — ベースライン画像との差分
  - [08 性能計測](design-unilab-ai-08-performance-recorder.md) — ステップ単位のフレーム時間・GC・ドローコール
  - [09 RunArchive とスマホ閲覧](design-unilab-ai-09-run-archive.md) — ラン単位の成果物集約とギャラリー
  - [10 LLM 駆動の目標プレイ](design-unilab-ai-10-llm-play.md) — 目標を与えて遊ばせ、成功手順をテスト化
  - [11 入力可視化オーバーレイ](design-unilab-ai-11-input-overlay.md) — 押したボタン・ポインタ・クリック・タッチを画面に描き、動画に写す
