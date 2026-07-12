# knowledge

## 目的
`knowledge/` は Phase 1 トラック D `d-skill-ds` の Demo 用業務ナレッジ置き場です。Skill.md は業務判断ロジック、DS.md は Fabric/Lakehouse の KPI・テーブル意味・クエリ補助を記述します。

## リポジトリ配置
- `knowledge/skill/mobile/`: モバイル通信 Skill.md
- `knowledge/skill/ecommerce/`: Eコマース Skill.md
- `knowledge/skill/fintech/`: Fintech Skill.md
- `knowledge/ds/`: DS.md

## ADLS 配置
- ストレージアカウント: `stnexus6skill1t2i`
- コンテナ: `skill-docs`
- Blob パス: `mobile/`, `ecommerce/`, `fintech/`, `ds-docs/`

## 更新フロー
1. Scout または担当者が Markdown を更新する。
2. `az storage blob upload-batch --auth-mode login` で `skill-docs` コンテナへアップロードする。
3. Foundry File Search の再インデクシング完了後、Agent 2・3 が検索参照する。
4. Vector store 登録や Foundry 側の設定変更はトラック J が担当する。

## ファイル命名規則
- Skill.md: `<division>_skill_<topic>.md`
- DS.md: `ds_<scope>.md`
- トピックは英小文字とハイフンを使い、実在企業名や個人情報を含めない。
