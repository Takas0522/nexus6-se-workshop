# 27. Teams 投稿可読性改善

関連: [README](README.md) | [Skill/DS 設計](08-skill-ds-design.md) | [Teams Graph](24-teams-graph.md)

## 目的

Teams Adaptive Card で、Fabric の物理列名や Skill.md ファイル名をそのまま出さず、業務担当者が読める日本語ラベルで根拠を確認できるようにする。

## DS.md マッピング表

`knowledge/ds/` の 4 ファイル末尾に `物理項目 ↔ 論理項目（日本語ラベル）対応` を追加した。

| 対象 | 例 |
|---|---|
| Fintech | `loan_balance` → ローン残高、`avg_interest_rate` → 平均貸出金利 |
| Ecommerce | `cart_abandon_count` → カート離脱件数、`cross_border_cost` → 越境 EC 仕入コスト |
| Mobile | `overseas_procurement_cost` → 海外調達コスト、`mnp_out_rate` → MNP転出率 |
| 共通 KPI | `gross_revenue_jpy` → 月次売上、`gross_margin_rate` → 粗利率 |

Agent 2/3 の指示には「Fabric KPI は DS.md の論理項目（日本語）で記述し、物理列名は本文に出さない。値には単位を付ける」を追加した。

## Skill manifest

`docs/skills/skill-manifest.json` で 9 本の Skill.md に日本語タイトルと担当を定義する。実行時は `ReferenceCatalog` の内蔵 manifest で同じ対応を使い、ファイル名しか返らない場合も日本語タイトルに変換する。

## Adaptive Card レイアウト

Card 末尾は Adaptive Card 1.5 の `Table` で次の 3 ブロックに分ける。

1. 参照データ (Fabric KPI): 論理名 / 値 / 単位
2. 根拠 (Source): 日本語タイトル / 担当
3. Web リファレンス: タイトル/URL

DS.md 参照がある場合は補助的に FactSet で表示する。Table が描画されない Teams クライアントでは、同じ情報を本文 JSON の `categorizedReferences` として保持し、後続で ColumnSet/FactSet 化できる。

## Before / After

| Before | After |
|---|---|
| `loan_balance 26325000` | `ローン残高 26,325千円` |
| `fintech_skill_mortgage-rate-hike.md` | `住宅ローン金利上昇インパクト判断（金融部門 ベテラン審査担当）` |
| `dataReferences: ["...", "..."]` | Fabric KPI / Skill / Web のカテゴリ別表 |

## E2E 確認観点

スクリーンショット代替テキスト:

- 本文に物理列名ではなく「ローン残高」「平均貸出金利」「カート離脱件数」などの日本語論理名が表示されている。
- 「根拠 (Source)」表に Skill.md ファイル名ではなく日本語タイトルと担当が表示されている。
- 「参照データ (Fabric KPI)」「Web リファレンス」が別表として表示されている。

## 検証結果

| 項目 | 結果 |
|---|---|
| `dotnet test` | 23 unit / 1 integration passed |
| Docker image | `crnexus6swc.azurecr.io/nexus6-hosted-agent:x-ux-readability-20260627-2` |
| ACA revision | `ca-nexus6-hosted-agent--0000017` Running / Healthy |
| Vector Store | `vs_EN0WyOWKa7aVn0STee8oFhZ7` DS.md 4 本を再 upload / attach、13 files completed |
| E2E | S1/S2/S3 `/devui/run` で Teams Graph 投稿成功。最新 ACA log で Table、Skill 日本語名、Fabric KPI 論理名（例: 月次売上、粗利）を確認 |

## 運用メモ

ADLS / Vector Store の更新は、DS.md を `stnexus6skill1t2i/skill-docs/ds-docs/` に同名上書きし、Vector Store `vs_EN0WyOWKa7aVn0STee8oFhZ7` の同名ファイルを削除後に再アップロード・attach する。認証情報がないローカル環境ではリポジトリ更新とテストまでを実施する。
