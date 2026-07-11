using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ12: アプリデプロイ - Container App + Functions をデプロイし環境変数を設定
/// </summary>
public class Step13AppDeploy : ISetupStep
{
    private readonly AzureCliWrapper _az;
    private readonly NonInteractiveConfig _niConfig;

    public int StepNumber => 13;
    public string Name => "アプリデプロイ";

    public Step13AppDeploy(AzureCliWrapper az, NonInteractiveConfig niConfig)
    {
        _az = az;
        _niConfig = niConfig;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure
            ?? throw new InvalidOperationException("Azure 構成が未完了です。Step 4 を先に実行してください。");
        var deployment = state.Deployment
            ?? throw new InvalidOperationException("デプロイが未完了です。Step 5 を先に実行してください。");

        Console.WriteLine("  アプリケーションをデプロイ中...\n");

        // リポジトリルートを特定
        var repoRoot = FindRepoRoot();

        // ポータルURL決定: Container App の /news-portal/ パスで配信（Static Website は MCAPS Policy で外部アクセス不可）
        if (!string.IsNullOrEmpty(deployment.ContainerAppUrl))
        {
            deployment.PortalBaseUrl = deployment.ContainerAppUrl.TrimEnd('/') + "/news-portal/";
        }
        else if (string.IsNullOrEmpty(deployment.PortalBaseUrl) && !string.IsNullOrEmpty(deployment.StorageAccountPortal))
        {
            deployment.PortalBaseUrl = $"https://{deployment.StorageAccountPortal}.z1.web.core.windows.net";
        }

        // =====================================================
        // 0. WebIQ API Key → Key Vault 登録
        // =====================================================
        if (!string.IsNullOrEmpty(deployment.WebIqBaseUrl) && !string.IsNullOrEmpty(deployment.KeyVaultUri))
        {
            Console.WriteLine("  🔑 WebIQ API Key を Key Vault に登録中...");
            var vaultName = new Uri(deployment.KeyVaultUri).Host.Split('.')[0];
            string? webIqApiKey;
            if (_niConfig.Enabled)
            {
                webIqApiKey = _niConfig.WebIqApiKey;
            }
            else
            {
                Console.Write("    WebIQ API Key を入力 (スキップは Enter): ");
                webIqApiKey = Console.ReadLine()?.Trim();
            }
            if (!string.IsNullOrWhiteSpace(webIqApiKey))
            {
                await _az.RunAsync(
                    $"keyvault secret set --vault-name {vaultName} " +
                    $"--name webiq-api-key " +
                    $"--value {webIqApiKey}",
                    silent: true);
                Console.WriteLine("    ✓ webiq-api-key を Key Vault に保存しました");
            }
            else
            {
                Console.WriteLine("    ⚠️ スキップ (後で手動設定してください)");
            }
        }

        // =====================================================
        // 1. Container App: news-analysis-agent (Hosted Agent)
        // =====================================================
        Console.WriteLine("  🐳 news-analysis-agent をビルド・デプロイ中...");
        if (!string.IsNullOrEmpty(deployment.AcrLoginServer))
        {
            var imageName = $"{deployment.AcrLoginServer}/news-analysis-agent:latest";
            Console.WriteLine($"    ACR ビルド: {imageName}");
            var dockerfilePath = Path.Combine(repoRoot, "src", "news-analysis-agent", "Dockerfile");
            var contextPath = Path.Combine(repoRoot, "src", "news-analysis-agent");
            await _az.RunAsync(
                $"acr build --registry {deployment.AcrLoginServer.Split('.')[0]} " +
                $"--image news-analysis-agent:latest " +
                $"--file {dockerfilePath} {contextPath}",
                silent: true);

            Console.WriteLine("    Container App に ACR レジストリを設定中...");
            var acrName = deployment.AcrLoginServer.Split('.')[0];
            try
            {
                await _az.RunAsync(
                    $"containerapp registry set --name {deployment.ContainerAppNameAgent} " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--server {deployment.AcrLoginServer} " +
                    $"--identity system",
                    silent: true);
            }
            catch
            {
                // Managed Identity が未設定の場合は admin credentials で試行
                await _az.RunAsync($"acr update --name {acrName} --admin-enabled true", silent: true);
                var creds = await _az.RunAsync($"acr credential show --name {acrName} --query \"{{username:username,password:passwords[0].value}}\" -o json", silent: true);
                var credDoc = System.Text.Json.JsonDocument.Parse(creds);
                var username = credDoc.RootElement.GetProperty("username").GetString()!;
                var password = credDoc.RootElement.GetProperty("password").GetString()!;
                await _az.RunAsync(
                    $"containerapp registry set --name {deployment.ContainerAppNameAgent} " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--server {deployment.AcrLoginServer} " +
                    $"--username {username} --password {password}",
                    silent: true);
            }

            Console.WriteLine("    Container App を更新中...");
            await _az.RunAsync(
                $"containerapp update --name {deployment.ContainerAppNameAgent} " +
                $"--resource-group {azure.ResourceGroup} " +
                $"--image {imageName}",
                silent: true);

            // 環境変数設定
            var agentEnvVars = BuildAgentEnvironmentVariables(deployment);
            await SetContainerAppEnvVars(deployment.ContainerAppNameAgent, azure.ResourceGroup, agentEnvVars);

            Console.WriteLine("    ✓ Hosted Agent デプロイ完了");
        }
        else
        {
            Console.WriteLine("    ⚠️ ACR が未設定のためスキップ");
        }

        // =====================================================
        // 2. Container App: news-analysis-webapp
        // =====================================================
        Console.WriteLine("\n  🌐 news-analysis-webapp をビルド・デプロイ中...");
        var webappDockerfile = Path.Combine(repoRoot, "src", "news-analysis-webapp", "Dockerfile");
        if (!string.IsNullOrEmpty(deployment.AcrLoginServer) && File.Exists(webappDockerfile))
        {
            var webappImage = $"{deployment.AcrLoginServer}/news-analysis-webapp:latest";
            Console.WriteLine($"    ACR ビルド: {webappImage}");
            var webappContext = Path.Combine(repoRoot, "src");
            await _az.RunAsync(
                $"acr build --registry {deployment.AcrLoginServer.Split('.')[0]} " +
                $"--image news-analysis-webapp:latest " +
                $"--file {webappDockerfile} {webappContext}",
                silent: true);

            // webapp Container App がまだなければ作成を試みる
            try
            {
                await _az.RunAsync(
                    $"containerapp show --name {deployment.ContainerAppNameWebapp} " +
                    $"--resource-group {azure.ResourceGroup} --output none",
                    silent: true);
            }
            catch
            {
                Console.WriteLine($"    Container App '{deployment.ContainerAppNameWebapp}' を新規作成中...");
                // 既存のagent Container Appから環境IDを取得
                var envIdJson = await _az.RunAsync(
                    $"containerapp show --name {deployment.ContainerAppNameAgent} " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--query properties.environmentId -o tsv",
                    silent: true);
                var envId = envIdJson.Trim();
                await _az.RunAsync(
                    $"containerapp create --name {deployment.ContainerAppNameWebapp} " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--environment {envId} " +
                    $"--image {webappImage} " +
                    $"--registry-server {deployment.AcrLoginServer} " +
                    $"--registry-identity system " +
                    $"--ingress external --target-port 8080 " +
                    $"--min-replicas 0 --max-replicas 2 " +
                    $"--system-assigned",
                    silent: true);
            }

            await _az.RunAsync(
                $"containerapp update --name {deployment.ContainerAppNameWebapp} " +
                $"--resource-group {azure.ResourceGroup} " +
                $"--image {webappImage}",
                silent: true);

            // 環境変数設定
            var webappEnvVars = BuildWebappEnvironmentVariables(deployment);
            await SetContainerAppEnvVars(deployment.ContainerAppNameWebapp, azure.ResourceGroup, webappEnvVars);

            // RBAC: webapp マネージドID にロールを割り当て
            Console.WriteLine("    Webapp RBAC を設定中...");
            try
            {
                var webappPrincipalId = (await _az.RunAsync(
                    $"containerapp show --name {deployment.ContainerAppNameWebapp} " +
                    $"--resource-group {azure.ResourceGroup} --query identity.principalId -o tsv",
                    silent: true)).Trim();

                if (!string.IsNullOrEmpty(webappPrincipalId))
                {
                    var rgScope = $"/subscriptions/{azure.SubscriptionId}/resourceGroups/{azure.ResourceGroup}";
                    var aiAccountName = GetResourceName(deployment.FoundryEndpoint);
                    // Cognitive Services User (account level - covers OpenAI + AIServices/agents)
                    await _az.RunAsync(
                        $"role assignment create --assignee {webappPrincipalId} " +
                        $"--role \"Cognitive Services User\" --scope {rgScope}/providers/Microsoft.CognitiveServices/accounts/{aiAccountName}",
                        silent: true);
                    // Key Vault Secrets User
                    if (!string.IsNullOrEmpty(deployment.KeyVaultUri))
                    {
                        var kvName = new Uri(deployment.KeyVaultUri).Host.Split('.')[0];
                        await _az.RunAsync(
                            $"role assignment create --assignee {webappPrincipalId} " +
                            $"--role \"Key Vault Secrets User\" --scope {rgScope}/providers/Microsoft.KeyVault/vaults/{kvName}",
                            silent: true);
                    }
                    // Storage Blob Data Contributor
                    await _az.RunAsync(
                        $"role assignment create --assignee {webappPrincipalId} " +
                        $"--role \"Storage Blob Data Contributor\" --scope {rgScope}/providers/Microsoft.Storage/storageAccounts/{deployment.StorageAccountSkills}",
                        silent: true);
                    Console.WriteLine("    ✓ Webapp RBAC 完了");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ Webapp RBAC 設定に失敗: {ex.Message[..Math.Min(150, ex.Message.Length)]}");
            }

            Console.WriteLine("    ✓ Webapp デプロイ完了");
        }
        else
        {
            Console.WriteLine("    ⚠️ Webapp Dockerfileが存在しないかACR未設定のためスキップ");
        }

        // =====================================================
        // 3. Azure Functions: news-trigger-function
        // =====================================================
        Console.WriteLine("\n  ⚡ news-trigger-function をデプロイ中...");
        if (!string.IsNullOrEmpty(deployment.FunctionAppName))
        {
            var funcProjectPath = Path.Combine(repoRoot, "src", "news-trigger-function");
            Console.WriteLine($"    func azure functionapp publish {deployment.FunctionAppName}");

            try
            {
                await _az.RunAsync(
                    $"functionapp deployment source config-zip " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--name {deployment.FunctionAppName} " +
                    $"--src {funcProjectPath}",
                    silent: true);
                Console.WriteLine("    ✓ Functions デプロイ完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ Functions デプロイに失敗: {ex.Message}");
                Console.WriteLine($"    手動デプロイ: cd {funcProjectPath} && func azure functionapp publish {deployment.FunctionAppName}");
            }

            // アプリ設定
            Console.WriteLine("    Functions アプリ設定を更新中...");
            try
            {
                var funcSettings = BuildFunctionAppSettings(deployment);
                await SetFunctionAppSettings(deployment.FunctionAppName, azure.ResourceGroup, funcSettings);
                Console.WriteLine("    ✓ Functions アプリ設定完了");
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"    ⚠️ Functions アプリ設定に失敗: {ex2.Message[..Math.Min(200, ex2.Message.Length)]}");
            }
        }
        else
        {
            Console.WriteLine("    ⚠️ Function App が未設定のためスキップ");
        }

        Console.WriteLine("\n  ✓ アプリデプロイ完了");
    }

