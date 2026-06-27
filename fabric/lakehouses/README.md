# Lakehouse 作成手順

Fabric ポータルで Workspace `fabric_seworkshop_ws1` を開き、以下を作成する。

| Lakehouse | 用途 |
|---|---|
| `lh_nexus6_bronze` | `scenario_seed.csv` と Fabric SQL DB ミラーの Raw データ |
| `lh_nexus6_silver` | Notebook `nb_bronze_to_silver` の正規化 Delta テーブル |
| `lh_nexus6_gold` | Notebook `nb_silver_to_gold` の AI エージェント向け集計 |

## 手順

1. **New item** → **Lakehouse** を選択し、上記 3 件を作成する。
2. `lh_nexus6_bronze` の `Files/manual_seed/` に `fabric/seed/scenario_seed.csv` をアップロードする。
3. Fabric SQL Database 16 件のミラーを Bronze から参照できるようにする。
4. Notebook インポート後、`nb_bronze_to_silver` は Bronze/Silver、`nb_silver_to_gold` は Silver/Gold にアタッチする。
5. `nb_bronze_to_silver` → `nb_silver_to_gold` の順に手動実行する。

既存 Lakehouse がある場合は削除せず、名前・接続先を確認して再利用する。
