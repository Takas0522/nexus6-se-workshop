# SNS事業：デジタルサービス法（DSA）準拠によるプラットフォーム規制インパクト判断スキル

## 概要

欧州デジタルサービス法（DSA）によるMeta・TikTok等の大規模プラットフォームへの追加義務（アルゴリズム透明性・未成年保護・ターゲティング広告制限）が、自社SNS事業に与える直接的・波及的インパクトを判断するスキル。日本国内の同種規制の検討状況も踏まえ、レコメンドアルゴリズムの透明性対応、広告収益モデルの見直し、未成年ユーザー保護施策の要否とコスト影響を多角的に評価する。本スキルはSNS事業にとって**直接的かつ高インパクト**なシナリオを扱う。

## 入力パラメータ

### ニュース情報

| パラメータ | 型 | 説明 |
|---|---|---|
| `news_category` | string | ニュースカテゴリ（`regulation`） |
| `regulation_name` | string | 規制名称（例: `DSA`） |
| `target_platforms` | list[string] | 規制対象プラットフォーム（例: `["Meta", "TikTok"]`） |
| `compliance_deadline_days` | int | 準拠期限（日数） |
| `penalty_max_revenue_percent` | float | 最大制裁金率（全世界売上比%） |
| `minor_age_threshold` | int | 未成年の年齢閾値 |
| `algorithm_transparency_required` | bool | アルゴリズム開示義務の有無 |
| `behavioral_ad_ban_minors` | bool | 未成年への行動ターゲティング禁止の有無 |
| `eu_social_ad_market_eur_billion` | float | 欧州SNS広告市場規模（十億EUR） |
| `domestic_regulation_status` | string | 国内同種規制の検討状況（`announced` / `under_review` / `none`） |

### 業務データ

| パラメータ | 型 | 説明 |
|---|---|---|
| `total_mau` | int | 月間アクティブユーザー数 |
| `minor_user_count` | int | 18歳未満のユーザー数 |
| `minor_user_ratio_percent` | float | 未成年ユーザー比率（%） |
| `monthly_ad_revenue_jpy` | int | 月間広告収益（円） |
| `behavioral_targeting_ad_ratio_percent` | float | 行動ターゲティング広告の比率（%） |
| `minor_targeted_ad_revenue_jpy` | int | 未成年向け行動ターゲティング広告の月間収益（円） |
| `recommendation_algorithm_type` | string | レコメンドアルゴリズムの種別（例: `collaborative_filtering`, `deep_learning`, `hybrid`） |
| `algorithm_documentation_level` | string | アルゴリズム文書化レベル（`full` / `partial` / `minimal` / `none`） |
| `content_moderation_staff_count` | int | コンテンツモデレーション担当者数 |
| `age_verification_method` | string | 年齢確認方法（`self_declaration` / `document_verification` / `ai_estimation` / `none`） |
| `advertiser_count` | int | 広告主数 |
| `top10_advertiser_revenue_ratio_percent` | float | 上位10広告主の収益比率（%） |
| `eu_user_count` | int | 欧州域内ユーザー数（0の場合も国内規制波及を評価） |
| `privacy_compliance_certifications` | list[string] | 取得済みプライバシー認証（例: `["ISMS", "Pmark"]`） |
| `data_retention_policy_exists` | bool | データ保持ポリシーの有無 |

## 判断ロジック

### Step 1: 直接規制対象の判定

```
-- 自社が直接規制対象となるかの判定
IF eu_user_count >= 45_000_000 THEN
    -- EU域内MAU 4500万以上は「超大規模プラットフォーム」（VLOP）指定
    direct_regulation_target = true
    regulation_urgency = "immediate"
ELSE IF eu_user_count >= 10_000_000 THEN
    -- DSAの一般義務は適用される可能性
    direct_regulation_target = true
    regulation_urgency = "near_term"
ELSE IF eu_user_count > 0 THEN
    direct_regulation_target = false
    regulation_urgency = "monitoring"
ELSE
    -- 欧州ユーザーなし → 国内波及のみ評価
    direct_regulation_target = false
    regulation_urgency = "domestic_spillover_only"

-- 国内規制の波及リスク
IF domestic_regulation_status == "announced" THEN
    domestic_spillover_risk = "high"
    domestic_compliance_timeline_months = 18   -- 法制化まで約18ヶ月と想定
ELSE IF domestic_regulation_status == "under_review" THEN
    domestic_spillover_risk = "medium"
    domestic_compliance_timeline_months = 30
ELSE
    domestic_spillover_risk = "low"
    domestic_compliance_timeline_months = 48
```