    /// <summary>
    /// Hosted Agent (Container App) の全環境変数
    /// </summary>
    private static Dictionary<string, string> BuildAgentEnvironmentVariables(DeploymentResult deployment)
    {
        // Assistants API はアカウントエンドポイントのみで動作するため FoundryEndpoint を使用
        var vars = new Dictionary<string, string>
        {
            // Foundry
            ["Foundry__ProjectEndpoint"] = deployment.FoundryEndpoint,
            ["Foundry__ApiVersion"] = "2024-10-21",
            ["Foundry__AssistantsApiVersion"] = "2024-10-01-preview",
            ["Foundry__DefaultModelDeployment"] = "gpt-5",
            ["Foundry__MaxCompletionTokens"] = "4096",
            ["Foundry__NotificationModelDeployment"] = "gpt-5",
            ["Foundry__Assistant__WebResearchName"] = "nexus6-web-research",
            ["Foundry__Assistant__ImpactName"] = "nexus6-business-impact-filesearch",
            ["Foundry__Assistant__MobileRecommendName"] = "nexus6-mobile-recommend-filesearch",
            ["Foundry__Assistant__EcommerceRecommendName"] = "nexus6-ecommerce-recommend-filesearch",
            ["Foundry__Assistant__FintechRecommendName"] = "nexus6-fintech-recommend-filesearch",
            ["Foundry__Assistant__RunMaxWaitSeconds"] = "180",
            ["Foundry__FileSearchVectorStoreId"] = deployment.FoundryVectorStoreId,

            // Fabric
            ["Fabric__SqlEndpoint"] = deployment.FabricSqlEndpoint,
            ["Fabric__Database"] = deployment.FabricDatabase,

            // Infrastructure
            ["KeyVault__Uri"] = deployment.KeyVaultUri,
            ["Storage__Account"] = deployment.StorageAccountSkills,
            ["AzureMonitor__ConnectionString"] = deployment.AppInsightsConnectionString,

            // WebIQ
            ["WebIq__BaseUrl"] = deployment.WebIqBaseUrl,
            ["WebIq__KeyVaultSecretName"] = "webiq-api-key",
        };

        return vars;
    }

