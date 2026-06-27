# 02. Agent 1〜4 定義仕様

関連: [開発計画 README](README.md) | [全体像](01-overview.md)

---

## Agent 1: Web情報収集エージェント

### 役割
入力ニュースに関連する背景情報・最新動向を Web 検索で補完し、後続エージェントが内部データと照合しやすい形式で要約する。

### 入力
| 項目 | 型 | 説明 |
|---|---|---|
| news_text | string | 元のニュース本文 |
| search_hints | string[] | 検索を補助するキーワード群（任意） |

### 出力
| 項目 | 型 | 説明 |
|---|---|---|
| summary | string | ニュース背景のサマリ（500文字以内） |
| key_factors | string[] | 主要影響因子リスト（最大10件） |
| source_urls | string[] | 参照 URL リスト |

### 使用ツール / プラグイン
- **BingSearchPlugin**: Bing Search API を呼び出し最新情報を取得
- **WebPageFetchPlugin**: 特定 URL の本文抽出（任意）

### システムプロンプト（骨子）
```
あなたはニュース分析の調査担当エージェントです。
入力されたニュース本文を読み、以下の観点で Web 検索を行い背景情報を補完してください。
- 為替・金融市場への影響
- 国内競合他社の動向
- 業界トレンドとマクロ経済指標
出力は JSON 形式（summary / key_factors / source_urls）で返してください。
```

### Microsoft Agent Framework 実装イメージ
```csharp
AgentDefinition webResearchAgent = new()
{
    Name = "WebResearchAgent",
    Description = "ニュースに関する Web 情報収集・要約を担当",
    Instructions = WebResearchPrompts.SystemPrompt,
    ModelDeployment = "gpt-4o",
    Tools = [typeof(BingSearchPlugin), typeof(WebPageFetchPlugin)]
};
```

---

## Agent 2: ビジネスインパクト評価エージェント

### 役割
Agent 1 の Web 調査結果と、OneLake / Microsoft Fabric に格納された全社経営データを照合し、3 事業部への影響度を評価する。

### 入力
| 項目 | 型 | 説明 |
|---|---|---|
| web_summary | string | Agent 1 のサマリ |
| key_factors | string[] | Agent 1 の影響因子リスト |

### 出力
| 項目 | 型 | 説明 |
|---|---|---|
| impact_scores | ImpactScore[] | 事業部別影響スコア（0〜5） |
| impact_reasons | string[] | スコア算出根拠（事業部ごと） |
| priority_order | string[] | 対応優先度順（"mobile","ecommerce","fintech"） |

#### ImpactScore 型
| 項目 | 型 |
|---|---|
| division | string |
| score | float |
| risk_level | string ("low" / "medium" / "high") |

### 使用ツール / プラグイン
- **FabricDataPlugin**: OneLake の全社 KPI・収益データを Kusto/SQL で照会
- **FinancialCalculatorPlugin**: 為替感応度・金利影響の簡易計算

### システムプロンプト（骨子）
```
あなたはビジネスインパクト評価を担当するエージェントです。
提供されたニュース背景情報と以下の内部データを照合し、
モバイル通信・Eコマース・Fintechの各事業部への影響を0〜5のスコアで評価してください。
参照可能な内部データ:
- 全社売上・コスト・粗利（FabricDataPlugin 経由）
- 事業部別 KPI 推移（FabricDataPlugin 経由）
評価軸: 収益影響・コスト影響・顧客影響・競争環境影響
```

### Microsoft Agent Framework 実装イメージ
```csharp
AgentDefinition impactAgent = new()
{
    Name = "BusinessImpactAgent",
    Description = "全社経営データを元にビジネスインパクトを評価",
    Instructions = ImpactAssessmentPrompts.SystemPrompt,
    ModelDeployment = "gpt-4o",
    Tools = [typeof(FabricDataPlugin), typeof(FinancialCalculatorPlugin)]
};
```

---

## Agent 3: 事業部別レコメンドエージェント

