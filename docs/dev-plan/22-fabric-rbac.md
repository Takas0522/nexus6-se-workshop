# 22. Fabric Workspace RBAC 付与記録

関連: [開発計画 README](README.md)

## 対象

- Track: `s-fabric-rbac-grant`
- ACA: `ca-nexus6-hosted-agent`
- Resource group: `rg-nexus6-swc`
- Fabric Workspace: `fabric_seworkshop_ws1`
- Workspace ID: `49d1a6c2-96c0-4da3-a1df-911b19a2f0bc`
- 目的: ACA System Assigned Managed Identity を Fabric Workspace Contributor に追加し、Fabric T-SQL endpoint の SQL 18456 を解消する。

## 実行コマンド

```bash
az containerapp identity show \
  -n ca-nexus6-hosted-agent \
  -g rg-nexus6-swc \
  --query principalId -o tsv

az account get-access-token \
  --resource https://api.fabric.microsoft.com \
  --query accessToken -o tsv

POST https://api.fabric.microsoft.com/v1/workspaces/49d1a6c2-96c0-4da3-a1df-911b19a2f0bc/roleAssignments
Content-Type: application/json

{
  "principal": {
    "id": "65c3be76-e682-43d7-b8d4-7a31b9acd1f6",
    "type": "ServicePrincipal"
  },
  "role": "Contributor"
}
```

## 付与結果

- ACA MI principalId: `65c3be76-e682-43d7-b8d4-7a31b9acd1f6`
- principal type: `ServicePrincipal`
- 付与 role: `Contributor`
- POST 結果: `201 Created`

POST 応答抜粋:

```json
{
  "id": "65c3be76-e682-43d7-b8d4-7a31b9acd1f6",
  "principal": {
    "id": "65c3be76-e682-43d7-b8d4-7a31b9acd1f6",
    "type": "ServicePrincipal"
  },
  "role": "Contributor"
}
```

GET 確認応答抜粋:

```json
[
  {
    "id": "65c3be76-e682-43d7-b8d4-7a31b9acd1f6",
    "principal": {
      "id": "65c3be76-e682-43d7-b8d4-7a31b9acd1f6",
      "displayName": "ca-nexus6-hosted-agent",
      "type": "ServicePrincipal",
      "servicePrincipalDetails": {
        "aadAppId": "3d795490-aa4b-46e7-bf94-2c397cb3d911"
      }
    },
    "role": "Contributor"
  }
]
```

## ACA Revision restart

```bash
az containerapp revision restart \
  -n ca-nexus6-hosted-agent \
  -g rg-nexus6-swc \
  --revision ca-nexus6-hosted-agent--0000003
```

確認結果:

```text
latest=ca-nexus6-hosted-agent--0000003
runningState=Running
fqdn=ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io
```

## 再 E2E

```bash
curl -sS -X POST \
  https://ca-nexus6-hosted-agent.ambitiousgrass-408dc79b.swedencentral.azurecontainerapps.io/devui/run \
  -H 'Content-Type: application/json' \
  -d '{"originalNewsText":"日銀が政策金利を0.5%に引き上げ。住宅ローン金利上昇懸念。"}'
```

結果概要:

- HTTP 応答: JSON 正常返却。
- `impactReasons`: `Fabric KPI heuristic fallback score ... based on margin, churn, FX exposure, and risk summary volume.`
- ACA console log tail 300/1000 で `Error Number:18456` / `Login failed` / `Fabric monthly revenue query failed` / `Fabric Mobile KPI query failed` / `Fabric Ecommerce KPI query failed` / `Fabric Fintech KPI query failed` は検出なし。
- `dataReferences` には `mock-skill-ds` が残存。ログ上、これは Fabric RBAC ではなく Foundry chat completions の `BadRequest` により `MockFoundryAgentClient` に fallback した経路。

E2E 応答抜粋:

```json
{
  "impactReasons": [
    "mobile: Fabric KPI heuristic fallback score 2.7 based on margin, churn, FX exposure, and risk summary volume.",
    "fintech: Fabric KPI heuristic fallback score 1.9 based on margin, churn, FX exposure, and risk summary volume.",
    "ecommerce: Fabric KPI heuristic fallback score 1.7 based on margin, churn, FX exposure, and risk summary volume."
  ],
  "recommendations": [
    {
      "division": 0,
      "dataReferences": ["mobile_ai.risk_summary", "mock-skill-ds"]
    }
  ]
}
```

ACA console log 抜粋:

```text
Foundry chat completions returned BadRequest. Falling back to mock client.
Teams Workflows URL is not configured for Ecommerce; using mock fallback
Teams Workflows URL is not configured for Mobile; using mock fallback
Teams Workflows URL is not configured for Fintech; using mock fallback
```

18456 検索結果:

```text
az containerapp logs show --tail 1000 --type console | grep -E '18456|Login failed|Fabric .* query failed'
# no matches
```

## 判定

- Fabric Workspace RBAC 付与: 完了。
- SQL 18456: 再 E2E ログでは再発なし。
- Fabric SQL 実クエリ: 現行コードは成功時ログを出さないため、失敗ログ不在と KPI heuristic の Fabric KPI 入力利用で確認。今後、`FabricDataPlugin` / 各業務 DataPlugin の成功時 `LogInformation` を追加すると証跡が明確になる。
- `mock-skill-ds`: 残存。原因は Foundry chat completions `BadRequest` による LLM レイヤーの mock fallback であり、Fabric RBAC とは別課題。

## 残課題

1. Foundry chat completions `BadRequest` の解消。
2. Teams Workflows URL 未設定の解消。
3. Fabric SQL 成功時ログの追加検討。
