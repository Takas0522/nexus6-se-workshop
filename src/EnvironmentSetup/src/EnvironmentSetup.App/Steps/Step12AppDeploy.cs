using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ12: アプリデプロイ - Container App + Functions をデプロイし環境変数を設定
/// </summary>
public class Step12AppDeploy : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 12;
    public string Name => "アプリデプロイ";

    public Step12AppDeploy(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure
            ?? throw new InvalidOperationException("Azure 構成が未完了です。Step 4 を先に実行してください。");
        var deployment = state.Deployment
            ?? throw new InvalidOperationException("デプロイが未完了です。Step 5 を先に実行してください。");

        Console.WriteLine("  アプリケーションをデプロイ中...\n");

        // ポータルURL決定
        if (string.IsNullOrEmpty(deployment.PortalBaseUrl) && !string.IsNullOrEmpty(deployment.StorageAccountPortal))
        {
            deployment.PortalBaseUrl = $"https://{deployment.StorageAccountPortal}.z1.web.core.windows.net";
        }

        // DomainConfig BlobUri
        var domainConfigBlobUri = state.DomainConfigBlobUri ?? "";

        // =====================================================
        // 1. Container App: news-analysis-agent (Hosted Agent)
        // =====================================================
        Console.WriteLine("  🐳 news-analysis-agent をビルド・デプロイ中...");
        if (!string.IsNullOrEmpty(deployment.AcrLoginServer))
        {
            var imageName = $"{deployment.AcrLoginServer}/news-analysis-agent:latest";
            Console.WriteLine($"    ACR ビルド: {imageName}");
            await _az.RunAsync(
                $"acr build --registry {deployment.AcrLoginServer.Split('.')[0]} " +
                $"--image news-analysis-agent:latest " +
                $"--file src/news-analysis-agent/Dockerfile src/news-analysis-agent",
                silent: true);

            Console.WriteLine("    Container App を更新中...");
            await _az.RunAsync(
                $"containerapp update --name {deployment.ContainerAppNameAgent} " +
                $"--resource-group {azure.ResourceGroup} " +
                $"--image {imageName}",
                silent: true);

            // 環境変数設定
            var agentEnvVars = BuildAgentEnvironmentVariables(deployment, domainConfigBlobUri);
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
        if (!string.IsNullOrEmpty(deployment.AcrLoginServer))
        {
            var webappImage = $"{deployment.AcrLoginServer}/news-analysis-webapp:latest";
            Console.WriteLine($"    ACR ビルド: {webappImage}");
            await _az.RunAsync(
                $"acr build --registry {deployment.AcrLoginServer.Split('.')[0]} " +
                $"--image news-analysis-webapp:latest " +
                $"--file src/news-analysis-webapp/Dockerfile src/news-analysis-webapp",
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
                await _az.RunAsync(
                    $"containerapp create --name {deployment.ContainerAppNameWebapp} " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--image {webappImage} " +
                    $"--ingress external --target-port 5100 " +
                    $"--min-replicas 0 --max-replicas 2",
                    silent: true);
            }

            await _az.RunAsync(
                $"containerapp update --name {deployment.ContainerAppNameWebapp} " +
                $"--resource-group {azure.ResourceGroup} " +
                $"--image {webappImage}",
                silent: true);

            // 環境変数設定
            var webappEnvVars = BuildWebappEnvironmentVariables(deployment, domainConfigBlobUri);
            await SetContainerAppEnvVars(deployment.ContainerAppNameWebapp, azure.ResourceGroup, webappEnvVars);

            Console.WriteLine("    ✓ Webapp デプロイ完了");
        }
        else
        {
            Console.WriteLine("    ⚠️ ACR が未設定のためスキップ");
        }

        // =====================================================
        // 3. Azure Functions: news-trigger-function
        // =====================================================
        Console.WriteLine("\n  ⚡ news-trigger-function をデプロイ中...");
        if (!string.IsNullOrEmpty(deployment.FunctionAppName))
        {
            var funcProjectPath = "src/news-trigger-function";
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
            var funcSettings = BuildFunctionAppSettings(deployment, domainConfigBlobUri);
            await SetFunctionAppSettings(deployment.FunctionAppName, azure.ResourceGroup, funcSettings);
            Console.WriteLine("    ✓ Functions アプリ設定完了");
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
    private static Dictionary<string, string> BuildAgentEnvironmentVariables(DeploymentResult deployment, string domainConfigBlobUri)
    {
        var vars = new Dictionary<string, string>
        {
            // Domain Config (Blob-driven)
            ["DomainConfig__BlobUri"] = domainConfigBlobUri,

            // Foundry
            ["Foundry__ProjectEndpoint"] = deployment.FoundryEndpoint,
            ["Foundry__ApiVersion"] = "2024-10-21",
            ["Foundry__AssistantsApiVersion"] = "2025-05-01",
            ["Foundry__DefaultModelDeployment"] = "gpt-5.4",
            ["Foundry__MaxCompletionTokens"] = "4096",
            ["Foundry__NotificationModelDeployment"] = "gpt-5.4",
            ["Foundry__Assistant__WebResearchName"] = "nexus6-web-research",
            ["Foundry__Assistant__ImpactName"] = "nexus6-business-impact-filesearch",
            ["Foundry__Assistant__MobileRecommendName"] = "nexus6-mobile-recommend-filesearch",
            ["Foundry__Assistant__EcommerceRecommendName"] = "nexus6-ecommerce-recommend-filesearch",
            ["Foundry__Assistant__FintechRecommendName"] = "nexus6-fintech-recommend-filesearch",
            ["Foundry__Assistant__RunMaxWaitSeconds"] = "60",

            // Fabric
            ["Fabric__SqlEndpoint"] = deployment.FabricSqlEndpoint,
            ["Fabric__Database"] = deployment.FabricDatabase,

            // Infrastructure
            ["KeyVault__Uri"] = deployment.KeyVaultUri,
            ["Storage__Account"] = deployment.StorageAccountSkills,
            ["AzureMonitor__ConnectionString"] = deployment.AppInsightsConnectionString,
        };

        return vars;
    }

    /// <summary>
    /// Webapp (Container App) の全環境変数
    /// </summary>
    private static Dictionary<string, string> BuildWebappEnvironmentVariables(DeploymentResult deployment, string domainConfigBlobUri)
    {
        var vars = new Dictionary<string, string>
        {
            // Domain Config
            ["DomainConfig__BlobUri"] = domainConfigBlobUri,

            // Foundry
            ["Foundry__ProjectEndpoint"] = deployment.FoundryEndpoint,
            ["Foundry__ApiVersion"] = "2024-10-21",
            ["Foundry__AssistantsApiVersion"] = "2025-05-01",
            ["Foundry__DefaultModelDeployment"] = "gpt-5.4",
            ["Foundry__MaxCompletionTokens"] = "4096",
            ["Foundry__NotificationModelDeployment"] = "gpt-5.4",
            ["Foundry__Assistant__WebResearchName"] = "nexus6-web-research",
            ["Foundry__Assistant__ImpactName"] = "nexus6-business-impact-filesearch",
            ["Foundry__Assistant__MobileRecommendName"] = "nexus6-mobile-recommend-filesearch",
            ["Foundry__Assistant__EcommerceRecommendName"] = "nexus6-ecommerce-recommend-filesearch",
            ["Foundry__Assistant__FintechRecommendName"] = "nexus6-fintech-recommend-filesearch",
            ["Foundry__Assistant__RunMaxWaitSeconds"] = "60",

            // Fabric
            ["Fabric__SqlEndpoint"] = deployment.FabricSqlEndpoint,
            ["Fabric__Database"] = deployment.FabricDatabase,

            // News Portal
            ["NewsPortal__BaseUrl"] = deployment.PortalBaseUrl,

            // CORS - Container App の webapp URL (自身)
            ["AllowedOrigins__0"] = !string.IsNullOrEmpty(deployment.ContainerAppUrl)
                ? deployment.ContainerAppUrl
                : "http://localhost:5173",

            // Monitoring
            ["AzureMonitor__ConnectionString"] = deployment.AppInsightsConnectionString,
        };

        return vars;
    }

    /// <summary>
    /// Functions のアプリ設定 (Bicep で設定済みの項目以外)
    /// </summary>
    private static Dictionary<string, string> BuildFunctionAppSettings(DeploymentResult deployment, string domainConfigBlobUri)
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

        // az functionapp config appsettings set --settings key=value key=value ...
        var settingsArgs = string.Join(" ", filtered.Select(kv => $"{kv.Key}={kv.Value}"));

        await _az.RunAsync(
            $"functionapp config appsettings set " +
            $"--name {functionAppName} " +
            $"--resource-group {resourceGroup} " +
            $"--settings {settingsArgs}",
            silent: true);
    }
}
