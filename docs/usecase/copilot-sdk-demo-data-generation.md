# GitHub Copilot SDK を使用したデモデータ生成仕様

## 目的と背景

本リポジトリは **デモ目的のアプリケーション** を想定しており、各業種業務システムのデモデータ（顧客・契約・取引等）をGitリポジトリで管理することは以下の理由から望ましくない。

- リアルに見えるデータを大量に手作りするコストが高い
- シナリオの変化（為替・競合統合・日銀政策）に合わせてデータを更新し続けるメンテナンスコストが高い
- 個人情報に見える模擬データをコミット履歴に残すことはリスクになり得る

そこで **GitHub Copilot SDK** を用い、シナリオコンテキストをプロンプトとして与えることで、各業務システムのデモデータをオンデマンドに生成するアーキテクチャを採用する。

---

## 参照情報

| 資料 | URL |
|---|---|
| GitHub Copilot SDK | https://github.com/github/copilot-sdk |
| Getting Started | https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md |
| .NET SDK | https://github.com/github/copilot-sdk/tree/main/dotnet |
| 認証ガイド | https://github.com/github/copilot-sdk/blob/main/docs/auth/README.md |
| BYOK ガイド | https://github.com/github/copilot-sdk/blob/main/docs/auth/byok.md |

---

## システム構成

```
┌─────────────────────────────────────────────────────────┐
│  デモデータ生成サービス（DemoDataGenerator）              │
│                                                           │
│  ┌─────────────────┐    JSON-RPC     ┌─────────────┐    │
│  │  ASP.NET Core   │ ◄────────────► │ Copilot CLI  │    │
│  │  Web API (C#)   │                 │  (bundled)   │    │
│  └────────┬────────┘                 └─────────────┘    │
│           │ Custom Tools (EF Core 書き込み)               │
│           ▼                                               │
│  ┌─────────────────────────────────────┐                 │
│  │  Fabric SQL Database (T-SQL)        │                 │
│  │  mobile / ecommerce / fintech       │                 │
│  └────────────────┬────────────────────┘                 │
└───────────────────┼─────────────────────────────────────┘
                    │（Fabric が自動的に物理データを同期）
                    ▼
         ┌──────────────────┐
         │   OneLake        │  Delta/Parquet 形式で保存
         │  （分析・BI用）  │  Power BI / Spark / Analytics
         └──────────────────┘
          ▲ HTTP（シナリオ切替リクエスト）
          │
┌─────────┴─────────┐
│  各業種アプリ     │  モバイル / EC / Fintech
│  （デモ初期化時） │
└───────────────────┘
```

生成サービスは各業種アプリの **デモ初期化エンドポイント** から呼び出される。シナリオ番号（1:円安 / 2:ONE PASS / 3:日銀利上げ）を受け取り、そのシナリオに整合したデモデータを生成して Fabric SQL Database に書き込む。Fabric が物理データを自動的に OneLake（Delta/Parquet）へ同期するため、Power BI や Spark での分析もそのまま利用可能。

> **Note: EF Core と OneLake の対応状況**
> EF Core から OneLake（Lakehouse / Delta テーブル）への **直接書き込みは非対応**。
> EF Core が書き込めるのは T-SQL エンドポイントを持つ **Fabric SQL Database** のみ。
> Fabric SQL Database の物理ストレージは内部的に OneLake に保存されるため、
> EF Core で書き込みつつ OneLake の分析機能（Spark / Power BI）を活用できる。

---

## 前提・認証

| 項目 | 内容 |
|---|---|
| ランタイム | .NET 8 以上 |
| SDK | `GitHub.Copilot.SDK`（NuGet） |
| CLI自動バンドル | .NET SDKはCLIを自動同梱（別途インストール不要） |
| データストア | **Microsoft Fabric SQL Database**（T-SQLエンドポイント） |
| EF Core プロバイダー | `Microsoft.EntityFrameworkCore.SqlServer` |
| 認証 | 環境変数 `GITHUB_TOKEN`（Copilotサブスクリプション必須） |
| 代替認証（BYOK） | `Provider` プロパティで `OpenAI` 等のAPIキーを指定してGitHub認証なしで利用可 |

```bash
dotnet add package GitHub.Copilot.SDK
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

`appsettings.Development.json`（リポジトリ非管理）:
```json
{
  "ConnectionStrings": {
    "CommonCustomerIdentityDb": "Server=<workspace>.database.fabric.microsoft.com;Database=common-customer-identity;Authentication=Active Directory Default;",

    "MobileCustomerMgmtDb":  "Server=<workspace>.database.fabric.microsoft.com;Database=mobile-customer-mgmt;Authentication=Active Directory Default;",
    "MobileContractMgmtDb":  "Server=<workspace>.database.fabric.microsoft.com;Database=mobile-contract-mgmt;Authentication=Active Directory Default;",
    "MobileBillingDb":       "Server=<workspace>.database.fabric.microsoft.com;Database=mobile-billing;Authentication=Active Directory Default;",
    "MobileInventoryDb":     "Server=<workspace>.database.fabric.microsoft.com;Database=mobile-inventory;Authentication=Active Directory Default;",
    "MobileCrmDb":           "Server=<workspace>.database.fabric.microsoft.com;Database=mobile-crm;Authentication=Active Directory Default;",

    "EcProductMgmtDb":       "Server=<workspace>.database.fabric.microsoft.com;Database=ec-product-mgmt;Authentication=Active Directory Default;",
    "EcOrderMgmtDb":         "Server=<workspace>.database.fabric.microsoft.com;Database=ec-order-mgmt;Authentication=Active Directory Default;",
    "EcInventoryDb":         "Server=<workspace>.database.fabric.microsoft.com;Database=ec-inventory;Authentication=Active Directory Default;",
    "EcMemberPointDb":       "Server=<workspace>.database.fabric.microsoft.com;Database=ec-member-point;Authentication=Active Directory Default;",
    "EcMarketingDb":         "Server=<workspace>.database.fabric.microsoft.com;Database=ec-marketing;Authentication=Active Directory Default;",

    "FintechAccountMgmtDb":  "Server=<workspace>.database.fabric.microsoft.com;Database=fintech-account-mgmt;Authentication=Active Directory Default;",
    "FintechCardMgmtDb":     "Server=<workspace>.database.fabric.microsoft.com;Database=fintech-card-mgmt;Authentication=Active Directory Default;",
    "FintechPaymentGwDb":    "Server=<workspace>.database.fabric.microsoft.com;Database=fintech-payment-gateway;Authentication=Active Directory Default;",
    "FintechTradingDb":      "Server=<workspace>.database.fabric.microsoft.com;Database=fintech-trading;Authentication=Active Directory Default;",
    "FintechCreditDb":       "Server=<workspace>.database.fabric.microsoft.com;Database=fintech-credit;Authentication=Active Directory Default;"
  },
  "GitHub": {
    "Token": "ghp_xxxxxxxxxxxx"
  }
}
```

> **接続先の注意点**: Fabric の SQL Analytics エンドポイント（Warehouse / Lakehouse の読み取り専用エンドポイント）ではなく、**Fabric SQL Database** のエンドポイントに接続すること。Analytics エンドポイントへの EF Core 書き込みは失敗する。

---

## SDK コアパターン

```csharp
using GitHub.Copilot;

