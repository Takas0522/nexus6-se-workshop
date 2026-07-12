# スキル: AI規制コンプライアンスインパクト分析

## 概要

EU AI規制法（AI Act）等の国際的なAI規制動向に関するニュースを分析し、当社AIエージェント事業への法的・財務的インパクトを判断するスキル。規制対象となるGPAIモデルの保有状況、EU域内顧客比率、売上規模から制裁金リスクと対応コストを算出し、コンプライアンス対応の優先度を決定する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（regulation / technology / policy_infra） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（規制名、制裁金額、対象範囲、施行日等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| eu_customer_count | int | EU域内の有効顧客数 | sqldb_ai_agent_01.customers (country_code IN EU27) |
| eu_customer_ratio | float | 全顧客に対するEU顧客比率 | 算出値 |
| eu_annual_revenue | decimal | EU域内年間売上高（EUR） | sqldb_ai_agent_01.transactions (集計) |
| global_annual_revenue | decimal | グローバル年間売上高（EUR） | sqldb_ai_agent_01.transactions (集計) |
| gpai_model_count | int | 提供中のGPAIモデル数 | sqldb_ai_agent_01.products (category IN ('llm','vision','speech')) |
| active_contracts_eu | int | EU域内の有効契約数 | sqldb_ai_agent_01.contracts (status='active') |
| current_compliance_status | string | 現行コンプライアンス対応状況 | 手動入力 or 外部システム連携 |

## 判断ロジック

### Step 1: 規制適用判定

```
IF gpai_model_count > 0 AND eu_customer_count > 0 THEN
    regulation_applicable = TRUE
ELSE
    regulation_applicable = FALSE
    → impact_score = 5.0 (情報共有レベル)
    → EXIT
END IF
```

### Step 2: 財務リスク算出

```
max_penalty_revenue = global_annual_revenue * 0.03
max_penalty_fixed = 35_000_000  -- EUR
potential_penalty = MAX(max_penalty_revenue, max_penalty_fixed)

estimated_compliance_cost = gpai_model_count * 1_200_000  -- Forrester試算ベース: モデルあたり年間120万EUR
```

### Step 3: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| eu_customer_ratio >= 30% AND potential_penalty >= 100M EUR | 85.0–100.0 | critical |
| eu_customer_ratio >= 15% AND potential_penalty >= 50M EUR | 65.0–84.9 | high |
| eu_customer_ratio >= 5% AND regulation_applicable = TRUE | 45.0–64.9 | medium |
| eu_customer_ratio < 5% AND regulation_applicable = TRUE | 20.0–44.9 | low |

```
base_score = lookup_from_table(eu_customer_ratio, potential_penalty)

-- 調整係数
IF current_compliance_status = 'not_started' THEN
    adjustment = +10.0
ELIF current_compliance_status = 'in_progress' THEN
    adjustment = +0.0
ELIF current_compliance_status = 'completed' THEN
    adjustment = -15.0
END IF

impact_score = CLAMP(base_score + adjustment, 0.0, 100.0)
```

### Step 4: 緊急度判定

```
enforcement_date = extracted_entities.enforcement_date  -- 2027-01-01 (本格執行)
days_until_enforcement = (enforcement_date - TODAY).days

IF days_until_enforcement <= 90 THEN
    urgency = 'immediate'
ELIF days_until_enforcement <= 180 THEN
    urgency = 'high'
ELIF days_until_enforcement <= 365 THEN
    urgency = 'normal'
ELSE
    urgency = 'low'
END IF
```

## 出力

### インパクト診断結果

| フィールド | 型 | 説明 |
|-----------|-----|------|
| impact_score | decimal(5,2) | インパクトスコア（0.00–100.00） |
| impact_level | string | インパクトレベル（critical / high / medium / low / none） |
| urgency | string | 緊急度（immediate / high / normal / low） |
| potential_penalty_eur | decimal | 最大制裁金額（EUR） |
| estimated_compliance_cost_eur | decimal | 推定コンプライアンスコスト（EUR） |
| affected_customer_count | int | 影響を受ける顧客数 |
| affected_contract_count | int | 影響を受ける契約数 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P0 | 欧州AI局への報告義務対象モデルの特定・一覧化 | AI基盤チーム | 2週間以内 |
| P0 | 学習データの著作権処理状況の棚卸し | データガバナンスチーム | 1ヶ月以内 |
| P1 | 安全性評価レポート作成プロセスの設計 | リスク管理部 | 2ヶ月以内 |
| P1 | EU域内顧客向け契約条項の見直し | 法務部 | 2ヶ月以内 |
| P2 | コンプライアンスコスト予算の確保・経営報告 | 経営企画部 | 3ヶ月以内 |
| P2 | 第三者安全性監査機関の選定・契約 | リスク管理部 | 4ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'critical' | Teams (即時) | AI事業本部長, 法務部長, CTO |
| impact_level = 'high' | Teams (即時) | AI事業部マネージャー, 法務担当 |
| impact_level = 'medium' | Teams (当日) | AI事業部メンバー |
| impact_level = 'low' | Email (週次) | AI事業部メンバー |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_ai_agent_gold | customers | EU域内顧客数・比率の算出 |
| lh_ai_agent_gold | contracts | EU域内有効契約の特定 |
| lh_ai_agent_gold | transactions | EU域内売上高・グローバル売上高の集計 |
| lh_ai_agent_gold | products | GPAIモデル一覧・規制対象判定 |
| lh_common_gold | unified_customers | 統合顧客情報との突合 |
| lh_common_gold | domain_id_mappings | 顧客ID間マッピング |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去のインパクト分析結果（トレンド比較用） |

### 外部参照

| ソース | 用途 |
|--------|------|
| EU AI Act 条文データベース | 規制要件の最新状態確認 |
| 欧州AI局公告API | 施行スケジュール・ガイダンス更新 |
| 為替レートAPI (ECB) | EUR建て金額のJPY変換 |
