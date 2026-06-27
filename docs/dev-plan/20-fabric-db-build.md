# 20. Fabric DB 実プロビジョニング記録

関連: [06. Fabric データ取り込み設計](06-fabric-data-ingestion.md) | [19. 未解決 TODO 集約](19-open-items.md)

---

## 採用案

採用: **案A: Fabric SQL Database × 4**

| DB | Fabric item | 物理 database name |
|---|---|---|
| common | `sqldb_common_01` | `sqldb_common_01-31ed4242-3749-4b67-a4cb-02f2ec32df34` |
| mobile | `sqldb_mobile_01` | `sqldb_mobile_01-6c0e0c01-8fcf-4e98-b311-190035a69911` |
| ecommerce | `sqldb_ecommerce_01` | `sqldb_ecommerce_01-670f0bcb-46e5-4c16-8836-330fd9b73829` |
| fintech | `sqldb_fintech_01` | `sqldb_fintech_01-529ae3ef-81d8-4fb2-96ec-d0d2f856e040` |

選定理由:

- `DemoDataGenerator` は EF Core `UseSqlServer` で 4 DbContext（common / mobile / ecommerce / fintech）に分割済み。
- Warehouse は Fabric SQL DW の型・DDL 制約が EF Core `EnsureCreated()` とずれやすく、F4 Capacity では最少構成が望ましい。
- Lakehouse SQL Endpoint はテーブル DDL/DML が read-only 寄りで、EF Core 直接書き込み先には不適。
- Fabric Skills は `sqldw-authoring-cli` / `sqldw-consumption-cli` の接続・検証手順、`FabricDataEngineer` の分担方針、`e2e-medallion-architecture` の Notebook 残課題整理に利用した。

---

## 作成コマンド

```bash
WS_ID="49d1a6c2-96c0-4da3-a1df-911b19a2f0bc"
for name in sqldb_common_01 sqldb_mobile_01 sqldb_ecommerce_01 sqldb_fintech_01; do
  az rest --method post \
    --resource "https://api.fabric.microsoft.com" \
    --url "https://api.fabric.microsoft.com/v1/workspaces/$WS_ID/sqlDatabases" \
    --headers "Content-Type=application/json" \
    --body "{\"displayName\":\"$name\"}"
done
```

接続文字列形式:

```text
Data Source=<server>.database.fabric.microsoft.com,1433;Initial Catalog={dbname};Authentication=Active Directory Default;Encrypt=True;Trust Server Certificate=False;Multiple Active Result Sets=False;Connect Timeout=60
```

`{dbname}` には上表の物理 database name を指定する。シークレットは不要（Entra ID / Managed Identity）。

---

## スキーマ作成・データ投入

`DemoDataGenerator` に物理 DB 名指定オプションを追加し、Fabric SQL 実行時はシナリオ再投入前に既存デモ行を削除するようにした。

```bash
SERVER="<server>.database.fabric.microsoft.com,1433"
CONN="Data Source=$SERVER;Initial Catalog={dbname};Authentication=Active Directory Default;Encrypt=True;Trust Server Certificate=False;Multiple Active Result Sets=False;Connect Timeout=60"

for s in 1 2 3; do
  dotnet run --project src/DemoDataGenerator/src/DemoDataGenerator.App -- \
    --scenario "$s" \
    --target fabric-sql \
    --conn-prefix "$CONN" \
    --common-db-name "sqldb_common_01-31ed4242-3749-4b67-a4cb-02f2ec32df34" \
    --mobile-db-name "sqldb_mobile_01-6c0e0c01-8fcf-4e98-b311-190035a69911" \
    --ecommerce-db-name "sqldb_ecommerce_01-670f0bcb-46e5-4c16-8836-330fd9b73829" \
    --fintech-db-name "sqldb_fintech_01-529ae3ef-81d8-4fb2-96ec-d0d2f856e040" \
    --scale small
done
```

実行結果:

```text
scenario=Fx, customers=300, mobile_contracts=300, ecommerce_orders=200, fintech_accounts=150
scenario=Competitor, customers=300, mobile_contracts=300, ecommerce_orders=200, fintech_accounts=150
scenario=BojRateHike, customers=300, mobile_contracts=300, ecommerce_orders=200, fintech_accounts=150
```

---

## 投入後件数

| DB | table | count |
|---|---|---:|
| common | `unified_customers` | 300 |
| common | `domain_id_mappings` | 650 |
| common | `customer_segment_assignments` | 300 |
| common | `customer_integration_events` | 300 |
| mobile | `mobile_customers` | 300 |
| mobile | `mobile_contracts` | 300 |
| mobile | `mobile_usage_billings` | 300 |
| mobile | `mobile_cost_items` | 30 |
| mobile | `mnp_history` | 37 |
| ecommerce | `members` | 200 |
| ecommerce | `orders` | 200 |
| ecommerce | `products` | 20 |
| ecommerce | `point_events` | 200 |
| ecommerce | `campaign_reactions` | 200 |
| fintech | `accounts` | 150 |
| fintech | `card_transactions` | 150 |
| fintech | `fx_positions` | 150 |
| fintech | `loan_balances` | 150 |
| fintech | `transaction_alerts` | 21 |

Hosted Agent の実 Fabric 接続確認用に、暫定 serving table を common DB に作成した。

| schema.table | count |
|---|---:|
| `kpi.monthly_revenue` | 3 |
| `mobile_ai.risk_summary` | 2 |
| `ecommerce_ai.risk_summary` | 2 |
| `fintech_ai.risk_summary` | 2 |

---

## ACA 接続情報更新

```bash
az containerapp update \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --set-env-vars \
    Fabric__SqlEndpoint="<server>.database.fabric.microsoft.com,1433" \
    Fabric__Database="sqldb_common_01-31ed4242-3749-4b67-a4cb-02f2ec32df34"
```

確認結果:

```text
ca-nexus6-hosted-agent--0000002  Running  traffic=100
Fabric__SqlEndpoint=<server>.database.fabric.microsoft.com,1433
Fabric__Database=sqldb_common_01-31ed4242-3749-4b67-a4cb-02f2ec32df34
```

---

## 再 E2E

`/devui/run` で S1 相当の短文を投入し、Workflow は HTTP 200 で完了した。

```text
runId=d69788f5-2d9f-4459-b1dc-5e67eb2aaeb2
web-research / business-impact / division-recommend-* / notification succeeded=True
impactReasons=Fabric KPI heuristic fallback score ...
mock-fabric token not present in /devui/logs
```

Foundry / Teams は既存 fallback 経路を含むが、Fabric KPI は上記 common DB の `kpi` / `*_ai` table を参照できる状態に更新済み。

---

## Bronze / Silver / Gold Notebook

今回は未実行。理由:

- 現行 Notebook は SQL DB ミラー名を `sqldb_common_01` 等の論理名で読む前提だが、今回作成した Fabric SQL Database の OneLake ミラー/Shortcut 未構成。
- F4 Capacity の負荷を抑え、DB 実投入と ACA 実接続を優先した。

手動実行手順:

1. `lh_nexus6_bronze` / `lh_nexus6_silver` / `lh_nexus6_gold` を作成する。
2. SQL Database 4 個を Bronze から読めるように Mirror / Shortcut / Notebook JDBC のいずれかで接続する。
3. `fabric/notebooks/nb_bronze_to_silver.ipynb` を SQL DB 4 個の物理名に合わせて調整する。
4. `nb_bronze_to_silver` → `nb_silver_to_gold` の順に RunNotebook job を実行する。
5. Gold SQL Endpoint が Ready になったら ACA `Fabric__SqlEndpoint` / `Fabric__Database` を Gold に戻す。

---

## 残課題

- Bronze/Silver/Gold Notebook の Fabric ワークスペース import・実行。
- SQL DB → Lakehouse の Mirror / Shortcut 設計確定。
- 暫定 `kpi` / `*_ai` serving table を Gold Lakehouse 生成後に置換。
- ACA Managed Identity の Fabric SQL Database 権限は E2E で実接続済みだが、運用時は最小権限を再確認する。