await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    OnPermissionRequest = PermissionHandler.ApproveAll,
    SystemMessage = SYSTEM_PROMPT,   // 業種・シナリオのコンテキスト
    Tools = CUSTOM_TOOLS,            // DB書き込みツール群
});

// SessionIdleEvent を使って完了を待機
var done = new TaskCompletionSource();
session.On<SessionEvent>(evt =>
{
    if (evt is AssistantMessageEvent msg)
        Console.WriteLine(msg.Data.Content);
    else if (evt is SessionIdleEvent)
        done.SetResult();
});

await session.SendAsync(new MessageOptions { Prompt = GENERATION_PROMPT });
await done.Task;
```

---

## カスタムツール定義方針

Copilot SDK の **Custom Tools** 機能を使い、Copilot が生成したデータを直接DBに挿入させる。ツールは「生成対象のデータモデル（テーブル）ごと」に1つ定義する。

下記は各業務システムで定義するツールの一覧と、C#実装例を示す。

### ツール一覧

#### 共通：顧客統合ID基盤（`common-customer-identity`）
| ツール名 | 対象データモデル |
|---|---|
| `insert_unified_customer` | 統合顧客マスタ |
| `insert_domain_id_mapping` | ドメイン別IDマッピング |
| `insert_customer_segment_master` | 顧客セグメントマスタ |
| `insert_customer_segment_assignment` | 顧客セグメント割り当て |

#### モバイル通信
| ツール名 | 対象データモデル | 業務システム |
|---|---|---|
| `insert_mobile_customer` | 顧客マスタ | 顧客管理システム |
| `insert_mobile_contract` | 契約マスタ | 契約管理システム |
| `insert_mobile_option_master` | オプション・追加サービスマスタ | 契約管理システム |
| `insert_mobile_customer_option` | 顧客オプション契約 | 契約管理システム |
| `insert_mobile_mnp` | MNP（番号ポータビリティ）履歴 | 契約管理システム |
| `insert_mobile_usage` | 利用・請求データ | 課金・請求システム |
| `insert_mobile_plan_rule` | プラン・料金ルール | 課金・請求システム |
| `insert_mobile_installment` | 端末分割払い明細 | 課金・請求システム |
| `insert_mobile_cost_item` | 端末・設備コストデータ | 在庫・端末管理システム |
| `insert_mobile_device_sku` | 端末SKUマスタ | 在庫・端末管理システム |
| `insert_mobile_base_station` | ネットワーク基地局マスタ | 在庫・端末管理システム |
| `insert_mobile_ticket` | 問い合わせ・解約データ | コールセンター/CRMシステム |
| `insert_mobile_campaign_action` | 施策配信・反応履歴 | コールセンター/CRMシステム |

#### Eコマース
| ツール名 | 対象データモデル | 業務システム |
|---|---|---|
| `insert_ec_category` | カテゴリマスタ | 商品管理システム |
| `insert_ec_seller` | 出店者・サプライヤーマスタ | 商品管理システム |
| `insert_ec_product` | 商品マスタ | 商品管理システム |
| `insert_ec_price_rule` | 価格ルール | 商品管理システム |
| `insert_ec_order` | 受注データ | 受注管理システム |
| `insert_ec_return` | 返品・キャンセルデータ | 受注管理システム |
| `insert_ec_inventory` | 在庫データ | 在庫管理システム |
| `insert_ec_shipping_route` | 配送・物流ルート | 在庫管理システム |
| `insert_ec_member` | 会員マスタ | 会員・ポイント管理システム |
| `insert_ec_point_event` | ポイント残高・付与・利用履歴 | 会員・ポイント管理システム |
| `insert_ec_campaign` | ポイント・キャンペーンデータ | マーケティング/キャンペーン管理システム |
| `insert_ec_member_event` | 会員行動データ | マーケティング/キャンペーン管理システム |
| `insert_ec_campaign_reaction` | キャンペーン反応履歴 | マーケティング/キャンペーン管理システム |

#### Fintech
| ツール名 | 対象データモデル | 業務システム |
|---|---|---|
| `insert_fintech_account` | 顧客口座マスタ | 口座管理システム |
| `insert_fintech_kyc` | KYC・本人確認記録 | 口座管理システム |
| `insert_fintech_account_link` | 顧客・口座紐付け履歴 | 口座管理システム |
| `insert_fintech_card_transaction` | カード・決済データ | カード管理システム |
| `insert_fintech_alert` | 取引アラート・通知履歴 | カード管理システム / 決済ゲートウェイ |
| `insert_fintech_merchant` | 加盟店マスタ | 決済ゲートウェイ |
| `insert_fintech_position` | FX・証券・暗号資産データ | FX/証券/暗号資産取引システム |
| `insert_fintech_fx_rate` | 為替レートスナップショット | FX/証券/暗号資産取引システム |
| `insert_fintech_product_rule` | 金利・商品ルール | FX/証券/暗号資産取引システム |
| `insert_fintech_credit_review` | 与信・審査データ | 与信・審査システム |
| `insert_fintech_loan` | ローン・リボ払い残高データ | 与信・審査システム |
| `insert_fintech_risk_event` | リスクイベント履歴 | 与信・審査システム |
| `insert_fintech_revenue` | 収益・リスクデータ | 与信・審査システム |

### C# 実装例（モバイル：顧客マスタ + MNP履歴）

```csharp
using GitHub.Copilot;
using Microsoft.Extensions.AI;

