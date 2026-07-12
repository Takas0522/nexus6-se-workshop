# 携帯電話事業：改正個人情報保護法インパクト判断スキル

## 前提

- 本文書は nexus6-se-workshop の Demo 用ナレッジであり、数値・事例はすべて想定値である。
- 個人情報、実在企業の機密、実顧客の行動履歴は含めない。
- 対象事業部は 携帯電話事業 である。
- 対応シナリオは「改正個人情報保護法によるデータ規制強化（法規制・コンプライアンスリスク）」。
- Agent 2 は影響有無の評価、Agent 3 は事業部別 Next Action の草案作成に使う。

## 概要

2026年施行の改正個人情報保護法により、携帯電話事業が保有する顧客行動データ（位置情報、通信利用パターン、閲覧履歴等）の利活用に明示的同意（オプトイン）が必須化された際のインパクトを判断するスキル。顧客データ活用マーケティング施策への制約度、同意取得率に基づく施策カバレッジ低下、制裁金リスク（売上高最大4%）、および是正対応コストを総合評価し、推奨アクションを出力する。

## 適用シナリオ

- 外部ニュースのトリガー: 個人情報保護法改正案の可決・施行、個人情報保護委員会のガイドライン公表、同種規制の海外動向（GDPR強化等）。
- ニュースは判断の入口であり、最終判断は業務システム内のデータで行う。
- 速報段階では 現行のデータ処理件数、同意取得状況、データ連携先パートナー数 を即時参照する。
- 中期評価では 同意管理システムの成熟度、マーケティング施策のデータ依存度、制裁金リスク額 を含めて再評価する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation_compliance`） |
| `regulation_name` | string | 規制名称（`改正個人情報保護法`） |
| `regulation_scope` | enum | `domestic` / `sector_specific` |
| `consent_requirement` | enum | `opt_in_explicit` / `opt_in_implicit` / `opt_out` |
| `penalty_rate_percent` | float | 制裁金率（売上高に対する%、例: `4.0`） |
| `enforcement_date` | date | 施行日 |
| `data_categories_affected` | list[string] | 規制対象データ種別（例: `location`, `usage_pattern`, `browsing_history`, `profiling`, `third_party_sharing`） |
| `third_party_sharing_restricted` | bool | 第三者提供の規制有無 |

### 業務データ

| パラメータ | 型 | 説明 | 参照テーブル |
|---|---|---|---|
| `total_customers` | int | 総顧客数 | `mobile_customers` (status=ACTIVE) |
| `customers_with_consent` | int | 明示的同意取得済み顧客数 | `mobile_customers` + 同意管理台帳 |
| `data_driven_campaigns_active` | int | データ活用マーケティング施策数（実行中） | 施策管理台帳 |
| `data_driven_revenue_annual_jpy` | int | データ活用施策による年間増収額（円） | `mobile_transactions` 施策紐付き集計 |
| `total_annual_revenue_jpy` | int | 携帯電話事業年間売上高（円） | `mobile_transactions` 年次集計 |
| `data_sharing_partner_count` | int | データ提供先パートナー企業数 | パートナー管理台帳 |
| `consent_management_maturity` | enum | `advanced` / `standard` / `basic` / `none` | 自己評価 |
| `compliance_staff_count` | int | プライバシー対応要員数 | 人事情報 |
| `customer_segments_using_data` | list[string] | データ活用中の顧客セグメント | `customer_segments` |

## 判断ロジック

### Step 1: 同意取得カバレッジの評価

```
consent_rate = customers_with_consent / total_customers

IF consent_rate < 0.30 THEN consent_gap = "critical"
ELSE IF consent_rate < 0.50 THEN consent_gap = "high"
ELSE IF consent_rate < 0.75 THEN consent_gap = "medium"
ELSE consent_gap = "low"
```

### Step 2: マーケティング施策カバレッジ損失の推定

```
-- 同意未取得顧客に対する施策は停止が必要
unconsented_ratio = 1.0 - consent_rate
campaign_coverage_loss = unconsented_ratio × data_driven_campaigns_active

-- 施策停止による収益影響
revenue_loss_jpy = data_driven_revenue_annual_jpy × unconsented_ratio
```

### Step 3: 制裁金リスクの算出

```
max_penalty_jpy = total_annual_revenue_jpy × (penalty_rate_percent / 100)

-- 現行体制の脆弱性に基づく違反確率推定
violation_probability:
  consent_management_maturity "none"     → 0.60
  consent_management_maturity "basic"    → 0.35
  consent_management_maturity "standard" → 0.15
  consent_management_maturity "advanced" → 0.05

expected_penalty_jpy = max_penalty_jpy × violation_probability
```

### Step 4: 対応準備度の評価

```
maturity_score:
  advanced → 0.85
  standard → 0.55
  basic    → 0.25
  none     → 0.0

staff_adequacy = MIN(1.0, compliance_staff_count / (total_customers / 3000))
preparedness = (maturity_score × 0.7 + staff_adequacy × 0.3)
vulnerability = 1.0 - MIN(preparedness, 1.0)
```

### Step 5: 時間的切迫度の評価

```
months_to_enforcement = DATEDIFF(MONTH, CURRENT_DATE, enforcement_date)

IF months_to_enforcement <= 3 THEN urgency = "critical"
ELSE IF months_to_enforcement <= 6 THEN urgency = "high"
ELSE IF months_to_enforcement <= 12 THEN urgency = "medium"
ELSE urgency = "low"

