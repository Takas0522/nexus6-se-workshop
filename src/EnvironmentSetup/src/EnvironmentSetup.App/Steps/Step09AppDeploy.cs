using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ9: アプリデプロイ - Container App + Functions をデプロイ
/// </summary>
public class Step09AppDeploy : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 9;
    public string Name => "アプリデプロイ";

    public Step09AppDeploy(AzureCliWrapper az)
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

        // 1. Container App (news-analysis-agent)
        Console.WriteLine("  🐳 news-analysis-agent をビルド・デプロイ中...");
        var dockerfilePath = "src/news-analysis-agent/Dockerfile";
        var contextPath = "src/news-analysis-agent";

        if (!string.IsNullOrEmpty(deployment.AcrLoginServer))
        {
            var imageName = $"{deployment.AcrLoginServer}/news-analysis-agent:latest";

            Console.WriteLine($"    ACR ビルド: {imageName}");
            await _az.RunAsync(
                $"acr build --registry {deployment.AcrLoginServer.Split('.')[0]} " +
                $"--image news-analysis-agent:latest " +
                $"--file {dockerfilePath} {contextPath}",
                silent: true);

            Console.WriteLine("    Container App を更新中...");
            await _az.RunAsync(
                $"containerapp update --name ca-nexus6-hosted-agent " +
                $"--resource-group {azure.ResourceGroup} " +
                $"--image {imageName}",
                silent: true);

            // 環境変数設定
            var envVars = new Dictionary<string, string>
            {
                ["Foundry__ProjectEndpoint"] = deployment.FoundryEndpoint,
                ["Fabric__SqlEndpoint"] = deployment.FabricSqlEndpoint,
                ["KeyVault__Uri"] = deployment.KeyVaultUri,
                ["AzureMonitor__ConnectionString"] = deployment.AppInsightsConnectionString,
                ["Storage__Account"] = deployment.StorageAccountSkills,
            };

            var envArgs = string.Join(" ", envVars
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={kv.Value}"));

            if (!string.IsNullOrEmpty(envArgs))
            {
                await _az.RunAsync(
                    $"containerapp update --name ca-nexus6-hosted-agent " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--set-env-vars {envArgs}",
                    silent: true);
            }

            Console.WriteLine("    ✓ Container App デプロイ完了");
        }
        else
        {
            Console.WriteLine("    ⚠️ ACR が未設定のためスキップ");
        }

        // 2. Azure Functions (news-trigger-function)
        Console.WriteLine("\n  ⚡ news-trigger-function をデプロイ中...");
        if (!string.IsNullOrEmpty(deployment.FunctionAppName))
        {
            var funcProjectPath = "src/news-trigger-function";
            Console.WriteLine($"    func azure functionapp publish {deployment.FunctionAppName}");

            // func CLI を使用してデプロイ
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
        }
        else
        {
            Console.WriteLine("    ⚠️ Function App が未設定のためスキップ");
        }

        Console.WriteLine("\n  ✓ アプリデプロイ完了");
    }
}