// insert_mobile_customer: 顧客マスタ（顧客管理システム）
var insertMobileCustomer = AIFunctionFactory.Create(
    async (
        string customerId,    // 顧客ID（UUID）
        string customerType,  // "個人" | "法人"
        string region,        // 居住地域（例: 関東、関西）
        string ageBand,       // 年齢帯（例: "20s", "30s", "40s"）
        string segment        // セグメント（例: heavy_user / standard / light / churn_risk）
    ) =>
    {
        await db.MobileCustomers.AddAsync(new MobileCustomer
        {
            CustomerId   = customerId,
            CustomerType = customerType,
            Region       = region,
            AgeBand      = ageBand,
            Segment      = segment,
        });
        await db.SaveChangesAsync();
        return new { success = true };
    },
    name: "insert_mobile_customer",
    description: "モバイル通信の顧客マスタ（顧客管理システム）に1件挿入します。"
);

// insert_mobile_mnp: MNP履歴（契約管理システム）
var insertMobileMnp = AIFunctionFactory.Create(
    async (
        string mnpId,         // MNP転出入ID（UUID）
        string customerId,    // 顧客ID
        string mnpType,       // "転入" | "転出"
        string fromCarrier,   // 転出元キャリア
        string toCarrier,     // 転入先キャリア
        string executedAt,    // 実施日時 ISO 8601
        string triggerReason  // 転出理由（"競合キャンペーン" | "料金" | "品質"）
    ) =>
    {
        await db.MobileMnpHistory.AddAsync(new MobileMnp
        {
            MnpId         = mnpId,
            CustomerId    = customerId,
            MnpType       = mnpType,
            FromCarrier   = fromCarrier,
            ToCarrier     = toCarrier,
            ExecutedAt    = DateTime.Parse(executedAt),
            TriggerReason = triggerReason,
        });
        await db.SaveChangesAsync();
        return new { success = true };
    },
    name: "insert_mobile_mnp",
    description: "MNP（番号ポータビリティ）履歴（契約管理システム）に1件挿入します。"
);

// SessionConfig の Tools に渡す
var session = await client.CreateSessionAsync(new SessionConfig
{
    Tools = [insertMobileCustomer, insertMobileMnp, /* 他ツール */ ],
});
```

---

## 業種別システムメッセージ（System Prompt）

### 共通部（全業種）

```
あなたは日本のデジタルサービス企業のデモ用データ生成AIです。
以下のシナリオが発生したことを前提に、リアリティのある業務データを生成してください。

# アクティブシナリオ
{SCENARIO_DESCRIPTION}

# データ生成ルール
- 全データはフィクションです。実在する個人・企業を模倣しないでください。
- 日本の慣習（住所・氏名・電話番号フォーマット）に準拠してください。
- 各フィールドは業務として整合性が取れるように生成してください。
- IDはUUID v4形式で生成してください。
- 日付は ISO 8601形式（YYYY-MM-DD）で生成してください。
```

---

### シナリオ別プロンプト変数 `{SCENARIO_DESCRIPTION}`

#### シナリオ1：為替ショック（円安）

```
【シナリオ1: 為替ショック】
現在の外国為替レートは USD/JPY = 158.42（前日比 +2.31）。
地政学リスクにより急速な円安が進行中。

影響:
- モバイル: 輸入端末コストが前年比+18%。秋モデルの端末価格を1〜1.5万円値上げ予定。
- EC: 越境EC仕入原価が+15〜20%。一部カテゴリのポイント還元率を引き下げ検討中。
- Fintech: FX取引量が急増。海外決済カードの与信リスクが上昇。
```

#### シナリオ2：競合統合（ONE PASS）

```
【シナリオ2: 競合経済圏統合 "ONE PASS"】
大手3キャリア×EC×QR決済が「ONE PASS」を発表。
回線契約者はEC購入で最大10倍ポイント、QR決済で最大5倍還元。

影響:
- モバイル: MNP転出リスクが高い顧客層が増加。乗換・離反予兆スコアが上昇。
- EC: ポイント還元競争が激化。自社ポイント価値が相対的に低下。
- Fintech: QR決済加盟店の手数料競争激化。カード利用率の流出リスク。
```

#### シナリオ3：日銀利上げ

```
【シナリオ3: 日銀金融政策転換（利上げ）】
日銀が政策金利を0.25% → 0.5% に引き上げ（17年ぶり高水準）。

