# SI事業：金融政策転換インパクト判断スキル

## 概要

中央銀行の金融政策転換（利上げ/利下げ）がSI事業に与えるインパクトを判断するスキル。クライアント企業のIT投資意欲の変化、進行中・パイプライン案件の凍結・延期リスク、自社の借入コスト変動を定量評価し、案件ポートフォリオの見直し要否と推奨アクションを出力する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `policy_direction` | enum | `rate_hike` / `rate_cut` |
| `rate_change_bps` | int | 政策金利変動幅（ベーシスポイント） |
| `new_policy_rate` | float | 変更後の政策金利（%） |
| `guidance_outlook` | enum | `further_tightening` / `neutral` / `easing` |
| `effective_date` | date | 政策適用日 |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `pipeline_total_oku` | float | パイプライン案件総額（億円） |
| `pipeline_count` | int | パイプライン案件数 |
| `active_projects_count` | int | 進行中プロジェクト数 |
| `active_projects_revenue_oku` | float | 進行中案件の年間売上見込（億円） |
| `client_industry_mix` | map[string, float] | クライアント業種別構成比 |
| `discretionary_project_ratio` | float | 裁量的投資案件（戦略投資型）の比率（0.0〜1.0） |
| `maintenance_project_ratio` | float | 保守・運用案件の比率（0.0〜1.0） |
| `avg_project_duration_months` | int | 平均プロジェクト期間（月） |
| `own_debt_ratio` | float | 自社有利子負債比率（0.0〜1.0） |
| `monthly_fixed_cost_oku` | float | 月間固定費（億円） |

## 判断ロジック

### Step 1: 金利変動の重大性判定

```
IF rate_change_bps >= 75 THEN severity = "critical"
ELSE IF rate_change_bps >= 50 THEN severity = "high"
ELSE IF rate_change_bps >= 25 THEN severity = "medium"
ELSE severity = "low"
```

### Step 2: クライアントIT投資意欲への影響推定

```
// 業種別の金利感応度
industry_sensitivity = {
    "manufacturing": 0.8,
    "retail": 0.7,
    "real_estate": 0.9,
    "financial": 0.6,
    "public_sector": 0.1,
    "telecom": 0.5,
    "healthcare": 0.3
}

weighted_sensitivity = SUM(client_industry_mix[i] × industry_sensitivity[i] for i in industries)

// 裁量的投資案件ほど凍結されやすい
freeze_probability =
    IF severity == "critical" THEN 0.40 × weighted_sensitivity
    ELSE IF severity == "high" THEN 0.25 × weighted_sensitivity
    ELSE IF severity == "medium" THEN 0.12 × weighted_sensitivity
    ELSE 0.03 × weighted_sensitivity
```

### Step 3: パイプライン案件の凍結・延期リスク算出

```
// 裁量的案件のみ凍結対象
at_risk_pipeline = pipeline_total_oku × discretionary_project_ratio × freeze_probability
at_risk_pipeline_count = pipeline_count × discretionary_project_ratio × freeze_probability

// 進行中案件の延期リスク（完了間近は影響小）
active_delay_risk = active_projects_revenue_oku × discretionary_project_ratio × freeze_probability × 0.5
```

### Step 4: 自社コスト・キャッシュフローへの影響

```
// 案件凍結時の固定費負担リスク
idle_months_risk = at_risk_pipeline_count × avg_project_duration_months × 0.3
idle_cost = idle_months_risk × monthly_fixed_cost_oku / active_projects_count

// 自社借入コスト増
annual_interest_increase = (active_projects_revenue_oku × own_debt_ratio × rate_change_bps / 10000)
```

### Step 5: 総合インパクトスコア算出

```
severity_weight:
  critical = 1.0, high = 0.7, medium = 0.4, low = 0.1

// 保守案件比率が高いほど耐性がある
resilience_factor = 1.0 - (maintenance_project_ratio × 0.6)

outlook_multiplier:
  further_tightening = 1.4
  neutral = 1.0
  easing = 0.6

raw_score = (severity_weight × 0.25 + freeze_probability × 2.0 × 0.35 + discretionary_project_ratio × resilience_factor × 0.25 + weighted_sensitivity × 0.15) × outlook_multiplier × 100

impact_score = clamp(raw_score, 0, 100)

IF policy_direction == "rate_cut" THEN
    impact_direction = "positive"
ELSE
    impact_direction = "negative"
```

### Step 6: インパクトレベル判定

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
| `impact_direction` | enum | `negative` / `positive` |
| `at_risk_pipeline_oku` | float | 凍結リスクパイプライン額（億円） |
| `at_risk_pipeline_count` | int | 凍結リスク案件数 |
| `active_delay_risk_oku` | float | 進行中案件延期リスク額（億円） |
| `idle_cost_oku` | float | 遊休コストリスク（億円） |
| `recommended_actions` | list[string] | 推奨アクションリスト |
| `risk_summary` | string | リスクサマリー文（自然言語） |

### 推奨アクション分岐

| レベル | 推奨アクション（利上げ時） |
|---|---|
| critical | パイプライン全案件の緊急リスク仕分け（Go/Hold/Cancel）、クライアントCIO/CFOへの即時コンタクト（投資継続の意思確認）、コスト削減型提案への緊急転換、技術者再配置計画の策定、経営層へのエスカレーション |
| high | 裁量的投資案件のフェーズ分割提案（投資分散）、ROI訴求の強化資料作成、保守・運用案件の拡大営業、公共セクター案件の獲得強化 |
| medium | クライアント投資計画のヒアリング強化、コスト削減・効率化テーマの提案準備、案件ポートフォリオの業種分散モニタリング |
| low | 定期レポートへの記載、営業戦略会議でのアジェンダ追加 |

| レベル | 推奨アクション（利下げ時） |
|---|---|
| high (positive) | 凍結案件の再提案・復活営業、大型DX投資案件の積極提案、採用・教育投資の前倒し |
| medium (positive) | クライアントへの投資拡大提案、新規領域（AI・クラウド移行）の案件創出 |

## 参照データソース（Fabric テーブル）

| テーブル名 | 説明 | 主要カラム |
|---|---|---|
| `fact_pipeline_deals` | パイプライン案件 | `deal_id`, `client_id`, `amount_oku`, `stage`, `probability`, `expected_close_date`, `project_type` |
| `fact_project_status` | プロジェクト進捗 | `project_id`, `client_id`, `status`, `start_date`, `planned_end_date`, `revenue_oku`, `completion_rate` |
| `fact_project_freeze_history` | 案件凍結・延期履歴 | `project_id`, `freeze_date`, `reason`, `duration_months`, `resumed_flag` |
| `dim_clients` | 顧客マスタ | `client_id`, `name`, `industry`, `tier`, `annual_it_budget_oku`, `investment_stance` |
| `fact_interest_rate_history` | 金利推移履歴 | `date`, `rate_type`, `rate_value`, `change_bps` |
| `agg_industry_it_spending` | 業種別IT投資動向 | `quarter`, `industry`, `spending_index`, `yoy_change`, `forecast` |
| `fact_resource_utilization` | 技術者稼働実績 | `month`, `domain`, `allocated_count`, `idle_count`, `utilization_rate` |