### Step 2: 広告収益インパクトの評価

```
-- 未成年向け行動ターゲティング禁止による収益影響
IF behavioral_ad_ban_minors THEN
    -- 未成年への行動ターゲティング広告が禁止された場合の直接損失
    direct_ad_revenue_loss_monthly = minor_targeted_ad_revenue_jpy
    direct_ad_revenue_loss_annual = direct_ad_revenue_loss_monthly × 12
    
    -- 未成年向け広告のコンテキスト広告への移行時の収益低下率
    contextual_ad_cpm_discount = 0.45  -- 行動ターゲティング比で55%のCPM低下が一般的
    recoverable_revenue_ratio = 1 - contextual_ad_cpm_discount
    net_annual_loss = direct_ad_revenue_loss_annual × contextual_ad_cpm_discount
ELSE
    direct_ad_revenue_loss_annual = 0
    net_annual_loss = 0

-- 広告主の出稿行動変化（全ユーザー向けの波及影響）
-- 規制対応のコンプライアンスコストが広告単価に転嫁される場合
IF behavioral_targeting_ad_ratio_percent >= 70 THEN
    advertiser_churn_risk = "high"      -- 行動ターゲティング依存度が高い
    estimated_advertiser_churn_percent = 12
ELSE IF behavioral_targeting_ad_ratio_percent >= 40 THEN
    advertiser_churn_risk = "medium"
    estimated_advertiser_churn_percent = 6
ELSE
    advertiser_churn_risk = "low"
    estimated_advertiser_churn_percent = 2

-- 上位広告主の集中リスク
IF top10_advertiser_revenue_ratio_percent >= 50 THEN
    concentration_risk = "critical"     -- 上位10社が離反すると壊滅的
ELSE IF top10_advertiser_revenue_ratio_percent >= 30 THEN
    concentration_risk = "high"
ELSE
    concentration_risk = "moderate"

total_ad_revenue_impact_annual = net_annual_loss + (monthly_ad_revenue_jpy × 12 × estimated_advertiser_churn_percent / 100)
```

### Step 3: アルゴリズム透明性対応コストの評価

```
-- アルゴリズム開示義務への対応コスト
IF algorithm_transparency_required THEN
    IF algorithm_documentation_level == "full" THEN
        transparency_readiness = "ready"
        documentation_cost_jpy = 5_000_000       -- 公開用整備のみ
    ELSE IF algorithm_documentation_level == "partial" THEN
        transparency_readiness = "partial"
        documentation_cost_jpy = 30_000_000      -- 追加文書化+監査
    ELSE IF algorithm_documentation_level == "minimal" THEN
        transparency_readiness = "major_gap"
        documentation_cost_jpy = 80_000_000      -- 大規模文書化プロジェクト
    ELSE
        transparency_readiness = "critical_gap"
        documentation_cost_jpy = 150_000_000     -- ゼロからの構築

    -- アルゴリズム種別による開示の技術的難易度
    IF recommendation_algorithm_type == "deep_learning" THEN
        explainability_difficulty = "high"        -- ブラックボックス性が高い
        additional_xai_cost_jpy = 50_000_000     -- XAI(説明可能AI)の導入コスト
    ELSE IF recommendation_algorithm_type == "hybrid" THEN
        explainability_difficulty = "medium"
        additional_xai_cost_jpy = 25_000_000
    ELSE
        explainability_difficulty = "low"
        additional_xai_cost_jpy = 5_000_000

    total_transparency_cost = documentation_cost_jpy + additional_xai_cost_jpy
ELSE
    transparency_readiness = "not_required"
    total_transparency_cost = 0
```

