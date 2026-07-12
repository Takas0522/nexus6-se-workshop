# スキル: ネットワーク運用AI機能に対する規制インパクト分析

## 概要

AI規制に関するニュースを分析し、当社ネットワークサービス事業で運用しているAI機能（異常検知、トラフィック最適化、障害予測、自動ルーティング等）がGPAI規制の対象となるか判断するスキル。ネットワーク運用基盤におけるAI利用状況と規制要件を照合し、運用継続リスクとコンプライアンス対応コストを算出する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（regulation） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（規制名、対象範囲、制裁金、施行日、GPAI定義、適用除外等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| ai_ops_systems | object[] | AI活用運用システム一覧（種別、モデルタイプ、用途） | インフラ台帳 |
| eu_circuits_count | int | EU域内の有効回線数 | sqldb_network_01.circuits (endpoint IN EU27) |
| eu_customer_count | int | EU域内顧客数 | sqldb_network_01.customers (service_region IN EU27) |
| eu_revenue_annual | decimal | EU域内年間売上高（EUR） | sqldb_network_01.transactions (集計) |
| global_revenue_annual | decimal | グローバル年間売上高（EUR） | sqldb_network_01.transactions (集計) |
| third_party_ai_vendors | object[] | サードパーティAIベンダー契約一覧 | sqldb_network_01.contracts |
| network_availability_sla | float | ネットワーク稼働率SLA（%） | 契約管理システム |
| ai_dependent_sla_customers | int | AI運用に依存するSLA顧客数 | sqldb_network_01.contracts + 技術台帳 |

## 判断ロジック

### Step 1: AI運用システムの規制対象分類

```
FOR EACH system IN ai_ops_systems:
    IF system.model_type = 'gpai' AND system.scope = 'eu_traffic' THEN
        -- EU域内トラフィックを処理するGPAIモデル
        system.regulation_status = 'directly_regulated'
    ELIF system.model_type = 'gpai' AND system.scope = 'global' THEN
        -- グローバルだがEUトラフィックも含む
        system.regulation_status = 'potentially_regulated'
    ELIF system.uses_third_party_gpai = TRUE THEN
        -- サードパーティGPAI利用（ベンダー責任だが監視要）
        system.regulation_status = 'vendor_dependent'
    ELIF system.model_type = 'narrow_ai' THEN
        -- 特化型AI（異常検知ルールベース等）は対象外の可能性高
        system.regulation_status = 'likely_exempt'
    END IF
END FOR

directly_regulated_systems = count(systems WHERE regulation_status = 'directly_regulated')
potentially_regulated_systems = count(systems WHERE regulation_status = 'potentially_regulated')
vendor_dependent_systems = count(systems WHERE regulation_status = 'vendor_dependent')
```

### Step 2: 運用継続リスク評価

```
-- AI停止時のSLAリスク
IF directly_regulated_systems > 0 THEN
    -- 規制対応完了までAI機能を停止した場合のSLA影響
    ai_coverage_ratio = ai_dependent_sla_customers / total_eu_customers
    sla_breach_risk = ai_coverage_ratio * (1 - fallback_manual_capability)
    -- fallback_manual_capability: AI無しでSLA維持できる割合（0-1）
END IF

-- 制裁金リスク
max_penalty_revenue = global_revenue_annual * 0.03
max_penalty_fixed = 35_000_000  -- EUR
potential_penalty = MAX(max_penalty_revenue, max_penalty_fixed)

-- コンプライアンス対応コスト（ネットワーク運用AI特有）
compliance_cost_per_system = 500_000  -- EUR（システムあたり、文書化+監査+改修）
total_compliance_cost = directly_regulated_systems * compliance_cost_per_system
                      + potentially_regulated_systems * compliance_cost_per_system * 0.5
```

### Step 3: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| directly_regulated_systems >= 3 AND sla_breach_risk >= 0.1 | 60.0–80.0 | high |
| directly_regulated_systems >= 1 AND eu_customer_count >= 100 | 40.0–59.9 | medium |
| potentially_regulated_systems >= 1 OR vendor_dependent_systems >= 2 | 20.0–39.9 | low |
| すべて likely_exempt | 5.0–19.9 | low |