影響:
- モバイル: 端末割賦債権の調達コスト上昇。設備投資計画の見直し検討。
- EC: 消費者マインドが悪化。高単価カテゴリ（家電・家具）の需要が減速傾向。
- Fintech: リボ払い延滞リスクが上昇。変動型住宅ローン契約者の可処分所得圧迫。
```

---

## 業種別 生成プロンプトと対象データモデル

各生成プロンプトのツール名は「カスタムツール定義方針」に記載のツール名と一致させること。

---

### 共通：顧客統合ID基盤（`common-customer-identity`）

| 生成対象 | ツール名 | 件数目安 |
|---|---|---|
| 統合顧客マスタ | `insert_unified_customer` | 500件 |
| ドメイン別IDマッピング（mobile/ecommerce/fintech 各1件） | `insert_domain_id_mapping` | 1,500件 |
| 顧客セグメントマスタ | `insert_customer_segment_master` | 20件 |
| 顧客セグメント割り当て | `insert_customer_segment_assignment` | 1,000件 |

**プロンプト例**:
```
統合顧客マスタを500件生成してください。
full_name は日本人名、birth_date は1960-01-01〜2005-12-31の範囲、
kyc_status は "verified" を80%、"pending" を15%、"rejected" を5%の割合で設定してください。
insert_unified_customer ツールを500回呼び出して挿入してください。

