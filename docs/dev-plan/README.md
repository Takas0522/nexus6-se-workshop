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
| [10-secrets-management.md](10-secrets-management.md) | user-secrets / Key Vault / コミット禁止物の整理 |
| [11-naming-conventions.md](11-naming-conventions.md) | Azure / M365 リソースの命名規約・サフィックス決定ルール |
| [12-news-portal-deployment.md](12-news-portal-deployment.md) | News Portal の Static Website デプロイ手順・公開 URL・Trigger Agent 巡回設定 |
| [13-foundry-runtime-config.md](13-foundry-runtime-config.md) | Foundry File Search・Bing Grounding・Trigger Agent のランタイム構成記録 |
| [14-container-build.md](14-container-build.md) | Hosted Agent の Dockerfile・ACR ビルド・タグ規約・smoke 確認 |
| [15-aca-deployment.md](15-aca-deployment.md) | Azure Container Apps への Hosted Agent デプロイ・RBAC・smoke 記録 |
| [16-foundry-trigger-wiring.md](16-foundry-trigger-wiring.md) | Foundry Trigger 未作成時の手動 enqueue E2E 手順・ACA ログ証跡・Trigger Agent 自動化試行結果 |
| [17-scenario-verification.md](17-scenario-verification.md) | Phase 3 シナリオ E2E 検証結果・キーワード照合記録 |
| [18-observability-check.md](18-observability-check.md) | Phase 4 観測性検証・App Insights クエリ結果 |
| [19-open-items.md](19-open-items.md) | 未解決 TODO と代替手段・優先度 |
| [20-fabric-db-build.md](20-fabric-db-build.md) | Fabric SQL Database 実プロビジョニング・投入件数・ACA 接続更新結果 |
| [21-fabric-medallion.md](21-fabric-medallion.md) | Fabric Bronze/Silver/Gold Lakehouse・Notebook 実行・Gold KPI 検証結果 |
| [22-fabric-rbac.md](22-fabric-rbac.md) | ACA Managed Identity の Fabric Workspace RBAC 付与・再 E2E 検証結果 |
| [23-foundry-fix.md](23-foundry-fix.md) | Foundry gpt-5.4 Chat Completions BadRequest の原因・修正・再 E2E 検証結果 |
| [24-teams-graph.md](24-teams-graph.md) | Teams 通知の Microsoft Graph + Managed Identity 設計変更・AppRole blocked 記録 |
| [25-teams-delegated.md](25-teams-delegated.md) | Teams Graph Delegated + Device Code Flow 方式・Key Vault refresh token 管理 |

## 技術スタック

- **言語**: C# / .NET 10
- **Agent Framework**: Microsoft Agent Framework for .NET（Azure AI Foundry 連携）
- **AI サービス**: Azure AI Foundry (Azure OpenAI)
- **データ基盤**: Microsoft Fabric / OneLake
- **ランタイム**: .NET 10 Worker Service (Console Host)