### Step 4: 未成年保護施策の対応評価

```
-- 年齢確認メカニズムの充足度
IF age_verification_method == "document_verification" THEN
    age_verify_readiness = "strong"
    age_verify_upgrade_cost_jpy = 0
ELSE IF age_verification_method == "ai_estimation" THEN
    age_verify_readiness = "moderate"
    age_verify_upgrade_cost_jpy = 20_000_000     -- 精度向上+補完手段
ELSE IF age_verification_method == "self_declaration" THEN
    age_verify_readiness = "weak"
    age_verify_upgrade_cost_jpy = 60_000_000     -- 本格的な年齢確認導入
ELSE
    age_verify_readiness = "none"
    age_verify_upgrade_cost_jpy = 100_000_000    -- ゼロからの構築

-- 未成年比率に応じた優先度
IF minor_user_ratio_percent >= 30 THEN
    minor_protection_priority = "critical"
ELSE IF minor_user_ratio_percent >= 15 THEN
    minor_protection_priority = "high"
ELSE IF minor_user_ratio_percent >= 5 THEN
    minor_protection_priority = "medium"
ELSE
    minor_protection_priority = "low"

-- コンテンツモデレーションの追加人員
IF minor_protection_priority IN ("critical", "high") THEN
    additional_moderators_needed = MAX(content_moderation_staff_count × 0.3, 5)
    annual_moderator_cost_jpy = additional_moderators_needed × 8_000_000  -- 人件費
ELSE
    additional_moderators_needed = MAX(content_moderation_staff_count × 0.1, 2)
    annual_moderator_cost_jpy = additional_moderators_needed × 8_000_000
```

### Step 5: 競争環境・市場機会の評価

```
-- 競合他社（Meta/TikTok）が規制対応に追われることによる機会
IF direct_regulation_target == false THEN
    -- 自社は直接対象外 → 競合が対応コストを負う間のチャンス
    competitive_window_months = compliance_deadline_days / 30
    
    IF domestic_spillover_risk == "low" THEN
        competitive_opportunity = "significant"   -- 国内規制も当面なし、有利
    ELSE IF domestic_spillover_risk == "medium" THEN
        competitive_opportunity = "moderate"      -- 時間差あり、短期チャンス
    ELSE
        competitive_opportunity = "limited"       -- 国内でも間もなく適用

    -- 広告主の移行（Meta/TikTokから自社への出稿シフト）
    IF advertiser_count >= 500 THEN
        ad_migration_potential = "high"
    ELSE IF advertiser_count >= 100 THEN
        ad_migration_potential = "medium"
    ELSE
        ad_migration_potential = "low"
ELSE
    competitive_opportunity = "none"              -- 自社も対象
    ad_migration_potential = "none"

-- 先行対応による信頼性ブランディング
IF privacy_compliance_certifications contains "ISMS" OR "Pmark" THEN
    trust_brand_readiness = "strong"
ELSE IF data_retention_policy_exists THEN
    trust_brand_readiness = "moderate"
ELSE
    trust_brand_readiness = "weak"
```

### Step 6: 総合インパクトスコアの算出