続いて、各顧客について mobile / ecommerce / fintech の3ドメイン分の
ドメイン別IDマッピングを insert_domain_id_mapping ツールで挿入してください。
```

---

### モバイル通信

#### 顧客管理システム（`mobile-customer-mgmt`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 顧客マスタ | `insert_mobile_customer` | customer_id / customer_type / region / age_band / segment | 500件 |

**プロンプト例（シナリオ2: ONE PASS）**:
```
モバイル通信の顧客マスタを500件生成してください。
ONE PASSシナリオを反映し、segment="churn_risk" を全体の25%程度含めてください。
age_band は "20s"〜"60s" を均等に分散させ、region は都道府県で設定してください。
insert_mobile_customer ツールを500回呼び出して挿入してください。
```

#### 契約管理システム（`mobile-contract-mgmt`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 契約マスタ | `insert_mobile_contract` | contract_id / customer_id / plan_id / device_type / update_month / subsidy_amount | 500件 |
| オプション・追加サービスマスタ | `insert_mobile_option_master` | option_id / option_name / option_fee / option_category / valid_from / valid_to | 30件 |
| 顧客オプション契約 | `insert_mobile_customer_option` | customer_option_id / customer_id / option_id / subscribed_at / cancelled_at | 800件 |
| MNP履歴 | `insert_mobile_mnp` | mnp_id / customer_id / mnp_type / from_carrier / to_carrier / executed_at / trigger_reason | 150件 |

**プロンプト例（シナリオ2: ONE PASS）**:
```
MNP履歴を150件生成してください。
ONE PASSシナリオを反映し、mnp_type="転出" が70%、trigger_reason="競合キャンペーン" が
転出全体の60%を占めるよう設定してください。
insert_mobile_mnp ツールを150回呼び出して挿入してください。
```

#### 課金・請求システム（`mobile-billing`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 利用・請求データ | `insert_mobile_usage` | usage_id / contract_id / usage_date / voice_usage / data_usage / monthly_charge | 6,000件（500契約×12ヶ月） |
| プラン・料金ルール | `insert_mobile_plan_rule` | plan_rule_id / plan_id / base_fee / overage_rule / device_discount_rule / valid_from / valid_to | 10件 |
| 端末分割払い明細 | `insert_mobile_installment` | installment_id / contract_id / device_sku_id / total_amount / monthly_payment / remaining_months / interest_rate / start_date | 300件 |

**プロンプト例（シナリオ3: 日銀利上げ）**:
```
端末分割払い明細を300件生成してください。
日銀利上げシナリオを反映し、interest_rate を 0.035〜0.048 の範囲で設定してください。
remaining_months が12ヶ月以上の件数を70%以上とし、
monthly_payment × remaining_months ≒ total_amount の整合性を保ってください。
insert_mobile_installment ツールを300回呼び出して挿入してください。
```

#### 在庫・端末管理システム（`mobile-inventory`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 端末・設備コストデータ | `insert_mobile_cost_item` | cost_item_id / cost_type / currency / unit_cost / procurement_date / vendor_region | 200件 |
| 端末SKUマスタ | `insert_mobile_device_sku` | device_sku_id / device_name / supplier_region / import_currency / standard_cost / sales_price | 50件 |
| ネットワーク基地局マスタ | `insert_mobile_base_station` | station_id / region / equipment_type / vendor_name / vendor_country / contract_currency / install_date / maintenance_cost | 100件 |

**プロンプト例（シナリオ1: 円安）**:
```
端末SKUマスタを50件生成してください。
円安シナリオを反映し、import_currency="USD" の端末については
standard_cost を「円安前の原価 × 1.18」に設定してください。
supplier_region は "中国" / "韓国" / "台湾" / "国内" を含めてください。
insert_mobile_device_sku ツールを50回呼び出して挿入してください。
```

#### コールセンター/CRMシステム（`mobile-crm`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 問い合わせ・解約データ | `insert_mobile_ticket` | ticket_id / customer_id / contact_reason / created_at / resolved_at / cancel_flag | 300件 |
| 施策配信・反応履歴 | `insert_mobile_campaign_action` | action_id / customer_id / action_type / sent_at / response_type / response_at | 1,000件 |

---

### Eコマース

#### 商品管理システム（`ec-product-mgmt`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| カテゴリマスタ | `insert_ec_category` | category_id / category_name / parent_category_id / import_ratio / demand_elasticity | 30件 |
| 出店者・サプライヤーマスタ | `insert_ec_seller` | seller_id / seller_name / seller_country / settlement_currency / cross_border_flag / contract_start_date | 50件 |
| 商品マスタ | `insert_ec_product` | sku / product_name / category / procurement_currency / cost_price / selling_price | 300件 |
| 価格ルール | `insert_ec_price_rule` | price_rule_id / sku / min_price / max_price / markdown_rule / valid_from / valid_to | 300件 |

**プロンプト例（シナリオ3: 日銀利上げ）**:
```
カテゴリマスタの家電・家具カテゴリについて、
demand_elasticity を -1.5〜-2.0 の範囲で設定してください。
その後、商品マスタを300件生成し、selling_price が3万円以上の商品については
markdown_rule に "要検討" を付与した価格ルールも合わせて作成してください。
insert_ec_category, insert_ec_product, insert_ec_price_rule ツールを使用してください。
```

#### 受注管理システム（`ec-order-mgmt`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 受注データ | `insert_ec_order` | order_id / member_id / sku / order_date / quantity / order_amount | 2,000件 |
| 返品・キャンセルデータ | `insert_ec_return` | return_id / order_id / member_id / sku / return_reason / return_amount / returned_at | 200件 |

#### 在庫管理システム（`ec-inventory`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 在庫データ | `insert_ec_inventory` | inventory_id / sku / warehouse_id / stock_qty / arrival_date / import_currency | 1,000件 |
| 配送・物流ルート | `insert_ec_shipping_route` | shipping_route_id / warehouse_id / destination_region / lead_time_days / shipping_cost | 50件 |

#### 会員・ポイント管理システム（`ec-member-point`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 会員マスタ | `insert_ec_member` | member_id / member_name / email / rank / registered_at / region / age_band | 500件 |
| ポイント残高・付与・利用履歴 | `insert_ec_point_event` | point_event_id / member_id / event_type / point_amount / balance_after / related_order_id / event_at / expiry_at | 3,000件 |

**プロンプト例（シナリオ2: ONE PASS）**:
```
会員マスタを500件生成してください。
ONE PASSシナリオを反映し、rank="ゴールド"以上の会員（他社サービス高利用者）を
全体の30%程度含めてください。
続いて、各会員のポイント残高履歴を平均6件生成し、
event_type="失効" を全履歴の10%程度含めてください。
insert_ec_member, insert_ec_point_event ツールを使用してください。
```

#### マーケティング/キャンペーン管理システム（`ec-marketing`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| ポイント・キャンペーンデータ | `insert_ec_campaign` | campaign_id / member_id / point_rate / campaign_budget / campaign_start_date / campaign_end_date | 20件 |
| 会員行動データ | `insert_ec_member_event` | event_id / member_id / event_type / event_time / device_type | 5,000件 |
| キャンペーン反応履歴 | `insert_ec_campaign_reaction` | reaction_id / campaign_id / member_id / reaction_type / reaction_time | 1,000件 |

---

### Fintech

#### 口座管理システム（`fintech-account-mgmt`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 顧客口座マスタ | `insert_fintech_account` | user_id / account_id / account_type / opened_at / balance | 500件 |
| KYC・本人確認記録 | `insert_fintech_kyc` | kyc_id / user_id / kyc_status / id_type / verified_at / expiry_at / review_result | 500件 |
| 顧客・口座紐付け履歴 | `insert_fintech_account_link` | link_id / user_id / external_account_ref / link_status / linked_at | 300件 |

#### カード管理システム（`fintech-card-mgmt`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| カード・決済データ | `insert_fintech_card_transaction` | card_id / user_id / transaction_id / transaction_date / amount / overseas_flag | 3,000件 |
| 取引アラート・通知履歴 | `insert_fintech_alert` | alert_id / user_id / alert_type / alert_level / triggered_at / notified_at / resolved_at | 200件 |

**プロンプト例（シナリオ1: 円安）**:
```
カード・決済データを3,000件生成してください。
円安シナリオを反映し、overseas_flag=true の取引を全体の20%含め、
海外取引の amount は国内平均の1.3倍以上に設定してください。
insert_fintech_card_transaction ツールを3,000回呼び出して挿入してください。
```

#### 決済ゲートウェイ（`fintech-payment-gateway`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 加盟店マスタ | `insert_fintech_merchant` | merchant_id / merchant_name / merchant_category / settlement_currency / fee_rate / overseas_flag / contracted_at | 200件 |

**プロンプト例（シナリオ2: ONE PASS）**:
```
加盟店マスタを200件生成してください。
ONE PASSシナリオを反映し、QR決済対応業種（飲食・小売・コンビニ）の
fee_rate を 0.5%〜1.0% の競争的水準に設定してください。
insert_fintech_merchant ツールを200回呼び出して挿入してください。
```

#### FX/証券/暗号資産取引システム（`fintech-trading`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| FX・証券・暗号資産データ | `insert_fintech_position` | position_id / user_id / product_type / position_amount / pnl_amount / market_currency | 1,000件 |
| 為替レートスナップショット | `insert_fintech_fx_rate` | rate_snapshot_id / base_currency / quote_currency / mid_rate / bid_rate / ask_rate / captured_at / source | 30日分 |
| 金利・商品ルール | `insert_fintech_product_rule` | product_rule_id / product_type / interest_rate / fee_rate / leverage_limit / valid_from / valid_to | 20件 |

**プロンプト例（シナリオ1: 円安）**:
```
為替レートスナップショットを過去30日分生成してください。
base_currency="USD", quote_currency="JPY" で、
最終日（今日）の mid_rate が 158.42 になるよう、
前15日間で 145〜152 円台から急騰するトレンドを描いてください。
bid_rate = mid_rate - 0.05、ask_rate = mid_rate + 0.05 で設定してください。
insert_fintech_fx_rate ツールを30回呼び出して挿入してください。
```

#### 与信・審査システム（`fintech-credit`）

| 生成対象 | ツール名 | フィールド | 件数目安 |
|---|---|---|---|
| 与信・審査データ | `insert_fintech_credit_review` | review_id / user_id / credit_score / approval_status / review_reason / reviewed_at | 500件 |
| ローン・リボ払い残高データ | `insert_fintech_loan` | loan_id / user_id / loan_type / principal_balance / interest_rate / monthly_payment / maturity_date / overdue_flag | 300件 |
| リスクイベント履歴 | `insert_fintech_risk_event` | risk_event_id / user_id / risk_type / risk_score / detected_at | 200件 |
| 収益・リスクデータ | `insert_fintech_revenue` | revenue_id / business_line / fee_revenue / interest_revenue / fx_revenue / risk_loss | 12ヶ月分 |

**プロンプト例（シナリオ3: 日銀利上げ）**:
```
ローン・リボ払い残高データを300件生成してください。
日銀利上げシナリオを反映し、loan_type="変動型住宅ローン" の
interest_rate を 1.5〜2.2% の範囲で設定し、
overdue_flag=true の割合を 8% 程度含めてください。
insert_fintech_loan ツールを300回呼び出して挿入してください。
```

---

## API インターフェース仕様

### エンドポイント

```
POST /api/demo/generate
```

### リクエスト

```json
{
  "scenario": 1,
  "domain": "mobile",
  "reset": true,
  "options": {
    "customerCount": 500,
    "seed": 42
  }
}
```

| パラメータ | 型 | 説明 |
|---|---|---|
| `scenario` | `1 \| 2 \| 3` | シナリオ番号（1:円安 / 2:ONE PASS / 3:日銀利上げ） |
| `domain` | `"common" \| "mobile" \| "ecommerce" \| "fintech" \| "all"` | 対象業種（`common`=顧客統合ID基盤のみ） |
| `reset` | `boolean` | 既存デモデータを削除してから生成するか |
| `options.customerCount` | `number` | 生成する顧客件数（省略時はデフォルト500） |
| `options.seed` | `number` | 乱数シード（省略時はランダム） |

### レスポンス

```json
{
  "jobId": "gen_20260627_001",
  "status": "completed",
  "scenario": 1,
  "domain": "mobile",
  "summary": {
    "mobileCustomers": 500,
    "mobileContracts": 500,
    "mobileOptions": 30,
    "mobileCustomerOptions": 800,
    "mobileMnpHistory": 150,
    "mobileUsage": 6000,
    "mobilePlanRules": 10,
    "mobileInstallments": 300,
    "mobileCostItems": 200,
    "mobileDeviceSkus": 50,
    "mobileBaseStations": 100,
    "mobileTickets": 300,
    "mobileCampaignActions": 1000
  },
  "durationMs": 186420
}
```

---

## 実装コード例（.NET / C#）

```csharp
// Generators/Mobile/MobileCustomerMgmtGenerator.cs
// 顧客管理システム（mobile-customer-mgmt）用ジェネレーター
using GitHub.Copilot;
using Microsoft.Extensions.AI;

