# 携帯電話事業：半導体供給制約インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は 携帯電話事業 である。
- 対応シナリオは「世界的半導体供給制約の深刻化（サプライチェーンリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

米中間の半導体輸出規制強化等により主要チップメーカーの出荷が遅延した際に、携帯電話事業に与えるインパクトを定量的に判断するスキル。端末調達遅延の深刻度、在庫枯渇リスク、販売機会損失額、MNP転出リスクを総合評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: 半導体輸出規制強化、ファウンドリ出荷遅延、チップメーカーの減産発表、地政学的緊張による供給網寸断。
- ニュースは判断の入口であり、最終判断は業務システム内のデータで行う。
- 速報段階では 現在在庫週数、直近月間販売台数、バックオーダー件数 を即時参照する。
- 中期評価では 調達リードタイム推移、代替サプライヤー対応状況、5G展開計画への影響 を含めて再評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`supply_chain`） |
| `affected_components` | list[string] | 影響を受ける半導体コンポーネント（例: `SoC`, `memory`, `display_driver`, `5g_modem`） |
| `supply_shortage_severity` | enum | `critical` / `severe` / `moderate` / `minor` |
| `estimated_delay_weeks` | int | 推定調達遅延期間（週） |
| `affected_manufacturers` | list[string] | 影響を受ける端末メーカー（例: `Apple`, `Samsung`, `Sony`, `Sharp`, `Google`） |
| `geographic_scope` | enum | `global` / `regional_asia` / `regional_other` |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `current_inventory_weeks` | float | 現在の端末在庫週数 | `mobile_device_inventory` |
| `monthly_sales_volume` | int | 月間端末販売台数 | `mobile_transactions` (DEVICE_PURCHASE) |
| `backorder_count` | int | 現在のバックオーダー件数 | `mobile_contracts` (status=ACTIVE, 未出荷) |
| `affected_model_ratio` | float | 影響モデルの売上構成比（0.0〜1.0） | `mobile_device_inventory`, `mobile_transactions` |
| `alternative_supplier_count` | int | 代替調達先の数 | `mobile_device_inventory` (manufacturer DISTINCT) |
| `mnp_churn_rate_current` | float | 直近MNP転出率（月次） | `mobile_customers` (status=CANCELLED / total) |
| `avg_device_margin_jpy` | int | 端末1台あたり平均粗利（円） | `mobile_transactions` (DEVICE_PURCHASE) 集計 |
| `active_contract_count` | int | アクティブ契約数 | `mobile_contracts` (status=ACTIVE) |

## 判断ロジック

### Step 1: 供給不足の重大性判定

```
IF supply_shortage_severity == "critical" AND geographic_scope == "global"
  THEN severity = "critical"
ELSE IF supply_shortage_severity == "critical"
  OR (supply_shortage_severity == "severe" AND geographic_scope == "global")
  THEN severity = "high"
ELSE IF supply_shortage_severity == "severe"
  OR supply_shortage_severity == "moderate"
  THEN severity = "medium"
ELSE
  severity = "low"
```

### Step 2: 在庫枯渇リスクの算出

```
weekly_demand = monthly_sales_volume / 4
inventory_coverage_ratio = current_inventory_weeks / estimated_delay_weeks

IF inventory_coverage_ratio < 0.3 THEN stockout_risk = "critical"
ELSE IF inventory_coverage_ratio < 0.6 THEN stockout_risk = "high"
ELSE IF inventory_coverage_ratio < 1.0 THEN stockout_risk = "medium"
ELSE stockout_risk = "low"
```

### Step 3: 販売機会損失額の推定

```
shortage_weeks = MAX(0, estimated_delay_weeks - current_inventory_weeks)
lost_sales_volume = weekly_demand × shortage_weeks × affected_model_ratio
lost_revenue_jpy = lost_sales_volume × avg_device_margin_jpy
```

### Step 4: 顧客離反リスクの評価

```
churn_multiplier:
  stockout_risk "critical" → 3.0
  stockout_risk "high"     → 2.0
  stockout_risk "medium"   → 1.4
  stockout_risk "low"      → 1.0

estimated_churn_rate = mnp_churn_rate_current × churn_multiplier
estimated_churn_volume = active_contract_count × estimated_churn_rate × (estimated_delay_weeks / 4)
```

