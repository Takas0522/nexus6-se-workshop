# Nexus6 Infrastructure as Code (Bicep)

Comprehensive Azure infrastructure deployment using Bicep templates. All resources are deployed to a single Resource Group in Sweden Central region.

## Architecture Overview

```
infra/
├── main.bicep                    # Orchestration file - calls all modules
├── main.bicepparam               # Parameter file with default values
└── modules/
    ├── monitoring.bicep          # Log Analytics Workspace + Application Insights
    ├── keyvault.bicep            # Key Vault (Standard, RBAC enabled)
    ├── container-registry.bicep  # Container Registry (Basic SKU)
    ├── container-apps-env.bicep  # Container Apps Environment
    ├── container-app.bicep       # Container App (Hosted Agent)
    ├── storage.bicep             # Storage Accounts (×2)
    ├── ai-foundry.bicep          # AI Services + Project + Model Deployments
    ├── ai-search.bicep           # AI Search (Standard tier)
    ├── fabric.bicep              # Fabric Capacity (F4)
    ├── functions.bicep           # App Service Plan (Y1) + Function App
    ├── diagnostics.bicep         # Diagnostic Settings (ログ集約)
    └── rbac.bicep                # RBAC Role Assignments
```

## Resources Deployed

### 1. Monitoring (`monitoring.bicep`)
- **Log Analytics Workspace**
  - SKU: PerGB2018
  - Retention: 30 days
- **Application Insights**
  - Type: Web
  - Connected to Log Analytics

### 2. Security (`keyvault.bicep`)
- **Key Vault**
  - SKU: Standard (Family A)
  - RBAC Authorization: Enabled
  - Soft Delete: Enabled
  - Public Network Access: Enabled

### 3. Container Registry (`container-registry.bicep`)
- **Container Registry**
  - SKU: Basic
  - Admin User: Disabled
  - Retention Policy: 30 days

### 4. Container Infrastructure
- **Container Apps Environment** (`container-apps-env.bicep`)
  - Connected to Log Analytics
  - Zero VNet integration (public)
  
- **Container App** (`container-app.bicep`)
  - Hosting Agent (`ca-nexus6-hosted-agent`)
  - Resources: 0.5 vCPU / 1Gi Memory
  - Managed Identity: System Assigned
  - Ingress: External on port 8080
  - Scale: 0-2 replicas
  - Default Image: `mcr.microsoft.com/k8se/quickstart:latest`

### 5. Storage (`storage.bicep`)
- **Skills Storage Account**
  - Type: StorageV2
  - SKU: Standard_LRS
  - Purpose: Skill data storage
  
- **Portal Storage Account**
  - Type: StorageV2
  - SKU: Standard_LRS
  - Static Website: Enabled ($web container)
  - Purpose: Static website hosting

### 6. AI Services (`ai-foundry.bicep`)
- **AI Services Account (Foundry)**
  - Kind: AIServices
  - SKU: S0
  - Custom Subdomain: `fd-nexus6`
  
- **AI Services Project**
  - Kind: AIServicesProject
  
- **Model Deployments** (4 total):
  | Model | SKU | Capacity |
  |-------|-----|----------|
  | gpt-5.4 | GlobalStandard | 500K TPM |
  | text-embedding-3-large | Standard | 120K TPM |
  | model-router | GlobalStandard | 500K TPM |
  | o4-mini | GlobalStandard | 500K TPM |

### 7. AI Search (`ai-search.bicep`)
- **AI Search Service**
  - SKU: Standard
  - Partitions: 1 (configurable)
  - Replicas: 1 (configurable)

### 8. Compute (`functions.bicep`)
- **App Service Plan**
  - SKU: Y1 (Dynamic)
  - OS: Linux
  
- **Function App**
  - Runtime: .NET 8.0 Isolated
  - Authentication: System Assigned Identity