public class MobileCustomerMgmtGenerator(
    MobileCustomerMgmtDbContext db,
    ILogger<MobileCustomerMgmtGenerator> logger)
{
    public async Task GenerateAsync(int scenario, GenerateOptions options)
    {
        await using var client = new CopilotClient();
        await client.StartAsync();

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-4.1",
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = SystemPrompts.Build(scenario, "mobile"),
            Tools = BuildTools(),
        });

        var done = new TaskCompletionSource();
        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    logger.LogInformation("Copilot: {Content}", msg.Data.Content);
                    break;
                case SessionErrorEvent err:
                    done.SetException(new Exception(err.Data.Message));
                    break;
                case SessionIdleEvent:
                    done.SetResult();
                    break;
            }
        });

        await session.SendAsync(new MessageOptions
        {
            Prompt = $"""
                モバイル通信の顧客マスタを{options.CustomerCount}件生成してください。
                customer_type は "個人" を80%、"法人" を20%の割合で設定してください。
                region は日本の都道府県で設定してください。
                age_band は "20s"〜"60s" を分散させてください。
                segment は "heavy_user" / "standard" / "light" / "churn_risk" のいずれかで設定してください。
                insert_mobile_customer ツールを{options.CustomerCount}回呼び出して挿入してください。
                """,
        });

        await done.Task.WaitAsync(TimeSpan.FromMinutes(10));
    }

    private IList<AIFunction> BuildTools() =>
    [
        // 顧客マスタ（mobile-telecom-system-data-model.md 参照）
        AIFunctionFactory.Create(
            async (
                string customerId,   // 顧客ID（UUID）
                string customerType, // "個人" | "法人"
                string region,       // 居住地域（都道府県）
                string ageBand,      // "20s" | "30s" | "40s" | "50s" | "60s"
                string segment       // "heavy_user" | "standard" | "light" | "churn_risk"
            ) =>
            {
                await db.Customers.AddAsync(new MobileCustomer
                {
                    CustomerId   = customerId,
                    CustomerType = customerType,
                    Region       = region,
                    AgeBand      = ageBand,
                    Segment      = segment,
                });
                await db.SaveChangesAsync();
                return new { success = true };
            },
            name: "insert_mobile_customer",
            description: "モバイル通信の顧客マスタ（顧客管理システム）に1件挿入します。"),
    ];
}
```

```csharp
// Generators/Mobile/MobileContractMgmtGenerator.cs
// 契約管理システム（mobile-contract-mgmt）用ジェネレーター
public class MobileContractMgmtGenerator(
    MobileContractMgmtDbContext db,
    ILogger<MobileContractMgmtGenerator> logger)
{
    private IList<AIFunction> BuildTools() =>
    [
        // 契約マスタ
        AIFunctionFactory.Create(
            async (string contractId, string customerId, string planId,
                   string deviceType, string updateMonth, decimal subsidyAmount) =>
            {
                await db.Contracts.AddAsync(new MobileContract
                {
                    ContractId    = contractId,
                    CustomerId    = customerId,
                    PlanId        = planId,
                    DeviceType    = deviceType,
                    UpdateMonth   = updateMonth,
                    SubsidyAmount = subsidyAmount,
                });
                await db.SaveChangesAsync();
                return new { success = true };
            },
            name: "insert_mobile_contract",
            description: "契約マスタ（契約管理システム）に1件挿入します。"),

        // MNP履歴
        AIFunctionFactory.Create(
            async (string mnpId, string customerId, string mnpType,
                   string fromCarrier, string toCarrier,
                   string executedAt, string triggerReason) =>
            {
                await db.MnpHistory.AddAsync(new MobileMnp
                {
                    MnpId         = mnpId,
                    CustomerId    = customerId,
                    MnpType       = mnpType,       // "転入" | "転出"
                    FromCarrier   = fromCarrier,
                    ToCarrier     = toCarrier,
                    ExecutedAt    = DateTime.Parse(executedAt),
                    TriggerReason = triggerReason, // "競合キャンペーン" | "料金" | "品質"
                });
                await db.SaveChangesAsync();
                return new { success = true };
            },
            name: "insert_mobile_mnp",
            description: "MNP（番号ポータビリティ）履歴（契約管理システム）に1件挿入します。"),

        // オプション・追加サービスマスタ
        AIFunctionFactory.Create(
            async (string optionId, string optionName, decimal optionFee,
                   string optionCategory, string validFrom, string validTo) =>
            {
                await db.OptionMaster.AddAsync(new MobileOption
                {
                    OptionId       = optionId,
                    OptionName     = optionName,
                    OptionFee      = optionFee,
                    OptionCategory = optionCategory, // "保険" | "エンタメ" | "セキュリティ"
                    ValidFrom      = DateTime.Parse(validFrom),
                    ValidTo        = DateTime.Parse(validTo),
                });
                await db.SaveChangesAsync();
                return new { success = true };
            },
            name: "insert_mobile_option_master",
            description: "オプション・追加サービスマスタ（契約管理システム）に1件挿入します。"),

        // 顧客オプション契約
        AIFunctionFactory.Create(
            async (string customerOptionId, string customerId, string optionId,
                   string subscribedAt, string? cancelledAt) =>
            {
                await db.CustomerOptions.AddAsync(new MobileCustomerOption
                {
                    CustomerOptionId = customerOptionId,
                    CustomerId       = customerId,
                    OptionId         = optionId,
                    SubscribedAt     = DateTime.Parse(subscribedAt),
                    CancelledAt      = cancelledAt is null ? null : DateTime.Parse(cancelledAt),
                });
                await db.SaveChangesAsync();
                return new { success = true };
            },
            name: "insert_mobile_customer_option",
            description: "顧客オプション契約（契約管理システム）に1件挿入します。cancelledAt は継続中の場合 null を渡してください。"),
    ];
}
```

```csharp
// Controllers/DemoGeneratorController.cs
[ApiController]
[Route("api/demo")]
public class DemoGeneratorController(
    // モバイル通信：5業務システム
    MobileCustomerMgmtGenerator mobileCustomerGen,
    MobileContractMgmtGenerator mobileContractGen,
    MobileBillingGenerator      mobileBillingGen,
    MobileInventoryGenerator    mobileInventoryGen,
    MobileCrmGenerator          mobileCrmGen,
    // EC：5業務システム
    EcProductMgmtGenerator      ecProductGen,
    EcOrderMgmtGenerator        ecOrderGen,
    EcInventoryGenerator        ecInventoryGen,
    EcMemberPointGenerator      ecMemberPointGen,
    EcMarketingGenerator        ecMarketingGen,
    // Fintech：5業務システム
    FintechAccountMgmtGenerator fintechAccountGen,
    FintechCardMgmtGenerator    fintechCardGen,
    FintechPaymentGwGenerator   fintechPaymentGen,
    FintechTradingGenerator     fintechTradingGen,
    FintechCreditGenerator      fintechCreditGen,
    // 共通：顧客統合ID基盤
    CustomerIdentityGenerator   customerIdentityGen
) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<GenerateResponse> Generate([FromBody] GenerateRequest request)
    {
        var sw   = Stopwatch.StartNew();
        var opts = new GenerateOptions { CustomerCount = request.Options?.CustomerCount ?? 500 };

        // 顧客統合ID基盤は全業種生成の前に実行
        if (request.Domain is "all" or "common")
            await customerIdentityGen.GenerateAsync(request.Scenario, opts);

        if (request.Domain is "mobile" or "all")
        {
            await mobileCustomerGen .GenerateAsync(request.Scenario, opts);
            await mobileContractGen .GenerateAsync(request.Scenario, opts);
            await mobileBillingGen  .GenerateAsync(request.Scenario, opts);
            await mobileInventoryGen.GenerateAsync(request.Scenario, opts);
            await mobileCrmGen      .GenerateAsync(request.Scenario, opts);
        }
        if (request.Domain is "ecommerce" or "all")
        {
            await ecProductGen    .GenerateAsync(request.Scenario, opts);
            await ecOrderGen      .GenerateAsync(request.Scenario, opts);
            await ecInventoryGen  .GenerateAsync(request.Scenario, opts);
            await ecMemberPointGen.GenerateAsync(request.Scenario, opts);
            await ecMarketingGen  .GenerateAsync(request.Scenario, opts);
        }
        if (request.Domain is "fintech" or "all")
        {
            await fintechAccountGen.GenerateAsync(request.Scenario, opts);
            await fintechCardGen   .GenerateAsync(request.Scenario, opts);
            await fintechPaymentGen.GenerateAsync(request.Scenario, opts);
            await fintechTradingGen.GenerateAsync(request.Scenario, opts);
            await fintechCreditGen .GenerateAsync(request.Scenario, opts);
        }

        return new GenerateResponse
        {
            JobId      = $"gen_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            Status     = "completed",
            Scenario   = request.Scenario,
            Domain     = request.Domain,
            DurationMs = sw.ElapsedMilliseconds,
        };
    }
}
```

---

## ファイル構成（想定）

```
src/
└── DemoDataGenerator/
    ├── DemoDataGenerator.csproj
    ├── Program.cs
    ├── Controllers/
    │   └── DemoGeneratorController.cs
    ├── Generators/
    │   ├── Mobile/
    │   │   ├── MobileCustomerMgmtGenerator.cs   # 顧客管理システム
    │   │   ├── MobileContractMgmtGenerator.cs   # 契約管理システム
    │   │   ├── MobileBillingGenerator.cs         # 課金・請求システム
    │   │   ├── MobileInventoryGenerator.cs       # 在庫・端末管理システム
    │   │   └── MobileCrmGenerator.cs             # コールセンター/CRMシステム
    │   ├── Ecommerce/
    │   │   ├── EcProductMgmtGenerator.cs         # 商品管理システム
    │   │   ├── EcOrderMgmtGenerator.cs           # 受注管理システム
    │   │   ├── EcInventoryGenerator.cs           # 在庫管理システム
    │   │   ├── EcMemberPointGenerator.cs         # 会員・ポイント管理システム
    │   │   └── EcMarketingGenerator.cs           # マーケティング/キャンペーン管理システム
    │   ├── Fintech/
    │   │   ├── FintechAccountMgmtGenerator.cs    # 口座管理システム
    │   │   ├── FintechCardMgmtGenerator.cs       # カード管理システム
    │   │   ├── FintechPaymentGwGenerator.cs      # 決済ゲートウェイ
    │   │   ├── FintechTradingGenerator.cs        # FX/証券/暗号資産取引システム
    │   │   └── FintechCreditGenerator.cs         # 与信・審査システム
    │   └── Common/
    │       └── CustomerIdentityGenerator.cs      # 顧客統合ID基盤（共通）
    ├── Prompts/
    │   ├── SystemPrompts.cs
    │   └── GenerationPrompts.cs
    ├── Models/
    │   ├── GenerateRequest.cs
    │   └── GenerateResponse.cs
    └── Data/
        ├── Common/
        │   └── CustomerIdentityDbContext.cs      # → common-customer-identity
        ├── Mobile/
        │   ├── MobileCustomerMgmtDbContext.cs    # → mobile-customer-mgmt
        │   ├── MobileContractMgmtDbContext.cs    # → mobile-contract-mgmt
        │   ├── MobileBillingDbContext.cs         # → mobile-billing
        │   ├── MobileInventoryDbContext.cs       # → mobile-inventory
        │   └── MobileCrmDbContext.cs             # → mobile-crm
        ├── Ecommerce/
        │   ├── EcProductMgmtDbContext.cs         # → ec-product-mgmt
        │   ├── EcOrderMgmtDbContext.cs           # → ec-order-mgmt
        │   ├── EcInventoryDbContext.cs           # → ec-inventory
        │   ├── EcMemberPointDbContext.cs         # → ec-member-point
        │   └── EcMarketingDbContext.cs           # → ec-marketing
        └── Fintech/
            ├── FintechAccountMgmtDbContext.cs    # → fintech-account-mgmt
            ├── FintechCardMgmtDbContext.cs       # → fintech-card-mgmt
            ├── FintechPaymentGwDbContext.cs      # → fintech-payment-gateway
            ├── FintechTradingDbContext.cs        # → fintech-trading
            └── FintechCreditDbContext.cs         # → fintech-credit
