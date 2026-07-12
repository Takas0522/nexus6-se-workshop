# Fabric data layer setup

Workspace: `fabric_seworkshop_ws1` / Capacity: `fabricswedencu001` (F4). このフォルダの成果物は Fabric ポータルへ貼り付け・インポートして再現する。

## 1. Fabric SQL Database 16 件

Fabric ポータルで以下の SQL Database を作成し、対応する `fabric/sql/*.sql` を Query editor で実行する。既存 DB は削除しない。

| DB | DDL | 主なテーブル |
|---|---|---|
| `sqldb_common_01` | `sql/sqldb_common_01.sql` | unified/customers, mappings, segments |
| `sqldb_mobile_01` | `sql/sqldb_mobile_01.sql` | mobile_customers |
| `sqldb_mobile_02` | `sql/sqldb_mobile_02.sql` | contracts, options, mnp_history |
| `sqldb_mobile_03` | `sql/sqldb_mobile_03.sql` | usage, plan_rules, installments |
| `sqldb_mobile_04` | `sql/sqldb_mobile_04.sql` | cost_items, device_skus, base_stations |
| `sqldb_mobile_05` | `sql/sqldb_mobile_05.sql` | tickets, campaign_actions |
| `sqldb_ecommerce_01` | `sql/sqldb_ecommerce_01.sql` | products, categories, sellers, price_rules |
| `sqldb_ecommerce_02` | `sql/sqldb_ecommerce_02.sql` | orders, returns |
| `sqldb_ecommerce_03` | `sql/sqldb_ecommerce_03.sql` | inventory, shipping_routes |
| `sqldb_ecommerce_04` | `sql/sqldb_ecommerce_04.sql` | members, point_events |
| `sqldb_ecommerce_05` | `sql/sqldb_ecommerce_05.sql` | campaigns, behaviors, reactions |
| `sqldb_fintech_01` | `sql/sqldb_fintech_01.sql` | accounts, kyc, links |
| `sqldb_fintech_02` | `sql/sqldb_fintech_02.sql` | card_transactions |
| `sqldb_fintech_03` | `sql/sqldb_fintech_03.sql` | merchants, transaction_alerts |
| `sqldb_fintech_04` | `sql/sqldb_fintech_04.sql` | fx_positions, fx_rates, product_rules |
| `sqldb_fintech_05` | `sql/sqldb_fintech_05.sql` | credit, loans, risk, revenue |

DDL は `src/DemoDataGenerator/src/DemoDataGenerator.Data/` の EF Core SQL Server モデルから生成して分割した。

## 2. Lakehouse

`fabric/lakehouses/README.md` に従い、`lh_nexus6_bronze`、`lh_nexus6_silver`、`lh_nexus6_gold` を作成する。`fabric/seed/scenario_seed.csv` は `lh_nexus6_bronze/Files/manual_seed/scenario_seed.csv` にアップロードする。

## 3. Notebook

1. Fabric Workspace に `fabric/notebooks/nb_bronze_to_silver.ipynb` と `fabric/notebooks/nb_silver_to_gold.ipynb` をインポートする。
2. `nb_bronze_to_silver` を Bronze/Silver Lakehouse にアタッチして実行する。
3. `nb_silver_to_gold` を Silver/Gold Lakehouse にアタッチして実行する。

Notebook は `overwriteSchema=true` で再実行可能。

## 4. DemoDataGenerator 接続例

Fabric SQL Database への接続文字列形式:

```text
Server=<workspace-or-sql-endpoint>.database.fabric.microsoft.com;Database={dbname};Authentication=Active Directory Default;Encrypt=True;
```

実行例:

```bash
cd src/DemoDataGenerator
dotnet run --project src/DemoDataGenerator.App -- \
  --scenario fx-shock \
  --target fabric-sql \
  --conn-prefix "Server=<sql-endpoint>.database.fabric.microsoft.com;Database={dbname};Authentication=Active Directory Default;Encrypt=True;" \
  --scale large
```

現行 CLI は `{dbname}` を `sqldb_common_01` / `sqldb_mobile_01` / `sqldb_ecommerce_01` / `sqldb_fintech_01` に展開する。16 DB 分割済み環境では、データ生成側の per-system 接続対応後に同じ DDL 配置で各 DB へ投入する。Notebook は現行 CLI の 4 DB 配置にもフォールバックして読み取れる。

## 5. Gold 検証クエリ

Gold Lakehouse SQL Analytics Endpoint で確認する。

```sql
SELECT TOP 100 * FROM kpi.monthly_revenue;
SELECT TOP 100 * FROM kpi.monthly_cost_detail;
SELECT TOP 100 * FROM kpi.customer_count;
SELECT TOP 100 * FROM kpi.fx_sensitivity;
SELECT TOP 100 * FROM mobile_ai.risk_summary;
SELECT TOP 100 * FROM ecommerce_ai.risk_summary;
SELECT TOP 100 * FROM fintech_ai.risk_summary;
```

## 6. Fabric REST 試行結果

`fabric/PROVISIONING_ATTEMPT.md` に記録した。Workspace 検索は成功。既存リソースを変更しないため SQL DB / Lakehouse / Notebook の自動作成は行わず、上記の手動手順で代替する。
