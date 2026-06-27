# nexus6-se-workshop

顧客 **Demo 用** の最小構成リポジトリです。想定ユースケース・シナリオと、それを実現するための最低限の Demo アプリケーション / 設計ドキュメントを含みます。

> 本リポジトリは Demo 想定の最低限構成です。本番運用相当の冗長化・スケジューラ・パイプライン自動化等は対象外とし、必要に応じ将来拡張ポイントとして記載しています。

## DevContainer

このリポジトリは **DevContainer** に対応しています。VS Code + Dev Containers 拡張（または GitHub Codespaces）で開くと、下記のツールがすべてセットアップされた状態で開発を開始できます。

| セットアップ内容 | 詳細 |
|---|---|
| .NET 10 SDK | `global.json` の `10.0.301` を `~/.dotnet` ユーザーインストールで参照 |
| Azure CLI (`az`) | `containerapp` 拡張込みでインストール |
| Azure Developer CLI (`azd`) | `postCreate` スクリプトでインストール |
| GitHub CLI (`gh`) | インストール済み |
| GitHub Copilot CLI (`copilot`) | `curl -fsSL https://gh.io/copilot-install | bash` でインストール |
| Docker | ホスト（WSL）の Docker Engine をソケット経由で使用（Docker outside of Docker） |
| EF Core CLI (`dotnet ef`) | DemoDataGenerator のマイグレーション用 |
| VS Code 拡張 | C# Dev Kit・Azure Dev・GitHub Copilot・Markdown 等 |

```bash
# コンテナ起動後の認証（初回のみ）
az login
gh auth login
azd auth login
```

> `src/*/appsettings.Development.json` は `.gitignore` 対象です。接続文字列・`GITHUB_TOKEN` 等はコンテナ内で `dotnet user-secrets` または環境変数で設定してください。

## 必要なツール・前提環境

### ローカル開発ツール