```

---

## 注意事項・制約

| 項目 | 内容 |
|---|---|
| **GitHub Copilot サブスクリプション** | SDK使用にはCopilotサブスクリプション（または BYOK設定）が必要 |
| **完了待機** | .NET SDKに `sendAndWait` 相当はないため `SessionIdleEvent` で待機。`TaskCompletionSource` + `WaitAsync(TimeSpan)` でタイムアウトを制御（推奨: 5〜10分） |
| **ツール呼び出し上限** | 1セッションで多数のツール呼び出しが発生する場合、セッションを分割して生成することを推奨 |
| **データ一貫性** | EF Core のトランザクション制御を行い、途中失敗時にロールバックできる構造にする |
| **接続先の選択** | **Fabric SQL Database** エンドポイント（`*.database.fabric.microsoft.com`）を使用。Warehouse / Lakehouse の SQL Analytics エンドポイントは読み取り専用のため EF Core 書き込み不可 |
| **OneLake との関係** | EF Core から OneLake（Delta/Parquet）への直接書き込みは非対応。Fabric SQL Database を経由することで物理データが自動的に OneLake に保存され、Power BI / Spark での分析が可能 |
| **シークレット管理** | `GITHUB_TOKEN` と接続文字列は `appsettings.Development.json` または `dotnet user-secrets` で管理し、コミット禁止 |
| **本番流用禁止** | 生成されたデータはデモ専用。本番DBへの流用は行わない |

---

## 関連ドキュメント

- [顧客統合ID基盤（共通システム）](common-customer-identity-system.md)
- [モバイル通信業務システムデータモデル](mobile-telecom-system-data-model.md)
- [Eコマース業務システムデータモデル](ecommerce-system-data-model.md)
- [Fintechシステムデータモデル](fintech-system-data-model.md)
- [業務システムニュース分析シナリオ](../scenario/business-system-news-analysis-scenario.md)
