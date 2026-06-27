# nexus6-se-workshop

顧客 **Demo 用** の最小構成リポジトリです。想定ユースケース・シナリオと、それを実現するための最低限の Demo アプリケーション / 設計ドキュメントを含みます。

> 本リポジトリは Demo 想定の最低限構成です。本番運用相当の冗長化・スケジューラ・パイプライン自動化等は対象外とし、必要に応じ将来拡張ポイントとして記載しています。

## DevContainer

このリポジトリは **DevContainer** に対応しています。VS Code + Dev Containers 拡張（または GitHub Codespaces）で開くと、下記のツールがすべてセットアップされた状態で開発を開始できます。

| セットアップ内容 | 詳細 |
|---|---|
| .NET 10 SDK | ベースイメージに含まれる |
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

### Azure・クラウドリソース（必須）

| サービス | SKU / ティア | 用途 |
|---|---|---|
| **Azure AI Foundry** | Standard | LLM 推論（GPT-4o / gpt-4o-mini）・File Search・Grounding with Bing Search |
| **Microsoft Fabric** | F2 以上 | OneLake・Lakehouse（Bronze/Silver/Gold）・業務システムデータ格納 |
| **Azure Container Apps** | Consumption | .NET 10 Hosted Agent ホスティング |
| **ADLS Gen2** | Standard LRS | Skill.md ファイル格納・OneLake Shortcut 基盤 |
| **Microsoft Entra ID** | 既存テナント | Managed Identity（全サービス認証の統一基盤） |
| **Azure Key Vault** | Standard | Teams Workflows URL 等、外部サービスの API キーのみ管理 |
| **Microsoft Teams (Workflows)** | M365 既存ライセンス | Agent 4 の Adaptive Card 通知 |

### Azure・クラウドリソース（推奨）

| サービス | SKU / ティア | 用途 |
|---|---|---|
| **Azure Monitor / App Insights** | Pay-as-you-go | エージェント実行ログ・トークン使用量監視 |
| **Azure Container Registry** | Basic | コンテナイメージ管理 |

> **認証方針**: Azure リソースへの認証は Container Apps の **System Assigned Managed Identity** に統一。API キー・サービスプリンシパルシークレットは原則 Key Vault 管理とし、コードやリポジトリに含めない。

### デモデータ生成（DemoDataGenerator）追加依存

| 項目 | 内容 |
|---|---|
| **GitHub Copilot サブスクリプション** | Copilot SDK の実行に必要（または BYOK 設定） |
| **Fabric SQL Database × 16** | 業務システム単位のDB（common 1 + mobile 5 + ecommerce 5 + fintech 5） |
| 環境変数 `GITHUB_TOKEN` | `dotnet user-secrets` で管理・コミット禁止 |

## ディレクトリ構成

```
nexus6-se-workshop/
├── docs/
│   ├── scenario/          # シナリオ文書（為替・競合統合・日銀利上げ）
│   ├── usecase/           # 事業部別業務システム・データモデル定義
│   └── dev-plan/          # Hosted Agent 開発仕様（Agent / Foundry / Fabric / M365）
└── src/
    ├── news-portal/       # Demo 入力源：静的 HTML ニュースポータル（既存）
    ├── news-analysis-agent/   # ニュース分析エージェント本体（未実装）
    │   │                      # .NET 10 / Azure AI Foundry / Microsoft Agent Framework
    │   ├── src/           # Agent 1〜4・Orchestration・Tools・Models
    │   └── tests/
    └── DemoDataGenerator/ # Copilot SDK によるデモデータ生成サービス（未実装）
                           # EF Core → Fabric SQL Database（業務システム単位 16 DB）
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