| ツール | バージョン | 用途 |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.x** | news-analysis-agent / DemoDataGenerator のビルド・実行 |
| [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) | 最新 | Azure リソース操作・Container Apps デプロイ |
| [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) | 最新 | インフラプロビジョニング（将来 IaC 追加時） |
| [GitHub CLI (gh)](https://cli.github.com/) | 最新 | リポジトリ操作・Actions 管理 |
| [Docker (WSL)](https://docs.docker.com/engine/install/ubuntu/) | 最新 | Container Apps 向けコンテナイメージビルド（Docker Desktop 不使用・WSL 上の Docker Engine を使用） |
| [Git](https://git-scm.com/) | 2.x 以上 | バージョン管理 |
| [GitHub Copilot CLI](https://docs.github.com/copilot/using-github-copilot/using-github-copilot-in-the-command-line) | 最新 | DemoDataGenerator の Copilot SDK ランタイム（CLI が自動バンドルされるため通常は別途不要だが、ローカル動作確認に使用） |

`.NET 10 SDK` は `global.json` で `10.0.301` を指定しています。DevContainer / ローカルではユーザー領域 `~/.dotnet` へ導入し、`DOTNET_ROOT` と `PATH` で優先参照します。

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
dotnet --list-sdks
```

現行実装では Preview API 差分によるビルド失敗を避けるため、Microsoft Agent Framework は独自プレースホルダ経由で分離しています。正式 API が安定した時点で `docs/dev-plan/04-implementation-guide.md` の想定パッケージへ置換します。

### Azure・クラウドリソース（既存利用 / RG: `SEWorkShopC12` / Sweden Central）

| サービス | リソース名 | SKU | 用途 |
|---|---|---|---|
| **Azure AI Foundry** | `fd-PartnerIQ` | S0 (AIServices) | LLM 推論（`gpt-5.4`）・File Search・Grounding with Bing Search |
| **Foundry Project** | `proj-PartnerIQ` | - | Agent 実行 / Knowledge 管理 |
| **Azure AI Search** | `iq-knowledge-source` | Standard | Foundry File Search のバックエンドとして共用 |
| **Microsoft Fabric Capacity** | `fabricswedencu001` | F4 | Fabric の容量ライセンス |
| **Microsoft Fabric Workspace** | `fabric_seworkshop_ws1` | - | Lakehouse / SQL Database / Notebook を配置 |

### Azure・クラウドリソース（新規作成 / RG: `rg-nexus6-swc` / Sweden Central）

| サービス | リソース名（案） | SKU | 用途 |
|---|---|---|---|
| **ADLS Gen2**（Skill.md 用） | `stnexus6skill1t2i` | Standard LRS | Skill.md ファイル格納・OneLake Shortcut 基盤 |
| **Storage Static Website**（News Portal 用） | `stnexus6portal1t2i` | Standard LRS | 静的 HTML ホスト（Public + `robots.txt` で検索除外） |
| **Azure Key Vault** | `kv-nexus6-swc` | Standard | Teams Workflows URL 等の外部 API キー |
| **Azure Container Apps Env** | `cae-nexus6-swc` | Consumption | Hosted Agent 実行環境 |
| **Azure Container App** | `ca-nexus6-hosted-agent` | Consumption | .NET 10 Hosted Agent ホスティング |
| **Azure Container Registry** | `crnexus6swc` | Basic | コンテナイメージ管理 |
| **Application Insights** | `appi-nexus6-swc` | Pay-as-you-go | エージェント実行ログ・トークン使用量監視 |

News Portal 公開 URL: <https://stnexus6portal1t2i.z1.web.core.windows.net/>

### M365 / Identity

| サービス | 状態 | 用途 |
|---|---|---|
| **Microsoft Entra ID** | 既存テナント | Managed Identity（全サービス認証の統一基盤） |
| **Microsoft Teams (Workflows)** | **後追い構築**（チャネル未作成） | Agent 4 の Adaptive Card 通知。URL は Key Vault シークレットですげ替え可能 |

> **認証方針**: Azure リソースへの認証は Container Apps の **System Assigned Managed Identity** に統一。API キー・サービスプリンシパルシークレットは原則 Key Vault 管理とし、コードやリポジトリに含めない。ローカル / Codespaces 実行時は `az login` 認証情報（`DefaultAzureCredential` の `AzureCliCredential` チェーン）を使用する。

### デモデータ生成（DemoDataGenerator）追加依存

| 項目 | 内容 |
|---|---|
| **GitHub Copilot サブスクリプション** | Copilot SDK の実行に必要（または BYOK 設定） |
| **Fabric SQL Database × 16** | 業務システム単位のDB（common 1 + mobile 5 + ecommerce 5 + fintech 5）。`fabric_seworkshop_ws1` 内に作成 |
| **代表 CSV 1 ファイル** | `scripts/seed-data/scenario_seed.csv`（手動配置） |
| 環境変数 `GITHUB_TOKEN` | `dotnet user-secrets` で管理・コミット禁止 |

> Fabric SQL Database × 16 は設計上の最終形です。現行 DemoDataGenerator は 4 DB 分割で動作し、Notebook 側 fallback で両対応します（詳細: [06](docs/dev-plan/06-fabric-data-ingestion.md) / [19](docs/dev-plan/19-open-items.md)）。

## ディレクトリ構成

```
nexus6-se-workshop/
├── docs/
│   ├── scenario/          # シナリオ文書（為替・競合統合・日銀利上げ）
│   ├── usecase/           # 事業部別業務システム・データモデル定義
│   └── dev-plan/          # Hosted Agent 開発仕様（Agent / Foundry / Fabric / M365）
├── fabric/                # Fabric Lakehouse / Notebook / seed / SQL 関連資材
├── knowledge/             # Skill.md / DS.md など業務ナレッジ
└── src/
    ├── news-portal/       # Demo 入力源：静的 HTML ニュースポータル
    ├── news-analysis-agent/   # ニュース分析エージェント本体
    │   ├── src/           # Agent 1〜4・Orchestration・Tools・Models
    │   └── tests/
    └── DemoDataGenerator/ # Copilot SDK によるデモデータ生成サービス
        ├── src/           # App / Data
        └── tests/
```

### 主要コンポーネント

| コンポーネント | パス | 技術 |
|---|---|---|
| ニュースポータル | `src/news-portal/` | 静的 HTML |
| ニュース分析エージェント | `src/news-analysis-agent/` | Azure AI Foundry + Microsoft Agent Framework for .NET |
| デモデータ生成 | `src/DemoDataGenerator/` | GitHub Copilot SDK (.NET) + EF Core + Fabric SQL Database |
| 業務システムDB | Fabric SQL Database × 16 | common / mobile × 5 / ecommerce × 5 / fintech × 5 |
| 分析・BI | Microsoft Fabric OneLake | Fabric SQL Database → OneLake 自動同期 |

### 関連ドキュメント

| ドキュメント | 内容 |
|---|---|
| [シナリオ](docs/scenario/business-system-news-analysis-scenario.md) | 為替・競合統合・日銀利上げの3シナリオ定義 |
| [共通顧客ID基盤](docs/usecase/common-customer-identity-system.md) | 全事業部共通の顧客統合IDシステム |
| [モバイル通信データモデル](docs/usecase/mobile-telecom-system-data-model.md) | 顧客管理・契約管理・課金・在庫・CRM |
| [Eコマースデータモデル](docs/usecase/ecommerce-system-data-model.md) | 商品・受注・在庫・会員ポイント・マーケティング |
| [Fintechデータモデル](docs/usecase/fintech-system-data-model.md) | 口座・カード・決済GW・FX・与信 |
| [Copilot SDK デモデータ生成仕様](docs/usecase/copilot-sdk-demo-data-generation.md) | デモデータ生成サービスの実装仕様 |
| [Agent Framework 開発計画](docs/dev-plan/README.md) | Hosted Agent の設計・実装・インフラ仕様一覧 |
| [シークレット管理](docs/dev-plan/10-secrets-management.md) | user-secrets / Key Vault / コミット禁止物 |
| [命名規約](docs/dev-plan/11-naming-conventions.md) | Azure / M365 リソース名・採用サフィックス |
| [News Portal デプロイ](docs/dev-plan/12-news-portal-deployment.md) | Static Website 公開 URL・デプロイ手順 |
| [Foundry ランタイム構成](docs/dev-plan/13-foundry-runtime-config.md) | File Search / Bing Grounding / Trigger Agent 構成 |
| [コンテナビルド](docs/dev-plan/14-container-build.md) | Dockerfile・ACR ビルド・smoke 確認 |
| [ACA デプロイ](docs/dev-plan/15-aca-deployment.md) | Container Apps デプロイ・RBAC・smoke 記録 |
| [Foundry Trigger 引継ぎ](docs/dev-plan/16-foundry-trigger-wiring.md) | 手動 enqueue E2E と Trigger Agent 自動化試行 |
| [シナリオ検証](docs/dev-plan/17-scenario-verification.md) | Phase 3 E2E 検証結果 |
| [観測性検証](docs/dev-plan/18-observability-check.md) | Phase 4 App Insights クエリ結果 |
