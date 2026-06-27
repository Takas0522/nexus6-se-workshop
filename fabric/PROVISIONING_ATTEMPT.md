# Fabric REST provisioning attempt

- Timestamp (UTC): 2026-06-27T09:28:27Z
- Target workspace: fabric_seworkshop_ws1
- Action: lookup workspace via GET https://api.fabric.microsoft.com/v1/workspaces
- Workspace lookup HTTP status: 200
- Workspace ID: 49d1a6c2-96c0-4da3-a1df-911b19a2f0bc
- Result: workspace lookup succeeded. Resource creation was not attempted automatically to avoid changing existing Fabric resources from this session; use the manual portal/import steps below.

Response excerpt:
```json
{
  "value": [
    {
      "id": "49d1a6c2-96c0-4da3-a1df-911b19a2f0bc",
      "displayName": "fabric_seworkshop_ws1",
      "description": "",
      "type": "Workspace",
      "capacityId": "3d53896f-3045-4654-98fc-db56c4bae0d4",
      "capacityRegion": "Sweden Central"
    },
    {
      "id": "5e342652-7d5b-4227-9f78-7a1afecbe370",
      "displayName": "My workspace",
      "description": "",
      "type": "Personal"
    }
  ]
}
```
