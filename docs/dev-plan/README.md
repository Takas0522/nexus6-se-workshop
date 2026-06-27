# 開発計画ドキュメント一覧

業務システム別ニュース分析シナリオを実現する **Hosted Agent（オーケストレーター）** の開発仕様書群です。

## 関連シナリオ

- [業務システム別ニュース分析シナリオ](../scenario/business-system-news-analysis-scenario.md)

## ドキュメント

| ファイル | 概要 |
|---|---|
| [01-overview.md](01-overview.md) | システム全体像・エージェント構成・フロー概要 |
| [02-agent-definitions.md](02-agent-definitions.md) | Agent1〜4 の役割・入出力・プロンプト仕様 |
| [03-hosted-agent-spec.md](03-hosted-agent-spec.md) | Hosted Agent（オーケストレーター）の仕様 |
| [04-implementation-guide.md](04-implementation-guide.md) | .NET 10 実装ガイド・プロジェクト構成・主要クラス設計 |
| [05-azure-configuration.md](05-azure-configuration.md) | Azure AI Foundry・依存サービスの構成仕様 |
| [06-fabric-data-ingestion.md](06-fabric-data-ingestion.md) | Fabric/OneLake への取り込み方式・パイプライン設計 |
| [07-fabric-data-model.md](07-fabric-data-model.md) | Fabric データモデル詳細・レコード数試算（30,000名・6ヶ月） |
| [08-skill-ds-design.md](08-skill-ds-design.md) | Skill.md / DS.md 設計（ベテランスタッフが作成する業務ナレッジファイル） |
| [09-azure-m365-configuration.md](09-azure-m365-configuration.md) | Azure / M365 必要構成まとめ・ロール割り当て・構築順序・コスト概算 |

## 技術スタック

- **言語**: C# / .NET 10
- **Agent Framework**: Microsoft Agent Framework for .NET（Azure AI Foundry 連携）
- **AI サービス**: Azure AI Foundry (Azure OpenAI)
- **データ基盤**: Microsoft Fabric / OneLake
- **ランタイム**: .NET 10 Worker Service (Console Host)
