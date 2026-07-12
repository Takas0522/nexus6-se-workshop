# スキル: 光ファイバー網整備政策によるネットワークサービス事業機会分析

## 概要

国内通信インフラ整備に関する政策ニュース（光ファイバー網拡充、5G展開、補助金制度等）を分析し、当社ネットワークサービス事業のサービスエリア拡大、新規顧客獲得、補助金活用、ローカル5G参入機会への影響を判断するスキル。整備対象地域と当社サービス提供可能エリアを照合し、事業拡大の規模・収益性・投資回収期間を算出する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（policy_infra） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（予算額、対象世帯数、補助率、対象事業者、5Gガイドライン変更、完了目標日等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| current_service_areas | object[] | 現行サービス提供エリア一覧 | sqldb_network_01.circuits + エリアマスタ |
| total_circuits_active | int | 有効回線数 | sqldb_network_01.circuits (status='active') |
| total_customers_japan | int | 国内顧客数 | sqldb_network_01.customers (service_region LIKE 'JP%') |
| customers_by_region | object[] | 地域別顧客分布 | sqldb_network_01.customers GROUP BY service_region |
| revenue_by_region | object[] | 地域別月間売上 | sqldb_network_01.transactions GROUP BY region |
| contracts_expiring_12m | object[] | 12ヶ月以内に更新予定の契約 | sqldb_network_01.contracts |
| arpu_by_service_type | object[] | サービス種別別ARPU | sqldb_network_01.transactions + contracts |
| local_5g_license_status | string | ローカル5G免許取得状況 | 免許管理台帳 |
| capex_budget_remaining | decimal | 設備投資残予算（円） | 予算管理システム |
| underserved_area_inquiries | int | 未整備地域からのサービス問合せ数 | CRMデータ |
| partnership_carriers | object[] | 提携通信キャリア一覧 | 契約管理システム |

## 判断ロジック

### Step 1: 事業機会規模の算出

```
newly_covered_households = extracted_entities.target_households  -- 520,000世帯
subsidy_rate = extracted_entities.subsidy_rate  -- 0.70 (70%)
target_completion_date = extracted_entities.completion_date  -- 2028年度末

-- 新規整備地域での事業所・法人顧客推定
estimated_businesses_in_area = newly_covered_households * 0.08  -- 世帯数の8%を事業所と推定（地方は比率高め）
-- 520,000 * 0.08 = 41,600事業所

-- 当社ネットワークサービスの市場浸透率適用
current_penetration_rate = total_customers_japan / total_addressable_market
potential_new_customers = estimated_businesses_in_area * current_penetration_rate

-- サービス種別別の獲得見込み
potential_fiber_customers = potential_new_customers * 0.60  -- 光回線サービス
potential_vpn_customers = potential_new_customers * 0.25  -- VPN/閉域網
potential_cloud_connect_customers = potential_new_customers * 0.15  -- クラウド接続
```

### Step 2: 収益・投資分析

```
-- 潜在年間売上
avg_arpu_fiber = arpu_by_service_type.find('fiber').value
avg_arpu_vpn = arpu_by_service_type.find('vpn').value
avg_arpu_cloud = arpu_by_service_type.find('cloud_connect').value

potential_annual_revenue = (potential_fiber_customers * avg_arpu_fiber * 12)
                         + (potential_vpn_customers * avg_arpu_vpn * 12)
                         + (potential_cloud_connect_customers * avg_arpu_cloud * 12)

-- 設備投資見込み（補助金考慮）
infrastructure_cost_per_customer = 800_000  -- 円（地方部1顧客あたり敷設コスト）
gross_investment = potential_new_customers * infrastructure_cost_per_customer
net_investment = gross_investment * (1 - subsidy_rate)  -- 補助金70%適用後

-- ROI（投資回収期間）
roi_months = net_investment / (potential_annual_revenue / 12)

-- ローカル5G事業機会
IF extracted_entities.includes_5g_guideline_change = TRUE THEN
    local_5g_opportunity_areas = extract_5g_candidate_areas(underserved_areas)
    local_5g_potential_revenue = local_5g_opportunity_areas.count * 5_000_000  -- エリアあたり年間500万円
ELSE
    local_5g_potential_revenue = 0
END IF

total_revenue_opportunity = potential_annual_revenue + local_5g_potential_revenue
```

### Step 3: 補助金活用可能性評価

```
-- 当社が補助金対象事業者に該当するか
IF partnership_carriers.any(name IN extracted_entities.target_carriers) THEN
    subsidy_eligibility = 'partner_eligible'  -- パートナー経由で補助対象
ELIF company_is_registered_carrier = TRUE THEN
    subsidy_eligibility = 'directly_eligible'
ELSE
    subsidy_eligibility = 'not_eligible'
END IF

-- 補助金申請による投資削減額
IF subsidy_eligibility IN ('directly_eligible', 'partner_eligible') THEN
    subsidy_amount = gross_investment * subsidy_rate
ELSE
    subsidy_amount = 0
    net_investment = gross_investment  -- 補助なしの場合
END IF
```