```
-- リスクスコア（ネガティブインパクト）
revenue_risk_score = CASE
    WHEN total_ad_revenue_impact_annual > monthly_ad_revenue_jpy × 3 THEN 30
    WHEN total_ad_revenue_impact_annual > monthly_ad_revenue_jpy × 1.5 THEN 22
    WHEN total_ad_revenue_impact_annual > monthly_ad_revenue_jpy × 0.5 THEN 14
    ELSE 5
END

compliance_cost_score = CASE
    WHEN (total_transparency_cost + age_verify_upgrade_cost_jpy) > 200_000_000 THEN 20
    WHEN (total_transparency_cost + age_verify_upgrade_cost_jpy) > 100_000_000 THEN 15
    WHEN (total_transparency_cost + age_verify_upgrade_cost_jpy) > 30_000_000 THEN 10
    ELSE 4
END

urgency_score = CASE
    WHEN regulation_urgency == "immediate" THEN 20
    WHEN regulation_urgency == "near_term" THEN 15
    WHEN domestic_spillover_risk == "high" THEN 12
    WHEN domestic_spillover_risk == "medium" THEN 8
    ELSE 3
END

minor_risk_score = CASE
    WHEN minor_protection_priority == "critical" THEN 15
    WHEN minor_protection_priority == "high" THEN 11
    WHEN minor_protection_priority == "medium" THEN 7
    ELSE 3
END

-- 機会スコア（ポジティブインパクト、別枠で記録）
opportunity_score = CASE
    WHEN competitive_opportunity == "significant" AND ad_migration_potential == "high" THEN 20
    WHEN competitive_opportunity == "moderate" AND ad_migration_potential IN ("high", "medium") THEN 14
    WHEN competitive_opportunity == "limited" OR ad_migration_potential == "low" THEN 7
    ELSE 0
END

-- 総合インパクトスコア（リスク中心、機会を加味）
total_impact_score = revenue_risk_score + compliance_cost_score + urgency_score + minor_risk_score
total_impact_score = MIN(total_impact_score, 100)

-- インパクトレベルの決定
IF total_impact_score >= 75 THEN impact_level = "critical"
ELSE IF total_impact_score >= 55 THEN impact_level = "high"
ELSE IF total_impact_score >= 35 THEN impact_level = "medium"
ELSE IF total_impact_score >= 20 THEN impact_level = "low"
ELSE impact_level = "minimal"

-- 機会がリスクを上回る場合の特別判定
IF opportunity_score > (total_impact_score × 0.5) AND direct_regulation_target == false THEN
    impact_type = "mixed_with_opportunity"
ELSE
    impact_type = "risk"
```

## 出力

### インパクト評価結果

| 出力項目 | 型 | 説明 |
|---|---|---|
| `impact_level` | enum | `critical` / `high` / `medium` / `low` / `minimal` |
| `impact_score` | int | 総合インパクトスコア（0-100） |
| `opportunity_score` | int | 機会スコア（0-20） |
| `confidence_score` | float | 分析確信度（0.00-1.00） |
| `impact_type` | enum | `risk` / `mixed_with_opportunity` |
| `direct_regulation_target` | bool | 直接規制対象か |
| `domestic_spillover_risk` | string | 国内規制波及リスク |
| `total_ad_revenue_impact_annual_jpy` | int | 年間広告収益影響額（円） |
| `total_compliance_cost_jpy` | int | コンプライアンス対応コスト合計（円） |
| `minor_protection_priority` | string | 未成年保護対応の優先度 |
| `transparency_readiness` | string | アルゴリズム透明性の準備状況 |
| `competitive_opportunity` | string | 競合規制に伴う事業機会 |
| `ad_migration_potential` | string | 広告主移行ポテンシャル |
| `compliance_deadline_date` | date | 準拠期限日 |

### 推奨アクション

| 優先度 | アクション | 条件 |
|---|---|---|
| P0（即時） | レコメンドアルゴリズムの動作原理の内部文書化を開始 | `transparency_readiness` が `critical_gap` または `major_gap` |
| P0（即時） | 未成年ユーザーの行動ターゲティング広告の段階的停止計画を策定 | `minor_protection_priority` が `critical` または `high` |
| P1（1ヶ月以内） | 年齢確認メカニズムの精度向上・多要素化の検討 | `age_verify_readiness` が `weak` または `none` |
| P1（1ヶ月以内） | 広告主向けにコンテキスト広告の代替プランを提案 | `behavioral_targeting_ad_ratio_percent` が 50% 以上 |
| P1（1ヶ月以内） | 競合からの広告主獲得キャンペーンの企画 | `competitive_opportunity` が `significant` または `moderate` |
| P2（3ヶ月以内） | XAI（説明可能AI）技術の導入によるアルゴリズム説明機能の実装 | `explainability_difficulty` が `high` |
| P2（3ヶ月以内） | プライバシー・バイ・デザイン原則に基づく広告システムの再設計 | `domestic_spillover_risk` が `high` |
| P2（3ヶ月以内） | 未成年専用フィード（安全モード）の設計・プロトタイプ開発 | `minor_user_ratio_percent` が 15% 以上 |
| P3（6ヶ月以内） | 日本の個人情報保護委員会の動向モニタリング体制の構築 | `domestic_spillover_risk` が `medium` 以上 |
| P3（6ヶ月以内） | 「透明性レポート」の定期公開体制の整備 | 常時実行 |
| P3（6ヶ月以内） | コンテンツモデレーションチームの増員計画策定 | `additional_moderators_needed` が 5名以上 |

