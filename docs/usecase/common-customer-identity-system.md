# 顧客統合ID基盤（共通システム）

## 目的
モバイル通信・Eコマース・Fintechの各事業部が個別に管理していた顧客IDを一元管理し、全事業部が共通の顧客基盤を参照できるようにする。

## 位置付け
- 全事業部の**上位に存在する社内共通システム**として機能する。
- 各事業部は自事業の業務システムを保持しつつ、顧客の同一性判定と基本属性はこのシステムを参照する。
- 事業部をまたぐデータ連携（Fintechシグナル→モバイル/EC）の名寄せキーもこのシステムが提供する。

## 対象業務システム
1. 統合顧客マスタ管理システム
2. ドメイン間IDマッピングシステム
3. 顧客属性・セグメント管理システム

## データモデル

### 統合顧客マスタ
| 項目 | 型 | 説明 |
|---|---|---|
| unified_customer_id | string | 統合顧客ID（全事業部共通キー） |
| full_name | string | 氏名 |
| birth_date | date | 生年月日 |
| gender | string | 性別 |
| region | string | 居住地域 |
| age_band | string | 年齢帯 |
| primary_email | string | 代表メールアドレス |
| primary_phone | string | 代表電話番号 |
| kyc_status | string | 本人確認ステータス（verified/pending/rejected） |
| registered_at | datetime | 初回登録日時 |
| last_updated_at | datetime | 最終更新日時 |

### ドメイン別IDマッピング
| 項目 | 型 | 説明 |
|---|---|---|
| map_id | string | マッピングID |
| unified_customer_id | string | 統合顧客ID |
| domain | enum | `mobile` / `ecommerce` / `fintech` |
| domain_customer_id | string | 各事業部の顧客ID |
| source_system | string | 元業務システム名 |
| linked_at | datetime | 紐付け日時 |
| link_status | string | 有効/無効/審査中 |

### 顧客セグメントマスタ
| 項目 | 型 | 説明 |
|---|---|---|
| segment_id | string | セグメントID |
| segment_name | string | セグメント名 |
| definition_rule | string | 判定ルール（例: age_band=20s AND region=関東） |
| target_domains | string | 対象事業部（カンマ区切り） |
| valid_from / valid_to | datetime | 有効期間 |

### 顧客セグメント割り当て
| 項目 | 型 | 説明 |
|---|---|---|
| assignment_id | string | 割り当てID |
| unified_customer_id | string | 統合顧客ID |
| segment_id | string | セグメントID |
| assigned_at | datetime | 割り当て日時 |
| expires_at | datetime | 有効期限（NULL=無期限） |

### 顧客統合イベント履歴
| 項目 | 型 | 説明 |
|---|---|---|
| event_id | string | イベントID |
| unified_customer_id | string | 統合顧客ID |
| event_type | string | 登録/名寄せ/更新/無効化 |
| domain | string | 発生事業部 |
| event_detail | string | 変更内容の概要 |
| occurred_at | datetime | 発生日時 |

## 業務システム別データモデルマッピング

| 業務システム | 使用するデータモデル |
|---|---|
| 統合顧客マスタ管理システム | 統合顧客マスタ、顧客統合イベント履歴 |
| ドメイン間IDマッピングシステム | ドメイン別IDマッピング |
| 顧客属性・セグメント管理システム | 顧客セグメントマスタ、顧客セグメント割り当て |

## 各事業部との関係

| 事業部 | 事業部側の顧客ID | 統合ID参照方法 |
|---|---|---|
| モバイル通信 | `customer_id` | ドメイン別IDマッピング（domain=`mobile`） |
| Eコマース | `member_id` | ドメイン別IDマッピング（domain=`ecommerce`） |
| Fintech | `user_id` | ドメイン別IDマッピング（domain=`fintech`） |

## 注意事項
- 各事業部は**自事業の顧客IDを廃止しない**。業務システムは事業部IDで動作し続ける。
- 事業部をまたぐ照合が必要な場合のみ `unified_customer_id` を使用する。
- `kyc_status` はFintechのKYC記録をもとに更新し、全事業部が参照できる。
