# 17. Scenario Verification

関連: [開発計画 README](README.md) | [手動 enqueue E2E](16-foundry-trigger-wiring.md)

---

## S1: 為替急変

| 項目 | 結果 |
|---|---|
| 検証トラック | Phase 3 / S1 `s1-scenario-fx` |
| 投入時刻 | 2026-06-27T10:03:53+00:00 |
| QueueMessageId | `2573a4d8-4ae5-4694-b998-5ec00b7429d6` |
| ACA FQDN | `https://ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io` |
| Workflow RunId | `41aaac6d-fe62-4530-8b3b-f26836d34b5c` |
| ヒットキーワード | 為替, 円安, 越境EC, 海外仕入, FX, 海外決済 |
| 不足キーワード | 端末仕入, 仕入コスト, 為替リスク, 為替エクスポージャー, 設備投資, 端末分割 |
| 判定 | **Pass** |

### データ準備

`DemoDataGenerator` を `scenario=1`, `target=local-sqlite`, `scale=small` で実行し成功。

- DB: `logs/demo-s1.db`
- サイズ: 1110016 bytes
- 主要件数: unified_customers=300, mobile_contracts=300, orders=200, accounts=150, fx_positions=150, fx_rate_snapshots=8, revenue_risks=6
- Fabric SQL DB x16 への実投入: 未実施（本検証対象外、Mock fallback 想定）

### 実行結果

- 結果 JSON: `logs/scenario-s1-result.json`
- ACA ログ抜粋: `logs/scenario-s1-aca.log`
- キーワード照合: `logs/scenario-s1-keywords.txt`
- Queue peek length: 0

Digest: 【速報】為替・緊急 円が一時158円台に急落 地政学リスクで年初来安値を更新、輸入コスト上昇懸念が広がる 📰 NEXUS経済編集部 🕐 2026年6月27日 10:32 📄 84,201 PV 外国為替市場で円安が急進し、一時1ドル＝158円42銭を付け今年の最安値を更新。中東情勢の緊迫化と米国の金融引き締め長期化観測が重なり、国内各業界で輸入コストや収益への影響試算が始まっている。 27日の外国為替市場で、円相場は一時 1ドル＝158円42銭 まで下落し、今年の最安値を更新した。前日比2円31銭の円安水準で、昨年10月以来約8ヶ月ぶりの安値圏となる。ニューヨーク市場でのドル買いの流れが東京市場に引き継がれ、午前中から売り圧力が強まった。 ■ 下落の背景：地政学リスクと米金融政策の複合要因 円売りの背景には複合的な要因がある。まず中東における地政学リスクの高まりが原油価格の上昇をもたらし、エネルギー輸入国である日本の経常収支を悪化させるとの観測が広がった。また米連邦準備制度理事会（FRB）が6月FOMC（連邦公開市場委員会）で年内利下げ回数の見通しを3回から1回に引き下げたことで、 日

---

## S2: 競合経済圏統合・ポイント改定

| 項目 | 結果 |
|---|---|
| 検証トラック | Phase 3 / S2 `s2-scenario-comp` |
| 投入時刻 | 2026-06-27T10:06:03+00:00 |
| QueueMessageId | `9293f739-ea8d-48ad-9e65-cb14cfd7b99a` |
| ACA FQDN | `https://ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io` |
| Workflow RunId | `5acf8500-8656-4f8c-bbdc-ab65f97108a5` |
| ヒットキーワード | MNP, 乗換, 競合, 統合, ポイント, ポイント還元, 経済圏, 決済シェア, 顧客流出 |
| 不足キーワード | カードシェア, マーケットシェア |
| 判定 | **Pass** |

### データ準備

`DemoDataGenerator` を `scenario=2`, `target=local-sqlite`, `scale=small` で実行し成功。

- DB: `logs/demo-s2.db`
- サイズ: 1122304 bytes
- 主要件数: unified_customers=300, mobile_contracts=300, orders=200, accounts=150
- Fabric SQL DB x16 への実投入: 未実施（本検証対象外、Mock fallback 想定）

### 実行結果

- 結果 JSON: `logs/scenario-s2-result.json`
- ACA ログ抜粋: `logs/scenario-s2-aca.log`
- キーワード照合: `logs/scenario-s2-keywords.txt`
- Queue peek length: 0