    /// <summary>
    /// Webapp (Container App) の全環境変数
    /// </summary>
    private static Dictionary<string, string> BuildWebappEnvironmentVariables(DeploymentResult deployment)
    {
        // Assistants API はアカウントエンドポイントのみで動作するため FoundryEndpoint を使用
        var vars = new Dictionary<string, string>
        {
            // Server
            ["Urls"] = "http://+:8080",

            // Domain Config
            

            // Foundry
            ["Foundry__ProjectEndpoint"] = deployment.FoundryEndpoint,
            ["Foundry__ApiVersion"] = "2024-10-21",
            ["Foundry__AssistantsApiVersion"] = "2024-10-01-preview",
            ["Foundry__DefaultModelDeployment"] = "gpt-5",
            ["Foundry__MaxCompletionTokens"] = "4096",
            ["Foundry__NotificationModelDeployment"] = "gpt-5",
            ["Foundry__Assistant__WebResearchName"] = "nexus6-web-research",
            ["Foundry__Assistant__ImpactName"] = "nexus6-business-impact-filesearch",
            ["Foundry__Assistant__MobileRecommendName"] = "nexus6-mobile-recommend-filesearch",
            ["Foundry__Assistant__EcommerceRecommendName"] = "nexus6-ecommerce-recommend-filesearch",
            ["Foundry__Assistant__FintechRecommendName"] = "nexus6-fintech-recommend-filesearch",
            ["Foundry__Assistant__RunMaxWaitSeconds"] = "180",
            ["Foundry__FileSearchVectorStoreId"] = deployment.FoundryVectorStoreId,

            // Fabric
            ["Fabric__SqlEndpoint"] = deployment.FabricSqlEndpoint,
            ["Fabric__Database"] = deployment.FabricDatabase,

            // News Portal
            ["NewsPortal__BaseUrl"] = deployment.PortalBaseUrl,

            // Key Vault
            ["KeyVault__Uri"] = deployment.KeyVaultUri,

            // CORS - Container App の webapp URL (自身)
            ["AllowedOrigins__0"] = !string.IsNullOrEmpty(deployment.ContainerAppUrl)
                ? deployment.ContainerAppUrl
                : "http://localhost:5173",

            // Monitoring
            ["AzureMonitor__ConnectionString"] = deployment.AppInsightsConnectionString,

            // WebIQ
            ["WebIq__BaseUrl"] = deployment.WebIqBaseUrl,
            ["WebIq__KeyVaultSecretName"] = "webiq-api-key",
        };

        return vars;
    }