### 役割
Agent 2 の影響評価を受け、各事業部の業務システムデータを直接参照して具体的な Next Action を生成する。3 事業部は **並列実行**（または優先度順の逐次実行）を想定。

### 入力
| 項目 | 型 | 説明 |
|---|---|---|
| impact_result | BusinessImpactResult | Agent 2 の評価結果全体 |
| target_division | string | "mobile" / "ecommerce" / "fintech" |

### 出力（1事業部あたり）
| 項目 | 型 | 説明 |
|---|---|---|
| division | string | 事業部名 |
| insight_summary | string | 内部データから得たインサイト（300文字以内） |
| recommended_actions | Action[] | Next Action リスト（最大5件） |
| data_evidence | string[] | 根拠となったデータ参照元 |

#### Action 型
| 項目 | 型 |
|---|---|
| title | string |
| description | string |
| priority | string ("high" / "medium" / "low") |
| owner_hint | string |

### 事業部別参照データ・プラグイン

| 事業部 | 主な参照データ | プラグイン |
|---|---|---|
| モバイル | 端末コスト・MNP履歴・分割払い・施策配信履歴 | `MobileDataPlugin` |
| Eコマース | 在庫・仕入通貨・ポイント還元・会員行動 | `EcommerceDataPlugin` |
| Fintech | FX・ローン残高・与信スコア・収益リスク | `FintechDataPlugin` |

### システムプロンプト（骨子）
```
あなたは {division} 事業部の業務データ分析エージェントです。
ビジネスインパクト評価結果と、{division} 事業部の業務システムデータを照合し、
具体的な Next Action を最大5件生成してください。
各アクションには優先度・担当部門ヒントを含めてください。
データの根拠（テーブル名・指標名）を必ず出力に含めてください。
```

### Microsoft Agent Framework 実装イメージ
```csharp
// 事業部ごとにインスタンスを生成（Tool のみ差し替え）
AgentDefinition CreateDivisionAgent(string division, Type dataToolType) => new()
{
    Name = $"{division}RecommendAgent",
    Description = $"{division} 事業部の業務データを元にレコメンドを生成",
    Instructions = string.Format(RecommendPrompts.SystemPromptTemplate, division),
    ModelDeployment = "gpt-4o",
    Tools = [dataToolType, typeof(SkillSearchPlugin)]
};
```

---

## Agent 4: 通知エージェント

### 役割
Agent 3 が生成した事業部別レコメンドを、適切な担当者・チャネルへ通知する。通知先は Dynamics 365 の組織データを元に決定する。

### 入力
| 項目 | 型 | 説明 |
|---|---|---|
| recommendations | DivisionRecommendation[] | Agent 3 の全事業部レコメンド |
| scenario_title | string | シナリオ名（件名に使用） |

### 出力
| 項目 | 型 | 説明 |
|---|---|---|
| notification_log | NotificationEntry[] | 送信ログ（宛先・チャネル・時刻） |
| publish_status | string | "success" / "partial" / "failed" |

### 使用ツール / プラグイン
- **TeamsNotificationPlugin**: Teams Adaptive Card の送信
- **M365MailPlugin**: Outlook メール送信（オプション）
- **Dynamics365Plugin**: 通知先担当者情報の照会
- **PublishPlugin**: Dynamics 365 / Dataverse へのレコメンド記録

### システムプロンプト（骨子）
```
あなたは通知担当エージェントです。
受け取ったレコメンドを事業部責任者に Teams カードで送信し、
結果を通知ログとして返してください。
優先度 high のアクションは @メンション付きで送信してください。
```

### Microsoft Agent Framework 実装イメージ
```csharp
AgentDefinition notificationAgent = new()
{
    Name = "NotificationAgent",
    Description = "Teams / M365 経由で担当者へ通知",
    Instructions = NotificationPrompts.SystemPrompt,
    ModelDeployment = "gpt-4o-mini",
    Tools = [typeof(TeamsNotificationPlugin), typeof(Dynamics365Plugin), typeof(PublishPlugin)]
};
```