### Step 5: 代替調達の緩和効果

```
IF alternative_supplier_count >= 3 THEN mitigation_factor = 0.5
ELSE IF alternative_supplier_count >= 1 THEN mitigation_factor = 0.75
ELSE mitigation_factor = 1.0
```

### Step 6: 総合インパクトスコア算出

```
severity_weight = { critical: 1.0, high: 0.75, medium: 0.45, low: 0.15 }
stockout_weight = { critical: 1.0, high: 0.7,  medium: 0.4,  low: 0.1  }

raw_score = (severity_weight[severity] × 0.4
           + stockout_weight[stockout_risk] × 0.4
           + affected_model_ratio × 0.2)
           × mitigation_factor × 100

impact_score = CLAMP(raw_score, 0, 100)
```

### Step 7: インパクトレベル判定

```
IF impact_score >= 80 THEN level = "critical"
ELSE IF impact_score >= 55 THEN level = "high"
ELSE IF impact_score >= 30 THEN level = "medium"
ELSE level = "low"
```

## 出力

| フィールド | 型 | 説明 |
|---|---|---|
| `impact_score` | int (0-100) | 総合インパクトスコア |
| `impact_level` | enum | `critical` / `high` / `medium` / `low` |
| `stockout_risk_level` | enum | 在庫枯渇リスクレベル |
| `shortage_weeks` | int | 在庫不足予測期間（週） |
| `lost_revenue_estimate_jpy` | int | 販売機会損失推定額（円） |
| `churn_risk_volume` | int | 顧客離反予測数 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル（`email` / `teams` / `slack`） |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | 緊急調達会議の招集、代替メーカー（OPPO/Xiaomi等）との緊急交渉開始、在庫配分アルゴリズムの即時切替（解約リスク顧客優先）、端末予約・優先配送プログラムの発動、経営層エスカレーション | email + teams |
| high | 代替端末メーカーへの発注拡大、中古端末リファービッシュ事業の拡大、通信プラン単体契約への誘導施策強化、バックオーダー顧客へのリテンション施策実行 | email |
| medium | 調達先ポートフォリオの見直し着手、在庫安全水準の引き上げ検討、影響モデルの販促抑制によるバーンレート低減 | email |
| low | サプライチェーン動向のモニタリング強化、次四半期調達計画のレビュー、定期レポートへの記載 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】半導体供給制約に伴う端末在庫リスクのお知らせ
本文:
半導体供給の逼迫により、一部端末モデルの入荷遅延が見込まれます。
- インパクトスコア: {impact_score}/100
- 在庫枯渇リスク: {stockout_risk_level}
- 在庫不足予測: {shortage_weeks}週間
- 販売機会損失推定: ¥{lost_revenue_estimate_jpy:,}
- 顧客離反リスク: {churn_risk_volume}名

現在の在庫状況を確認のうえ、該当端末の販売計画の見直し
および代替機種の提案準備をお願いします。
影響を受ける契約顧客への事前案内もご検討ください。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `mobile_customers` | sqldb_mobile_01 | 携帯電話事業の顧客マスタ | `mobile_customer_id`, `customer_name`, `customer_type`, `status` |
| `mobile_contracts` | sqldb_mobile_01 | 回線契約情報 | `contract_id`, `mobile_customer_id`, `plan_code`, `phone_number`, `monthly_fee`, `status` |
| `mobile_transactions` | sqldb_mobile_01 | 取引履歴 | `transaction_id`, `mobile_customer_id`, `contract_id`, `transaction_type`, `amount`, `transaction_date` |
| `mobile_device_inventory` | sqldb_mobile_01 | 端末在庫管理 | `inventory_id`, `device_model`, `manufacturer`, `stock_quantity`, `unit_cost`, `warehouse_code`, `status` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- `mobile_device_inventory.stock_quantity` が 0 かつ `status` が AVAILABLE の場合はデータ不整合の可能性あり。SHIPPED として扱う。
- `mobile_transactions` の `transaction_type = 'DEVICE_PURCHASE'` のみ端末販売として集計する。
- `mobile_customers.status = 'CANCELLED'` の発生日は `updated_at` を参照する。
- 在庫週数の算出には直近4週間の販売実績平均を用いる。単週の異常値（キャンペーン等）は除外する。