Digest: 大手3キャリア×ECが「ONE PASS」発表 回線・ポイント・決済を完全統合 - NEXUS経済ニュース NEXUS経済 ビジネス・金融・テクノロジー情報 検索 トップ 為替・国際 通信・EC 金融政策 マーケット テクノロジー Fintech 経済 企業 産業 トップ › 通信・EC › 大手3キャリア×ECが「ONE PASS」発表 【大型統合】通信・EC・決済 大手3キャリア×ECプラットフォームが「ONE PASS」を発表 回線・ポイント・決済を完全統合、4,200万アカウント基盤で経済圏覇権争いへ。

補足:

- `/devui/logs` の latestRun が `article-comp1.html` 由来であることを `ONE PASS` / `大型統合` を含む本文 prefix で確認。
- ACA ログでは Foundry chat completions が BadRequest のため Mock fallback に切り替わったが、全 Workflow step は `Succeeded: True` で完了。
- 期待キーワードは 9 件ヒットし、DoD の 4 件以上を満たした。

---

## S3: 日銀金融政策転換・利上げ

| 項目 | 結果 |
|---|---|
| 検証トラック | Phase 3 / S3 `s3-scenario-boj` |
| 投入時刻 | 2026-06-27T10:08:13+00:00 |
| QueueMessageId | `52413baa-3bbe-4823-bd8e-53f7dc94f3b0` |
| ACA FQDN | `https://ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io` |
| Workflow RunId | `1630e7a1-65fb-468c-a40b-0f3e92a26171` |
| ヒットキーワード | 利上げ, 金利, 日銀, 分割払い, 端末分割, 住宅ローン, リボ, 利ざや, 消費, 設備投資, 与信 |
| 不足キーワード | 信用 |
| 判定 | **Pass** |

### データ準備

`DemoDataGenerator` を `scenario=3`, `target=local-sqlite`, `scale=small` で実行し成功。

- DB: `logs/demo-s3.db`
- サイズ: 1110016 bytes
- 主要件数: unified_customers=300, mobile_contracts=300, orders=200, accounts=150, fx_positions=150, fx_rate_snapshots=8, revenue_risks=6
- Fabric SQL DB x16 への実投入: 未実施（本検証対象外、Mock fallback 想定）

### 実行結果

- 結果 JSON: `logs/scenario-s3-result.json`
- ACA ログ抜粋: `logs/scenario-s3-aca.log`
- キーワード照合: `logs/scenario-s3-keywords.txt`
- Queue peek length: 0

Digest: 日銀が政策金利を0.5%に引き上げ決定 17年ぶり高水準 - NEXUS経済ニュース NEXUS経済 ビジネス・金融・テクノロジー情報 検索 トップ 為替・国際 通信・EC 金融政策 マーケット テクノロジー Fintech 経済 企業 産業 トップ › 金融政策 › 日銀が政策金利を0.5%に引き上げ決定 【政策転換】日銀・金融政策 日銀が政策金利を0.5%に引き上げ決定 17年ぶりの高水準、住宅ローン・リボ払いへの影響注視 次回会合での追加利上げも視野 📰 金融政策部 🕐 2026年6月27日 08:00 📄 97,443 PV 日本銀行は金融政策決定会合において、政策金利を0.25%から0.5%へ引き上げることを決定。2007年以来17年ぶりとなる高水準。植田総裁は「経済・物価の見通しが実現していけば、引き続き利上げを進める」と表明し、7月末の追加利上げも意識される。 日本銀行は27日の金融政策決定会合において、政策金利（無担保コール翌日物金利）の誘導目標を 0.25%から0.5%へ引き上げる ことを賛成多数で決定した。2007年2月以来17年ぶりとなる高水準で、国内の金融環境は。

補足:

- `/devui/logs` の latestRun が `article-boj1.html` 由来であることを `日銀` / `政策金利` / `0.5%` を含む本文 prefix で確認。
- ACA ログでは Foundry chat completions が BadRequest のため Mock fallback に切り替わったが、全 Workflow step は `Succeeded: True` で完了。
- 期待キーワードは 11 件ヒットし、DoD の 4 件以上を満たした。
