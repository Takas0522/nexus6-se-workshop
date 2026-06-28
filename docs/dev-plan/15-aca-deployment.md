# 15. Azure Container Apps デプロイ記録

関連: [開発計画 README](README.md) | [実装ガイド](04-implementation-guide.md) | [Azure / M365 構成](09-azure-m365-configuration.md) | [Foundry ランタイム構成](13-foundry-runtime-config.md) | [コンテナビルド](14-container-build.md)

---

## デプロイ対象

| 項目 | 値 |
|---|---|
| Container App | `ca-nexus6-hosted-agent` |
| Resource Group | `rg-nexus6-swc` |
| ACA Environment | `cae-nexus6-swc` |
| Region | `swedencentral` |
| Image | `crnexus6swc.azurecr.io/nexus6-hosted-agent:0.1.0` |
| Registry auth | System Assigned Managed Identity |
| Ingress | external / port `8080` / HTTP |
| Scale | min `0` / max `2` / Consumption |
| FQDN | `ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io` |

> Demo smoke のため ingress は external のままにした。本番化時は internal 化、IP 制限、認証の追加を検討する。

---

## 実行コマンド

App Insights connection string は値を出力せず、コマンド置換で環境変数へ注入した。

```bash
APPINSIGHTS_CONNECTION_STRING="$(az monitor app-insights component show \
  -g rg-nexus6-swc \
  -a appi-nexus6-swc \
  --query connectionString -o tsv)"

az containerapp create \
  --resource-group rg-nexus6-swc \
  --name ca-nexus6-hosted-agent \
  --environment cae-nexus6-swc \
  --image crnexus6swc.azurecr.io/nexus6-hosted-agent:0.1.0 \
  --ingress external \
  --target-port 8080 \
  --transport http \
  --registry-server crnexus6swc.azurecr.io \
  --registry-identity system \
  --system-assigned \
  --min-replicas 0 \
  --max-replicas 2 \
  --env-vars \
    Foundry__ProjectEndpoint=https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ \
    Foundry__DefaultModelDeployment=gpt-5.4 \
    Foundry__NotificationModelDeployment=gpt-5.4 \
    Foundry__FileSearchVectorStoreId=vs_EN0WyOWKa7aVn0STee8oFhZ7 \
    Foundry__GroundingBingConnectionId= \
    Fabric__SqlEndpoint=fabric_seworkshop_ws1.datawarehouse.fabric.microsoft.com \
    Fabric__Database=lh_nexus6_gold \
    Storage__Account=stnexus6skill1t2i \
    KeyVault__Uri=https://kv-nexus6-swc.vault.azure.net/ \
    AzureMonitor__ConnectionString="$APPINSIGHTS_CONNECTION_STRING" \
    APPLICATIONINSIGHTS_CONNECTION_STRING="$APPINSIGHTS_CONNECTION_STRING" \
    DevUi__EnableManualTrigger=true
```

---

## RBAC

System Assigned MI principalId: `65c3be76-e682-43d7-b8d4-7a31b9acd1f6`

```bash
PRINCIPAL_ID="$(az containerapp identity show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query principalId -o tsv)"

ACR_ID="$(az acr show -n crnexus6swc --query id -o tsv)"
KV_ID="$(az keyvault show -g rg-nexus6-swc -n kv-nexus6-swc --query id -o tsv)"
ST_ID="$(az storage account show -g rg-nexus6-swc -n stnexus6skill1t2i --query id -o tsv)"
FOUNDRY_ID="$(az cognitiveservices account show -g SEWorkShopC12 -n fd-PartnerIQ --query id -o tsv)"

az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role AcrPull --scope "$ACR_ID"
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User" --scope "$KV_ID"
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor" --scope "$ST_ID"
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role "Storage Queue Data Contributor" --scope "$ST_ID"
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role "Cognitive Services User" --scope "$FOUNDRY_ID"

REVISION="$(az containerapp show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query properties.latestRevisionName -o tsv)"

az containerapp revision restart \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --revision "$REVISION"
```

確認結果:

| Scope | Role | Count |
|---|---|---:|
| `crnexus6swc` | `AcrPull` | 1 |
| `kv-nexus6-swc` | `Key Vault Secrets User` | 1 |
| `stnexus6skill1t2i` | `Storage Blob Data Contributor` | 1 |
| `stnexus6skill1t2i` | `Storage Queue Data Contributor` | 1 |
| `fd-PartnerIQ` | `Cognitive Services User` | 1 |

---

## Smoke 結果

```bash
FQDN="$(az containerapp show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query properties.configuration.ingress.fqdn -o tsv)"

curl -sS -o /dev/null -w '%{http_code}\n' "https://$FQDN/devui"

curl -sS -X POST "https://$FQDN/devui/run" \
  -H 'Content-Type: application/json' \
  -d '{"originalNewsText":"ACA smoke"}' \
  -o logs/aca-smoke.json \
  -w '%{http_code}\n'
```

確認結果:

| チェック | 結果 |
|---|---|
| Revision | `Running` / `Healthy` |
| `GET /devui` | `200` |
| `HEAD /devui` | `405`（アプリ側が HEAD 未実装。`curl -sI -X GET` では `200`） |
| `POST /devui/run` | `200` |
| Response keys | `webResearchResult`, `impactResult`, `recommendations`, `notificationResult` を確認 |

未設定値の影響:

- `Foundry__GroundingBingConnectionId` は空。Bing Grounding 未設定のため Web research は Mock fallback で応答する。
- `Fabric__SqlEndpoint` は `fabric_seworkshop_ws1.datawarehouse.fabric.microsoft.com` を設定。Fabric 接続不可時は KPI heuristic / Mock fallback で応答する。
- Teams Workflow URL は Key Vault 未設定でも `MockTeamsPlugin` にフォールバックする。

---

## 再デプロイ

新しいタグを ACR に push した後、Container App の image を更新する。

```bash
az containerapp update \
  --resource-group rg-nexus6-swc \
  --name ca-nexus6-hosted-agent \
  --image crnexus6swc.azurecr.io/nexus6-hosted-agent:<new-tag>
```

更新後は revision が `Running` / `Healthy` になることを確認し、Smoke を再実行する。

---

## トラブルシュート

### Registry pull に失敗する

1. Container App に System Assigned MI があることを確認する。
2. ACR scope で `AcrPull` が付与されていることを確認する。
3. `--registry-server crnexus6swc.azurecr.io --registry-identity system` が設定されていることを確認する。
4. RBAC 反映後に revision を restart する。

```bash
az containerapp identity show -g rg-nexus6-swc -n ca-nexus6-hosted-agent
az role assignment list --assignee "$PRINCIPAL_ID" --scope "$ACR_ID" --role AcrPull -o table
az containerapp registry list -g rg-nexus6-swc -n ca-nexus6-hosted-agent -o table
az containerapp revision restart -g rg-nexus6-swc -n ca-nexus6-hosted-agent --revision "$REVISION"
```

ACR admin user や registry password は使わない。

### Revision が Healthy にならない

```bash
az containerapp revision list \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query '[].{name:name,active:properties.active,runningState:properties.runningState,healthState:properties.healthState}' \
  -o table

az containerapp logs show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --tail 100
```

環境変数、Key Vault RBAC、Storage Queue RBAC、Foundry RBAC を順に確認する。
