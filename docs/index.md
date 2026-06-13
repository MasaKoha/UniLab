---
layout: default
title: Home
---

# UniLab

Unity プロジェクト向けの共通ライブラリ・エディタツール群。

## Documentation

- [Tools Guide](TOOLS_GUIDE.md) — エディタツールの使い方ガイド
- [AssetVault 利用ガイド](asset-vault-guide.md) — 概要・使い方・サンプル・差分配信運用の統合ガイド

## Design Docs

- [基盤3点セット 全体設計方針](design-unilab-foundation-overview.md) — AssetVault / IAP / Popup v2 の疎結合方針と asmdef 構成
- [UniLab.AssetVault](design-unilab-asset-vault.md) — Addressable 配信基盤（差分チェック・事前ダウンロード）
- [UniLab.IAP](design-unilab-iap.md) — UnityIAP 課金基盤（レシート検証注入・Pending 方式）
- [Popup v2](design-unilab-popup-v2.md) — ポップアップ基盤の汎用化（優先度キュー・View 供給抽象化）
