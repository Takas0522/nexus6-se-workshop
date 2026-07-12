# スキル: ゲーム内AI機能に対する規制インパクト分析

## 概要

AI規制に関するニュースを分析し、当社ゲーム事業で利用しているAI機能（NPC対話生成、プレイヤー行動予測、レコメンデーション、プロシージャル生成等）がGPAI規制の対象となるか判断するスキル。EU域内向けタイトルのAI機能利用状況と規制要件を照合し、コンプライアンスリスクと対応コストを算出する。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| article_id | string | ニュース記事ID |
| title | string | 記事タイトル |
| summary | string | 記事要約 |
| category | string | カテゴリ（regulation） |
| published_at | datetime | 配信日時 |
| extracted_entities | object[] | 抽出エンティティ（規制名、対象範囲、制裁金、施行日、GPAI定義等） |

### 業務データ

| パラメータ | 型 | 説明 | ソース |
|-----------|-----|------|--------|
| eu_active_titles | object[] | EU域内配信中のゲームタイトル一覧 | sqldb_game_01.game_titles + 配信地域マスタ |
| ai_features_by_title | object[] | タイトル別AI機能利用一覧 | 技術台帳 |
| eu_mau | int | EU域内MAU | sqldb_game_01.customers (country_code IN EU27) |
| eu_revenue_annual | decimal | EU域内年間売上高（EUR） | sqldb_game_01.transactions (集計) |
| global_revenue_annual | decimal | グローバル年間売上高（EUR） | sqldb_game_01.transactions (集計) |
| gpai_models_used | object[] | 利用中のGPAIモデル一覧（自社開発/サードパーティ） | 技術台帳 |
| third_party_ai_providers | object[] | サードパーティAIプロバイダ契約情報 | sqldb_game_01.contracts |
| eu_player_count | int | EU域内プレイヤー数 | sqldb_game_01.customers |

## 判断ロジック

### Step 1: AI機能の規制対象判定

```
FOR EACH title IN eu_active_titles:
    title_ai_features = ai_features_by_title.filter(title_id = title.id)

    FOR EACH feature IN title_ai_features:
        IF feature.model_type = 'gpai' THEN
            -- GPAIモデルを直接利用している場合
            feature.regulation_status = 'directly_regulated'
        ELIF feature.uses_third_party_gpai = TRUE THEN
            -- サードパーティGPAI経由の場合（プロバイダ責任だが監視必要）
            feature.regulation_status = 'indirectly_regulated'
        ELIF feature.model_type = 'narrow_ai' THEN
            -- 特化型AI（レコメンデーション等）は対象外の可能性
            feature.regulation_status = 'likely_exempt'
        END IF
    END FOR
END FOR

directly_regulated_count = count(features WHERE regulation_status = 'directly_regulated')
indirectly_regulated_count = count(features WHERE regulation_status = 'indirectly_regulated')
affected_title_count = count(titles WHERE any feature is regulated)
```

### Step 2: 財務リスク算出

```
-- 制裁金リスク（ゲーム事業単体ではなく企業グループ全体売上が基準）
max_penalty_revenue = global_revenue_annual * 0.03
max_penalty_fixed = 35_000_000  -- EUR
potential_penalty = MAX(max_penalty_revenue, max_penalty_fixed)

-- 対応コスト算出
compliance_cost_per_gpai_feature = 300_000  -- EUR（機能あたり年間コンプライアンスコスト）
compliance_cost_per_title = 150_000  -- EUR（タイトルあたり透明性表示対応コスト）

total_compliance_cost = (directly_regulated_count * compliance_cost_per_gpai_feature)
                      + (affected_title_count * compliance_cost_per_title)
```

### Step 3: インパクトスコア算出

| 条件 | スコア範囲 | レベル |
|------|-----------|--------|
| directly_regulated_count >= 5 AND affected_title_count >= 3 | 65.0–85.0 | high |
| directly_regulated_count >= 2 OR affected_title_count >= 2 | 40.0–64.9 | medium |
| indirectly_regulated_count >= 1 AND directly_regulated_count = 0 | 20.0–39.9 | low |
| すべて likely_exempt | 5.0–19.9 | low |