### Step 4: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| total_revenue_opportunity >= 10億円 AND subsidy_eligibility != 'not_eligible' AND roi_months <= 24 | 80.0–100.0 | critical |
| total_revenue_opportunity >= 5億円 AND roi_months <= 36 | 60.0–79.9 | high |
| total_revenue_opportunity >= 1億円 | 40.0–59.9 | medium |
| total_revenue_opportunity >= 3000万円 | 20.0–39.9 | low |
| total_revenue_opportunity < 3000万円 | 5.0–19.9 | low |

```
base_score = lookup_from_table(total_revenue_opportunity, roi_months, subsidy_eligibility)

-- 調整係数
IF underserved_area_inquiries >= 50 THEN
    -- 既に問合せが多い場合、需要が顕在化している
    adjustment = +10.0
ELIF underserved_area_inquiries >= 20 THEN
    adjustment = +5.0
ELSE
    adjustment = +0.0
END IF

IF extracted_entities.includes_5g_guideline_change = TRUE THEN
    IF local_5g_license_status = 'acquired' THEN
        adjustment = adjustment + 8.0  -- 既にライセンス保有で即座に展開可能
    ELIF local_5g_license_status = 'applying' THEN
        adjustment = adjustment + 4.0
    END IF
END IF

IF subsidy_eligibility = 'directly_eligible' THEN
    adjustment = adjustment + 6.0
ELIF subsidy_eligibility = 'partner_eligible' THEN
    adjustment = adjustment + 3.0
END IF

-- 競合動向
IF partnership_carriers.count >= 2 THEN
    -- 複数キャリアと提携している場合、機会活用の柔軟性高い
    adjustment = adjustment + 3.0
END IF

impact_score = CLAMP(base_score + adjustment, 0.0, 100.0)
```

## 出力

### インパクト診断結果

| フィールド | 型 | 説明 |
|-----------|-----|------|
| impact_score | decimal(5,2) | インパクトスコア（0.00–100.00） |
| impact_level | string | インパクトレベル（critical / high / medium / low） |
| potential_new_customers | int | 潜在新規顧客数 |
| potential_annual_revenue_jpy | decimal | 潜在年間売上（円） |
| local_5g_potential_revenue_jpy | decimal | ローカル5G事業機会（円） |
| total_revenue_opportunity_jpy | decimal | 総収益機会（円） |
| gross_investment_jpy | decimal | 総投資額（円） |
| net_investment_jpy | decimal | 補助金控除後投資額（円） |
| subsidy_amount_jpy | decimal | 補助金見込み額（円） |
| subsidy_eligibility | string | 補助金適格性 |
| roi_months | int | 投資回収期間（月） |
| completion_timeline | string | インフラ整備完了見込み時期 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P0 | 補助金申請要件の詳細確認および対応計画策定 | 事業企画部 + 法務 | 2週間以内 |
| P0 | 未整備地域における当社サービス展開可能性の地域別調査 | サービス企画部 | 3週間以内 |
| P1 | 提携通信キャリアとの共同事業スキーム検討 | 事業開発部 | 1ヶ月以内 |
| P1 | ローカル5Gガイドライン改定内容の精査と参入計画策定 | ネットワーク技術部 | 1ヶ月以内 |
| P1 | 中山間地域・離島部の潜在顧客数・収益性シミュレーション | 経営企画部 | 6週間以内 |
| P2 | 設備投資計画の見直し（補助金活用前提での予算再配分） | 設備計画部 + 財務 | 2ヶ月以内 |
| P2 | 競合動向調査（他社の補助金活用・エリア拡大計画把握） | マーケティング部 | 2ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'critical' | Teams (即時) | ネットワーク事業本部長, 経営企画部長, CFO |
| impact_level = 'high' | Teams (即時) | 事業企画部長, サービス企画部長, 設備計画部長 |
| impact_level = 'medium' | Teams (当日) | ネットワーク事業部マネージャー |
| impact_level = 'low' | Email (週次) | ネットワーク事業部メンバー |
| subsidy_eligibility != 'not_eligible' | Teams (即時) | 財務部長, 事業企画部長 |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_network_gold | circuits | 現行サービスエリア・回線情報 |
| lh_network_gold | customers | 地域別顧客数・サービス種別分布 |
| lh_network_gold | contracts | 契約情報・提携キャリア・更新予定 |
| lh_network_gold | transactions | 地域別売上・サービス別ARPU |
| lh_common_gold | unified_customers | 統合顧客の地域属性 |
| lh_common_gold | customer_segments | 地方企業セグメント・潜在需要 |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去の政策系インパクト分析 |

### 外部参照

| ソース | 用途 |
|--------|------|
| 総務省 補助金公募要領 | 申請要件・対象事業者・スケジュール |
| 総務省 ローカル5Gガイドライン | 改定内容・参入要件の詳細 |
| 総務省 情報通信統計データベース | 地域別ブロードバンド整備状況 |
| 国勢調査データ（e-Stat） | 未整備地域の事業所数・産業構成 |
| 電気通信事業者協会（TCA） | 市場シェア・競合動向 |
