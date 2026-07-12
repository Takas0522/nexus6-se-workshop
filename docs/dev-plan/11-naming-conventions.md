# 11. 命名規約

関連: [開発計画 README](README.md) | [Azure / M365 構成まとめ](09-azure-m365-configuration.md)

---

## 既存リソース

既存リソース名・SKU は [09. Azure / M365 必要構成まとめ](09-azure-m365-configuration.md#azure-サービス一覧) を正とする。

| 種別 | リソース名 | RG / 配置先 | SKU |
|---|---|---|---|
| Azure AI Foundry | `fd-PartnerIQ` | `SEWorkShopC12` | S0 (AIServices) |
| Foundry Project | `proj-PartnerIQ` | `fd-PartnerIQ` 配下 | - |
| Azure AI Search | `iq-knowledge-source` | `SEWorkShopC12` | Standard |
| Microsoft Fabric Capacity | `fabricswedencu001` | Sweden Central | F4 |
| Microsoft Fabric Workspace | `fabric_seworkshop_ws1` | Fabric | - |

---

## 新規リソース名

新規 Azure リソースは `rg-nexus6-swc` / Sweden Central に作成する。詳細な用途・SKU は [09 章の新規作成リソース一覧](09-azure-m365-configuration.md#azure-サービス一覧) を参照する。

| 種別 | 名称 |
|---|---|
| ADLS Gen2 | `stnexus6skill<NNNN>` |
| Storage Static Website | `stnexus6portal<NNNN>` |
| Storage Queue | `stnexus6skill<NNNN>` 内の `news-analysis-jobs` |
| Azure Key Vault | `kv-nexus6-swc` |
| Azure Container Apps Env | `cae-nexus6-swc` |
| Azure Container App | `ca-nexus6-hosted-agent` |
| Azure Container Registry | `crnexus6swc`（衝突時はトラック A で調整） |
| Application Insights | `appi-nexus6-swc` |

### `<NNNN>` の決定ルール

- `<NNNN>` は 4 桁のランダム英数小文字サフィックスとする。
- サブスクリプション内またはグローバル一意制約がある名称に使用する。
  - 対象: ADLS Gen2 / Storage Static Website / Azure Container Registry。
- ストレージアカウント名は Azure 制約に合わせ、英小文字と数字のみで構成する。
- 実際に採用する `<NNNN>` は、トラック A `a-infra-iac` が `az storage account check-name` 等で空き名を確認して確定する。
- ACR は 09 章の `crnexus6swc` を第一候補とし、名前衝突時のみ同等の一意性確認で調整する。

### 採用済み `<NNNN>`

| トラック | 採用値 | 確認結果 |
|---|---|---|
| Phase 1 Track A `a-infra-iac` | `1t2i` | `stnexus6skill1t2i` / `stnexus6portal1t2i` ともに `az storage account check-name` で `nameAvailable: true` を確認済み |

---

## リソースグループ・リージョン・タグ

| 項目 | 規約 |
|---|---|
| 新規リソースグループ | `rg-nexus6-swc` |
| リージョン | `swedencentral`（Sweden Central） |
| 必須タグ | `project=nexus6-se-workshop`, `env=demo` |

Phase 0 `p0-infra-base` ではリソースグループのみ作成し、個別リソースはトラック A `a-infra-iac` で作成する。
