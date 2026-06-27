# 21. Fabric Medallion 実行記録

関連: [20. Fabric DB 実プロビジョニング記録](20-fabric-db-build.md) | [07. Fabric データモデル設計](07-fabric-data-model.md)

---

## 採用手順

採用: **1 Workspace / 3 Lakehouse 構成**。

| Layer | Lakehouse | itemId |
|---|---|---|
| Bronze | `lh_nexus6_bronze` | `f5269936-069a-4850-b3f5-778889c97cec` |
| Silver | `lh_nexus6_silver` | `6479740e-61c8-4bff-a1df-b68316ab74b5` |
| Gold | `lh_nexus6_gold` | `9e97231b-d080-4158-8bf1-4e9202f9cc0a` |

`fabric/seed/scenario_seed.csv` は Bronze の `Files/manual_seed/scenario_seed.csv` に OneLake DFS API でアップロードした。

SQL DB ミラーは Spark Catalog からは未解決だったため、Notebook では SQL DB 論理名を試行後、Fabric SQL Database へ Entra token JDBC で read-only 取得する fallback を追加した。OneLake DFS の SQL DB item 直参照は `501 OneLake PathResolution Api is not enabled` だった。

---

## SQL DB 同期 / 参照確認

| DB | table | count | sample |
|---|---|---:|---|
| `sqldb_common_01` | `unified_customers` | 300 | `UC-000001 / Demo Customer 000001 / 関西` |
| `sqldb_mobile_01` | `mobile_contracts` | 300 | `CON-000001 / MOB-000001 / 2026-06` |
| `sqldb_ecommerce_01` | `orders` | 200 | `ORD-000001 / ECM-000001 / 2026-05-31` |
| `sqldb_fintech_01` | `accounts` | 150 | `ACC-000001 / FIN-000001 / 普通` |

---

## Notebook import / 実行

| Notebook | itemId | default Lakehouse | final job | result |
|---|---|---|---|---|
| `nb_bronze_to_silver` | `38e1e864-6a12-49eb-8360-aaa69a0d2e6c` | `lh_nexus6_silver` | `3523e5cd-1449-48ac-bd14-74096aaa45a7` | Completed |
| `nb_silver_to_gold` | `c2f17dc1-f338-426f-b089-74f524604f79` | `lh_nexus6_gold` | `c312e111-61e5-4bb6-acf0-e2592d2952cb` | Completed |

失敗時の主な修正:

```text
RuntimeError: No readable source table found ... JDBC ... SSL certificate ...
→ SQL DB mirror 未解決時の JDBC fallback を維持し、Fabric SQL Database の証明書経路差分に対応。

IllegalArgumentException: DELTA_OVERWRITE_SCHEMA_WITH_DYNAMIC_PARTITION_OVERWRITE
→ overwriteSchema と競合する dynamic partition overwrite を static に変更。

AnalysisException: Cannot resolve column name "division" among (year_month, ecommerce, cost_of_goods, total_cost_jpy)
→ Gold cost_detail union の lit 列に alias を付与。
```

---

## Gold KPI 検証

Gold SQL Endpoint:

```text
server=mnlvphuj2ouevmfjuzf2jtaqfe-yktncsoas2ru3io7senrtixqxq.datawarehouse.fabric.microsoft.com
database=lh_nexus6_gold
status=Success
```

| table | count |
|---|---:|
| `kpi.monthly_revenue` | 5 |
| `kpi.monthly_cost_detail` | 5 |
| `kpi.customer_count` | 5 |
| `kpi.fx_sensitivity` | 1 |
| `mobile_ai.risk_summary` | 19 |
| `ecommerce_ai.risk_summary` | 10 |
| `fintech_ai.risk_summary` | 5 |

主要 sample:

```text
kpi.monthly_revenue
2026-06 fintech gross_revenue_jpy=1515000.00 gross_margin_rate=0.9306931 active_customer_count=150
2026-06 mobile  gross_revenue_jpy=50523.00   gross_margin_rate=-0.7417810 active_customer_count=300
2026-06 ecommerce gross_revenue_jpy=23400.00 gross_margin_rate=0.3589744 active_customer_count=6

mobile_ai.risk_summary
2026-06 device_subsidy 3000000.0000 JPY
2026-05 mnp_out_count 35.0000 件

ecommerce_ai.risk_summary
2026-05 cross_border_cost 1434750.0000 JPY
2026-05 point_cost 15520.0000 point

fintech_ai.risk_summary
2026-06 fx_position_pnl 56325.0000 JPY
2026-06 loan_balance 26325000.0000 JPY
```

---

## ACA env / E2E smoke

ACA `ca-nexus6-hosted-agent` は Gold SQL Endpoint に更新済み。

```text
revision=ca-nexus6-hosted-agent--0000003 traffic=100 Ready=Running
Fabric__SqlEndpoint=mnlvphuj2ouevmfjuzf2jtaqfe-yktncsoas2ru3io7senrtixqxq.datawarehouse.fabric.microsoft.com
Fabric__Database=lh_nexus6_gold
```

`/devui/run` で S1 相当の円安シナリオを実行し HTTP 200。

```text
runId=a8954d92-6165-4db4-943f-7b7955c31008
web-research / business-impact / division-recommend-* / notification succeeded=True
impactReasons=Fabric KPI heuristic fallback score ...
dataReferences=mobile_ai.risk_summary / ecommerce_ai.risk_summary / fintech_ai.risk_summary
mock-fabric token not present in /devui/logs
```

---

## 残課題

- Fabric SQL Database の OneLake 自動ミラーを Spark Catalog で直接参照する名前解決は未確定。現状は JDBC fallback で Gold 生成済み。
- Notebook は手動 RunNotebook 実行。定期実行する場合は Pipeline 化する。
- JDBC fallback は Fabric SQL Database の証明書ルーティング差分により `trustServerCertificate=true` を使用したため、運用前に証明書検証方式を再確認する。