```
base_score = lookup_from_table(directly_regulated_count, affected_title_count)

-- 調整係数
IF eu_revenue_annual / global_revenue_annual >= 0.25 THEN
    -- EU売上比率が高い場合、リスク重大
    adjustment = +10.0
ELIF eu_revenue_annual / global_revenue_annual >= 0.10 THEN
    adjustment = +5.0
ELSE
    adjustment = +0.0
END IF

-- サードパーティ依存リスク
IF third_party_ai_providers.any(provider.compliance_status = 'unknown') THEN
    adjustment = adjustment + 5.0
END IF

-- 対象タイトルにライブサービス（運営型）が含まれる場合
live_service_affected = eu_active_titles.filter(type = 'live_service' AND is_regulated)
IF live_service_affected.count >= 1 THEN
    adjustment = adjustment + 7.0  -- ライブサービスは即座に影響
END IF

impact_score = CLAMP(base_score + adjustment, 0.0, 100.0)
```

### Step 4: 対応緊急度判定

```
enforcement_date = extracted_entities.enforcement_date  -- 2027-01-01
days_until_enforcement = (enforcement_date - TODAY).days

-- ゲームタイトルのアップデートサイクルを考慮
IF days_until_enforcement <= 120 THEN
    urgency = 'immediate'  -- 次回メジャーアップデートまでに対応必要
ELIF days_until_enforcement <= 240 THEN
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
| directly_regulated_count | int | 直接規制対象AI機能数 |
| indirectly_regulated_count | int | 間接規制対象AI機能数 |
| affected_title_count | int | 影響を受けるタイトル数 |
| potential_penalty_eur | decimal | 最大制裁金額（EUR） |
| total_compliance_cost_eur | decimal | 推定コンプライアンス総コスト（EUR） |
| eu_player_impact_count | int | 影響を受けるEUプレイヤー数 |

### 推奨アクション

| 優先度 | アクション | 担当 | 期限目安 |
|--------|-----------|------|----------|
| P1 | EU配信タイトルのAI機能利用状況の棚卸し・GPAI該当性判定 | 技術部門 | 2週間以内 |
| P1 | サードパーティAIプロバイダの規制対応状況をヒアリング | 調達・法務 | 3週間以内 |
| P2 | AI生成コンテンツの識別表示機能の設計・実装計画策定 | 開発チーム | 1ヶ月以内 |
| P2 | EU向けプライバシーポリシー・AI利用開示文書の更新 | 法務部 | 2ヶ月以内 |
| P2 | NPC対話生成モデルの学習データ著作権処理状況確認 | データガバナンス | 2ヶ月以内 |
| P3 | 規制対象外の代替AI手法（ルールベース等）への切替可否調査 | 技術部門 | 3ヶ月以内 |

### 通知設定

| 条件 | チャネル | 宛先 |
|------|---------|------|
| impact_level = 'high' AND urgency = 'immediate' | Teams (即時) | ゲーム事業本部長, 法務部長 |
| impact_level = 'high' | Teams (当日) | プロデューサー全員, 技術部門マネージャー |
| impact_level = 'medium' | Email (当日) | 各タイトルプロデューサー |
| impact_level = 'low' | Email (週次) | ゲーム事業部メンバー |

## 参照データソース

### Fabric テーブル

| レイクハウス | テーブル | 用途 |
|-------------|---------|------|
| lh_game_gold | game_titles | EU配信タイトル一覧・ジャンル・配信地域 |
| lh_game_gold | customers | EU域内プレイヤー数・地域分布 |
| lh_game_gold | transactions | EU域内売上高・グローバル売上高の集計 |
| lh_game_gold | inventory | AI生成アイテムの流通状況 |
| lh_game_gold | contracts | サードパーティAIプロバイダ契約情報 |
| lh_common_gold | unified_customers | 統合顧客情報との突合 |
| lh_news_gold | news_articles | ニュース記事メタデータ |
| lh_news_gold | impact_analyses | 過去の規制系インパクト分析 |

### 外部参照

| ソース | 用途 |
|--------|------|
| EU AI Act 条文データベース | GPAI定義・ゲーム分野適用範囲の確認 |
| 欧州AI局公告API | 施行スケジュール・業種別ガイダンス |
| PEGI / USK レーティングDB | タイトル別レーティング・AI機能開示要件 |
| 為替レートAPI (ECB) | EUR建て金額のJPY変換 |
