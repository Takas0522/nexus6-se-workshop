# Bicep 構築に向けたリソースレビュー

> **目的**: 現在 `rg-nexus6-swc` と `SEWorkShopC12` に分散しているリソースを **単一リソースグループに統合** するための Bicep テンプレートを設計する。  
> **リージョン**: Sweden Central  
> **ステータス**: レビュー中

---

## 1. 現行リソース一覧

### 1.1 rg-nexus6-swc （アプリケーション基盤）

| # | リソース名 | リソースタイプ | SKU / Tier | 備考 |
|---|-----------|--------------|-----------|------|
| 1 | `log-nexus6-swc` | Log Analytics Workspace | PerGB2018 | 監視基盤。Container Apps Env に接続 |
| 2 | `appi-nexus6-swc` | Application Insights | web | Log Analytics に接続 |
| 3 | `kv-nexus6-swc` | Key Vault | Standard (Family A) | RBAC 認可有効、ソフトデリート有効 |
| 4 | `crnexus6swc` | Container Registry | Basic | Container App からシステム Managed ID で pull |
| 5 | `cae-nexus6-swc` | Container Apps Environment | — | Log Analytics 接続済み、VNet 未接続 |
| 6 | `ca-nexus6-hosted-agent` | Container App | 0.5 vCPU / 1Gi | System Assigned MI、外部 Ingress (port 8080)、スケール 0–2 |
| 7 | `stnexus6skill1t2i` | Storage Account (V2) | Standard_LRS | スキルデータ用 |
| 8 | `stnexus6portal1t2i` | Storage Account (V2) | Standard_LRS | ポータル静的サイト用 |
| 9 | `SwedenCentralLinuxDynamicPlan` | App Service Plan | Y1 (Dynamic) | Azure Functions 用 |
| 10 | EventGrid System Topics (×2) | EventGrid SystemTopic | — | 各 Storage Account に紐づく自動生成リソース |
| 11 | `Application Insights Smart Detection` | Action Group | — | 自動生成 |

### 1.2 SEWorkShopC12 （AI / データ基盤）

| # | リソース名 | リソースタイプ | SKU / Tier | 備考 |
|---|-----------|--------------|-----------|------|
| 1 | `fd-PartnerIQ` | AI Services (Foundry) | S0 | カスタムサブドメイン: `fd-partneriq` |
| 2 | `fd-PartnerIQ/proj-PartnerIQ` | AI Services Project | — | Foundry プロジェクト |
| 3 | `iq-knowledge-source` | AI Search | Standard | ナレッジベース検索 |
| 4 | `fabricswedencu001` | Fabric Capacity | F4 | Microsoft Fabric |
| 5 | `testeeeeeee` | Power Platform Account | — | **テスト用 → Bicep 対象外** |

---

## 2. モデルデプロイメント（fd-PartnerIQ）

| デプロイ名 | モデル | バージョン | SKU | キャパシティ |
|-----------|--------|----------|-----|------------|
| `gpt-5.4` | gpt-5.4 | 2026-03-05 | GlobalStandard | 500K TPM |
| `text-embedding-3-large` | text-embedding-3-large | 1 | Standard | 120K TPM |
| `model-router` | model-router | 2025-11-18 | GlobalStandard | 500K TPM |
| `o4-mini` | o4-mini | 2025-04-16 | GlobalStandard | 500K TPM |

---

## 3. アプリケーション構成との対応

| アプリケーション | ソースパス | デプロイ先 | 主な依存リソース |
|----------------|-----------|----------|-----------------|
| Hosted Agent (news-analysis) | `src/news-analysis-agent/` | Container App (`ca-nexus6-hosted-agent`) | AI Foundry, AI Search, Storage, Key Vault, App Insights |
| News Portal (静的サイト) | `src/news-portal/` | Storage Account 静的 Web サイト (`stnexus6portal1t2i`) | — |
| News Trigger Function | `src/news-trigger-function/` | Azure Functions (Y1 Plan) | Storage Account, EventGrid |
| News Analysis WebApp | `src/news-analysis-webapp/` | ※未デプロイ or 別環境 | TBD |
| Demo Data Generator | `src/DemoDataGenerator/` | ローカル/CI ツール | — |

---

## 4. Container App 環境変数（＝外部依存の全体像）

```
Foundry__ProjectEndpoint          → AI Foundry (fd-PartnerIQ/proj-PartnerIQ)
Foundry__DefaultModelDeployment   → gpt-5.4 デプロイ
Foundry__NotificationModelDeployment → モデルデプロイ
Foundry__FileSearchVectorStoreId  → AI Foundry Vector Store
Foundry__GroundingBingConnectionId → Bing Grounding 接続
Foundry__AssistantsApiVersion     → Assistants API バージョン
Foundry__Assistant__*             → Assistant ID 群
Foundry__ApiVersion               → API バージョン
Foundry__MaxCompletionTokens      → トークン上限
Fabric__SqlEndpoint               → Fabric SQL エンドポイント
Fabric__Database                  → Fabric データベース名
Storage__Account                  → Storage Account 名
KeyVault__Uri                     → Key Vault URI
AzureMonitor__ConnectionString    → App Insights 接続文字列
Teams__Graph__*                   → Teams 通知用 ID 群
DevUi__EnableManualTrigger        → 開発 UI フラグ
APPLICATIONINSIGHTS_CONNECTION_STRING → App Insights (重複設定)
```