```
base_score = lookup_from_table(directly_regulated_systems, sla_breach_risk)

-- 調整係数
eu_revenue_ratio = eu_revenue_annual / global_revenue_annual
IF eu_revenue_ratio >= 0.20 THEN
    adjustment = +8.0
ELIF eu_revenue_ratio >= 0.10 THEN
    adjustment = +4.0
ELSE
    adjustment = +0.0
END IF

-- SLA依存度による調整
IF ai_dependent_sla_customers >= 50 THEN
    adjustment = adjustment + 7.0
ELIF ai_dependent_sla_customers >= 20 THEN
    adjustment = adjustment + 3.0
END IF

-- サードパーティベンダーのコンプライアンス不明リスク
unknown_vendor_count = third_party_ai_vendors.filter(compliance_status = 'unknown').count
IF unknown_vendor_count >= 2 THEN
    adjustment = adjustment + 5.0
END IF

impact_score = CLAMP(base_score + adjustment, 0.0, 100.0)
```

### Step 4: 対応優先度判定

```
enforcement_date = extracted_entities.enforcement_date  -- 2027-01-01
days_until_enforcement = (enforcement_date - TODAY).days

-- ネットワーク運用AIは24/365稼働のため、切替に長期計画が必要
IF days_until_enforcement <= 180 AND directly_regulated_systems >= 1 THEN
    urgency = 'immediate'
ELIF days_until_enforcement <= 270 THEN
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
| impact_level | string | インパクトレベル（high / medium / low） |
| urgency | string | 緊急度（immediate / high / normal / low） |
| directly_regulated_systems | int | 直接規制対象システム数 |
| potentially_regulated_systems | int | 潜在的規制対象システム数 |
| sla_breach_risk | float | SLA違反リスク（0–1） |
| potential_penalty_eur | decimal | 最大制裁金額（EUR） |
| total_compliance_cost_eur | decimal | 推定コンプライアンスコスト（EUR） |
| ai_dependent_sla_customers | int | AI依存SLA顧客数 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P2 | ネットワーク運用AIシステムのGPAI該当性精査 | ネットワーク技術部 | 3週間以内 |
| P2 | AI停止時のフォールバック運用手順の確認・文書化 | NOC（運用センター） | 1ヶ月以内 |
| P2 | サードパーティAIベンダーの規制対応状況ヒアリング | 調達部 | 1ヶ月以内 |
| P3 | 異常検知・トラフィック最適化AIの学習データ出所文書化 | データエンジニアリング | 2ヶ月以内 |
| P3 | EU域内ネットワーク運用のAI依存度低減策の検討 | ネットワーク設計部 | 3ヶ月以内 |
| P3 | 法務部門と連携しコンプライアンス対応ロードマップ策定 | 法務 + 技術 | 3ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'high' AND urgency = 'immediate' | Teams (即時) | ネットワーク事業本部長, 法務部長, CTO |
| impact_level = 'high' | Teams (当日) | NOCマネージャー, ネットワーク技術部長 |
| impact_level = 'medium' | Email (当日) | ネットワーク事業部マネージャー |
| impact_level = 'low' | Email (週次) | ネットワーク事業部メンバー（参考情報） |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_network_gold | customers | EU域内顧客数・地域分布 |
| lh_network_gold | contracts | EU域内契約・SLA情報・AI依存度 |
| lh_network_gold | transactions | EU域内売上高・グローバル売上高集計 |
| lh_network_gold | circuits | EU域内回線数・AI管理対象回線 |
| lh_common_gold | unified_customers | 統合顧客情報との突合 |
| lh_common_gold | domain_id_mappings | 顧客ID間マッピング |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去の規制系インパクト分析 |

### 外部参照

| ソース | 用途 |
|--------|------|
| EU AI Act 条文データベース | GPAI定義・通信インフラ分野の適用範囲 |
| 欧州AI局公告API | 通信事業者向けガイダンス |
| ETSI（欧州電気通信標準化機構） | 通信分野AI標準・コンプライアンス要件 |
| 為替レートAPI (ECB) | EUR建て金額のJPY変換 |