### 9. Access Control (`rbac.bicep`)
Role assignments for **Container App** Managed Identity:
- **AcrPull** on Container Registry
- **Storage Blob Data Contributor** on Skills Storage
- **Storage Table Data Contributor** on Skills Storage
- **Key Vault Secrets User** on Key Vault
- **Cognitive Services OpenAI User** on AI Services
- **Search Index Data Reader** on AI Search

Role assignments for **Function App** Managed Identity:
- **Storage Blob Data Contributor** on Skills & Portal Storage
- **Storage Table Data Contributor** on Skills Storage

### 10. Fabric Capacity (`fabric.bicep`)
- **Microsoft Fabric Capacity**
  - SKU: F4 (configurable)
  - Tier: Fabric

### 11. Diagnostics (`diagnostics.bicep`)
- Key Vault, AI Services, AI Search の全ログ・メトリクスを Log Analytics に集約

## Deployment

### Prerequisites
- Azure CLI installed
- Bicep CLI installed (or use `az bicep` commands)
- Appropriate permissions in Azure subscription

### Deploy to Azure

```bash
# Validate the template
az bicep build --file main.bicep

# Preview changes (what-if)
az deployment group create \
  --resource-group <resource-group-name> \
  --template-file main.bicep \
  --parameters main.bicepparam \
  --what-if

# Deploy to Azure
az deployment group create \
  --resource-group <resource-group-name> \
  --template-file main.bicep \
  --parameters main.bicepparam
```

### Custom Parameters

Edit `main.bicepparam` to customize:
- Project name
- Resource names
- Location (default: swedencentral)
- Tags
- Container App image
- Environment variables

## Key Features

✅ **Infrastructure as Code**: All resources defined in Bicep  
✅ **Modular Design**: Separate modules for each resource type  
✅ **Parameterized**: Configurable via `main.bicepparam`  
✅ **Tagging**: Consistent tags across all resources  
✅ **Security**: RBAC-enabled Key Vault, managed identities  
✅ **Monitoring**: Integrated Application Insights and Log Analytics  
✅ **Scalability**: Configured for auto-scaling where applicable  

## Resource Naming Convention

```
log-{projectName}-swc              # Log Analytics
appi-{projectName}-swc             # Application Insights
kv-{projectName}-swc               # Key Vault
cr{projectName}swc                 # Container Registry
cae-{projectName}-swc              # Container Apps Environment
ca-{projectName}-hosted-agent      # Container App
st{projectName}skill1t2i           # Skills Storage
st{projectName}portal1t2i          # Portal Storage
fd-{projectName}                   # AI Services
iq-knowledge-source                # AI Search
{projectName}LinuxDynamicPlan      # App Service Plan
func-{projectName}-trigger         # Function App
```

## Outputs

The deployment provides the following outputs:
- Log Analytics Workspace ID and name
- Application Insights connection string and key
- Key Vault URI and ID
- Container Registry login server
- Container App URL and FQDN
- Storage Account names and web endpoints
- AI Services endpoints
- Function App details

## References

- Design Document: `docs/infra/resource-review.md`
- Azure Bicep Documentation: https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/
- Module Design Guidelines: Azure Bicep best practices

## Notes

- All resources are deployed to a **single Resource Group**
- Region: **Sweden Central** (swedencentral)
- Fabric Capacity included (F4 SKU)
- VNet integration not included (recommended for production - 段階的導入可)
- Diagnostic Settings で Key Vault / AI Services / AI Search のログを Log Analytics に集約

## Troubleshooting

### Globally Unique Names
Some Azure resources require globally unique names:
- Container Registry: `crnexus6swc`
- Storage Accounts: `stnexus6skill1t2i`, `stnexus6portal1t2i`
- Key Vault: `kv-nexus6-swc`
- AI Services: `fd-nexus6`

If deployment fails due to name conflicts, update values in `main.bicepparam`.

### Authentication Issues
Ensure your Azure CLI is authenticated:
```bash
az login
az account show
```

### Model Deployment Errors
If model deployments fail, verify:
- AI Services region supports the models
- Quota is available for token usage
- Model versions match availability in your region