    /// <summary>
    /// Functions のアプリ設定 (Bicep で設定済みの項目以外)
    /// </summary>
    private static Dictionary<string, string> BuildFunctionAppSettings(DeploymentResult deployment)
    {
        return new Dictionary<string, string>
        {
            ["Storage__Account"] = deployment.StorageAccountSkills,
            ["Storage__QueueName"] = "news-analysis-jobs",
            ["NewsPortal__BaseUrl"] = deployment.PortalBaseUrl,
            ["NewsPortal__ArticlePathPrefix"] = "article-",
            ["NewsPortal__MaxEnqueuePerRun"] = "10",
            ["DailyRunCron"] = "0 0 0 * * *", // 毎日 UTC 0:00
        };
    }

    private async Task SetContainerAppEnvVars(string appName, string resourceGroup, Dictionary<string, string> envVars)
    {
        var filtered = envVars.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToList();
        if (filtered.Count == 0) return;

        // az containerapp update --set-env-vars は一度に複数指定可能
        var envArgs = string.Join(" ", filtered.Select(kv => $"{kv.Key}={kv.Value}"));

        Console.WriteLine($"    環境変数 ({filtered.Count} 件) を設定中...");
        await _az.RunAsync(
            $"containerapp update --name {appName} " +
            $"--resource-group {resourceGroup} " +
            $"--set-env-vars {envArgs}",
            silent: true);
    }

    private async Task SetFunctionAppSettings(string functionAppName, string resourceGroup, Dictionary<string, string> settings)
    {
        var filtered = settings.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToList();
        if (filtered.Count == 0) return;

        // az functionapp config appsettings set --settings "key=value" "key=value" ...
        var settingsArgs = string.Join(" ", filtered.Select(kv => $"\"{kv.Key}={kv.Value}\""));

        await _az.RunAsync(
            $"functionapp config appsettings set " +
            $"--name {functionAppName} " +
            $"--resource-group {resourceGroup} " +
            $"--settings {settingsArgs}",
            silent: true);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        // fallback: search from current directory
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("リポジトリルートが見つかりません。");
    }

    /// <summary>FoundryEndpoint URLからCognitive Servicesリソース名を抽出</summary>
    private static string GetResourceName(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint)) return string.Empty;
        try { return new Uri(endpoint).Host.Split('.')[0]; }
        catch { return string.Empty; }
    }
}