---

## 5. Bicep で構築するリソース（提案）

### 5.1 現行リソースの統合

以下をすべて **1 つのリソースグループ** にまとめた Bicep テンプレートとして構築する。

| カテゴリ | リソース | Bicep モジュール案 |
|---------|---------|-------------------|
| **監視** | Log Analytics Workspace | `modules/monitoring.bicep` |
| | Application Insights | `modules/monitoring.bicep` |
| **セキュリティ** | Key Vault | `modules/keyvault.bicep` |
| **コンテナ** | Container Registry | `modules/container-registry.bicep` |
| | Container Apps Environment | `modules/container-apps-env.bicep` |
| | Container App (Hosted Agent) | `modules/container-app.bicep` |
| **ストレージ** | Storage Account ×2 | `modules/storage.bicep` |
| **AI** | AI Services (Foundry) | `modules/ai-foundry.bicep` |
| | AI Services Project | `modules/ai-foundry.bicep` |
| | AI Search | `modules/ai-search.bicep` |
| **データ** | Fabric Capacity | `modules/fabric.bicep` |
| **コンピュート** | App Service Plan (Functions) | `modules/functions.bicep` |
| **イベント** | EventGrid System Topics | ※Storage 連動で自動生成。必要に応じて明示 |

### 5.2 追加推奨リソース（現在未構成）

| # | リソース | 理由 | 優先度 |
|---|---------|------|-------|
| 1 | **Azure Front Door / CDN** | 静的サイト (`news-portal`) の配信最適化とカスタムドメイン対応 | 中 |
| 2 | **VNet + Private Endpoints** | AI Services, Key Vault, Storage, ACR へのネットワーク分離。Container Apps Env を VNet 統合する | 高 |
| 3 | **User Assigned Managed Identity** | 複数リソース間で共有可能な ID。現在 System Assigned のみだが、再作成時の権限再設定が不要になる | 中 |
| 4 | **Azure Functions App** | `news-trigger-function` の Function App リソース本体が RG 内に見当たらない（Plan のみ存在）。Bicep で明示的に定義すべき | 高 |
| 5 | **Budget Alert** | コスト管理のためのアラート設定 | 低 |
| 6 | **Diagnostic Settings** | 各リソースの診断ログを Log Analytics に集約する設定 | 中 |
| 7 | **RBAC ロール割り当て** | Managed Identity → AI Services, Storage, Key Vault, ACR への権限付与を Bicep で明示管理 | 高 |

---

## 6. Bicep テンプレート構成案

```
infra/
├── main.bicep                    # オーケストレーション（モジュール呼び出し）
├── main.bicepparam               # パラメータファイル
├── modules/
│   ├── monitoring.bicep          # Log Analytics + App Insights
│   ├── keyvault.bicep            # Key Vault
│   ├── container-registry.bicep  # ACR
│   ├── container-apps-env.bicep  # Container Apps Environment
│   ├── container-app.bicep       # Container App (Hosted Agent)
│   ├── storage.bicep             # Storage Account ×2
│   ├── ai-foundry.bicep          # AI Services + Project + Model Deployments
│   ├── ai-search.bicep           # AI Search
│   ├── fabric.bicep              # Fabric Capacity
│   ├── functions.bicep           # App Service Plan + Function App
│   └── rbac.bicep                # RBAC ロール割り当て
```

---

## 7. 主な設計判断ポイント

| # | 判断項目 | 現行 | 推奨 |
|---|---------|------|------|
| 1 | リソースグループ構成 | 2 RG 分散 | 1 RG 統合 |
| 2 | ネットワーク分離 | なし（パブリック） | VNet + Private Endpoint（段階的導入可） |
| 3 | Managed Identity | System Assigned のみ | User Assigned を追加検討 |
| 4 | Container Registry SKU | Basic | Basic のまま（小規模であれば十分） |
| 5 | AI Search SKU | Standard | Standard（現行踏襲） |
| 6 | Fabric Capacity | F4 | F4（現行踏襲。Bicep で管理可能だが手動管理も可） |
| 7 | Function App | Plan のみ存在 | Function App リソースを明示定義 |

---

## 8. 次のステップ

1. **本ドキュメントのレビュー** → 不要リソースの除外・追加リソースの確定
2. **パラメータ設計** → 環境別 (dev/staging/prod) の分離方針を決定
3. **Bicep モジュール実装** → モジュール単位で構築・テスト
4. **デプロイ検証** → what-if による差分確認後、実環境へ適用