### 通知テンプレート

```
【{priority}】EU DSA規制強化に伴うSNS事業への影響分析

■ インパクトレベル: {impact_level}（スコア: {impact_score}/100）
■ 直接規制対象: {direct_regulation_target}
■ 国内波及リスク: {domestic_spillover_risk}
■ 年間広告収益影響: ▲{total_ad_revenue_impact_annual_jpy:,}円
■ コンプライアンス対応コスト: {total_compliance_cost_jpy:,}円
■ 準拠期限: {compliance_deadline_date}（残{compliance_deadline_days}日）

欧州デジタルサービス法（DSA）に基づき、Meta・TikTokに対して
アルゴリズム透明性確保と未成年保護の追加義務が命じられました。

【リスク評価】
- アルゴリズム透明性準備状況: {transparency_readiness}
- 未成年保護対応優先度: {minor_protection_priority}
- 広告主離反リスク: {advertiser_churn_risk}
- 収益集中リスク: {concentration_risk}

【機会評価】（機会スコア: {opportunity_score}/20）
- 競合規制対応中の事業機会: {competitive_opportunity}
- 広告主移行ポテンシャル: {ad_migration_potential}
- 信頼性ブランディング準備: {trust_brand_readiness}

【推奨アクション】
{recommended_actions}

本規制は当社SNS事業の広告モデルおよびアルゴリズム設計に
直接的な影響を及ぼす可能性があります。早期対応を推奨します。
```

## 参照データソース

### Fabric テーブル（sqldb_sns_01）

| テーブル名 | 用途 |
|---|---|
| `customers` | MAU算出、年齢層別ユーザー分析、未成年比率の算出、アカウントティア分析 |
| `contracts` | 広告主契約の分析、行動ターゲティング依存度の算出、上位広告主の特定 |
| `transactions` | 広告費取引のターゲティング手法別内訳、未成年セグメント向け広告の収益算出 |
| `ad_inventory` | 広告枠のフォーマット別・ターゲティング手法別の在庫と単価分析 |

### Fabric テーブル（sqldb_common_01）

| テーブル名 | 用途 |
|---|---|
| `unified_customers` | 年齢情報の補完（他事業領域での登録情報との照合） |
| `customer_segments` | 未成年セグメントの特定、リスクスコアとの相関分析 |
| `domain_id_mappings` | 携帯電話事業の年齢確認済み顧客との突合 |

### Fabric テーブル（sqldb_news_01）

| テーブル名 | 用途 |
|---|---|
| `news_articles` | DSA関連・プライバシー規制関連ニュースの時系列トラッキング |
| `impact_analyses` | 携帯電話事業・SI事業での同一ニュース分析結果との統合評価 |
| `notifications` | 過去の規制関連通知との重複排除、エスカレーション判定 |

### 外部参照データ

| データソース | 用途 |
|---|---|
| 個人情報保護委員会 公表資料 | 国内プラットフォーム規制の検討状況モニタリング |
| Sensor Tower / data.ai | 競合プラットフォームのユーザー動向・広告市場規模推計 |
| IAB Europe AdEx Benchmark | 欧州デジタル広告市場のトレンド・手法別内訳 |
| European Commission DSA Transparency Database | 規制対象プラットフォームの義務履行状況 |