urgency_weight:
  critical → 1.0
  high     → 0.75
  medium   → 0.5
  low      → 0.25
```

### Step 6: 第三者提供リスクの評価

```
IF third_party_sharing_restricted AND data_sharing_partner_count >= 10
  THEN partner_risk = "high"
ELSE IF third_party_sharing_restricted AND data_sharing_partner_count >= 3
  THEN partner_risk = "medium"
ELSE
  partner_risk = "low"

partner_risk_weight:
  high   → 0.30
  medium → 0.15
  low    → 0.05
```

### Step 7: 総合インパクトスコア算出

```
consent_gap_weight:
  critical → 1.0
  high     → 0.75
  medium   → 0.45
  low      → 0.15

financial_factor = MIN(1.0, (revenue_loss_jpy + expected_penalty_jpy) / (total_annual_revenue_jpy × 0.05))

raw_score = (consent_gap_weight × 0.25
           + financial_factor × 0.30
           + vulnerability × 0.15
           + urgency_weight × 0.20
           + partner_risk_weight × 0.10)
           × 100

impact_score = CLAMP(raw_score, 0, 100)
```

### Step 8: インパクトレベル判定

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
| `consent_gap_level` | enum | 同意取得ギャップレベル |
| `consent_rate` | float | 現在の同意取得率 |
| `revenue_loss_estimate_jpy` | int | データ施策停止による年間収益損失推定額（円） |
| `max_penalty_estimate_jpy` | int | 最大制裁金推定額（円） |
| `expected_penalty_jpy` | int | 期待制裁金額（違反確率加味） |
| `campaigns_at_risk` | int | 停止が必要な施策数 |
| `urgency_level` | enum | 時間的切迫度 |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `notification_channel` | string | 推奨通知チャネル（`email` / `teams` / `slack`） |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション | 通知チャネル |
|---|---|---|
| critical | 全データ処理プロセスの即時棚卸し・是正、同意管理基盤（CMP）の緊急導入/刷新、同意未取得顧客向けデータ施策の一時停止、第三者データ提供契約の緊急レビュー・凍結、法務部門との日次協議体制の構築、経営層エスカレーション | teams + email |
| high | 同意取得キャンペーンの即時展開（対象: 未取得顧客）、エリアターゲティング/利用状況分析の同意フロー再設計、プライバシー影響評価（PIA）の全施策実施、コンプライアンス要員の増員計画策定 | teams |
| medium | 現行データ処理の棚卸し・文書化、同意取得UI/UXの改善計画策定、社内プライバシー教育プログラムの実施、業界団体を通じたベストプラクティス収集 | email |
| low | 規制動向のモニタリング継続、四半期レビューでの進捗確認、改正法Q&Aの社内共有 | 定期レポート |

### 通知テンプレート（critical / high 時）

```
件名: 【{impact_level}】個人情報保護法改正に伴う顧客データ利用の見直し
本文:
改正法の施行により、顧客データを活用したマーケティング施策に
明示的同意が必要となります。
- インパクトスコア: {impact_score}/100
- 同意取得率: {consent_rate:.1%}（ギャップ: {consent_gap_level}）
- 影響施策数: {campaigns_at_risk}件
- 収益損失推定: ¥{revenue_loss_estimate_jpy:,}/年
- 最大制裁金: ¥{max_penalty_estimate_jpy:,}（売上高の{penalty_rate_percent}%）

既存の顧客データ利用状況を棚卸しし、同意未取得の
データ処理を特定してください。
対象顧客への同意取得キャンペーンの実施を検討願います。
```

## 参照データソース（Fabric テーブル）

| テーブル名 | DB | 説明 | 主要カラム |
|---|---|---|---|
| `mobile_customers` | sqldb_mobile_01 | 携帯電話事業の顧客マスタ | `mobile_customer_id`, `customer_name`, `customer_type`, `status` |
| `mobile_contracts` | sqldb_mobile_01 | 回線契約情報 | `contract_id`, `mobile_customer_id`, `plan_code`, `status` |
| `mobile_transactions` | sqldb_mobile_01 | 取引履歴 | `transaction_id`, `mobile_customer_id`, `transaction_type`, `amount`, `transaction_date` |
| `unified_customers` | sqldb_common_01 | 統合顧客マスタ | `unified_customer_id`, `full_name`, `email`, `phone_number` |
| `domain_id_mappings` | sqldb_common_01 | 業務領域IDマッピング | `unified_customer_id`, `domain_code`, `domain_customer_id`, `is_active` |
| `customer_segments` | sqldb_common_01 | 顧客セグメント | `unified_customer_id`, `segment_code`, `risk_score`, `lifetime_value` |

## データ品質メモ

- 同意取得状況は現行DBスキーマに専用カラムがないため、外部の同意管理台帳（CMP）との突合が必要。`customers_with_consent` は CMP から取得する前提。
- `mobile_transactions` の `description` に施策コードが含まれる取引を「データ活用施策紐付き」として集計する。
- `customer_segments.segment_code` が `PREMIUM` の顧客はデータ活用度が高い傾向があり、同意未取得時の収益影響が大きい。セグメント別の影響分析を推奨。
- 第三者データ提供先の管理はパートナー管理台帳（別システム）を参照。DB上の `domain_id_mappings` は領域間マッピングであり、外部パートナーは含まない。
- 制裁金算出の `total_annual_revenue_jpy` はグループ連結ではなく携帯電話事業単体の売上を使用する。
