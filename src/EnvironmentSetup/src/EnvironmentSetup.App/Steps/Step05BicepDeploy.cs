using System.Text.Json;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ5: Bicep デプロイ - infra/ のテンプレートを使用してAzureリソースをデプロイ
/// </summary>
public class Step05BicepDeploy : ISetupStep
{
    private readonly AzureCliWrapper _az;
    private readonly NonInteractiveConfig _niConfig;

    public int StepNumber => 5;
    public string Name => "Bicep デプロイ";

    public Step05BicepDeploy(AzureCliWrapper az, NonInteractiveConfig niConfig)
    {
        _az = az;
        _niConfig = niConfig;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure
            ?? throw new InvalidOperationException("Azure ログインが未完了です。Step 4 を先に実行してください。");

        // リソースグループ名の決定
        var suffix = Random.Shared.Next(1000, 9999).ToString();
        var defaultRgName = $"rg-nexus6-{suffix}";

        string rgName;
        if (_niConfig.Enabled && !string.IsNullOrEmpty(_niConfig.ResourceGroup))
        {
            rgName = _niConfig.ResourceGroup;
        }
        else if (_niConfig.Enabled)
        {
            rgName = defaultRgName;
        }
        else
        {
            Console.Write($"  リソースグループ名 [{defaultRgName}]: ");
            rgName = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(rgName))
                rgName = defaultRgName;
        }

        azure.ResourceGroup = rgName;

        Console.WriteLine($"\n  リソースグループ: {rgName}");
        Console.WriteLine($"  リージョン: {azure.Region}");
        Console.WriteLine($"  テンプレート: infra/main.bicep\n");

        // リソースグループ作成
        Console.WriteLine("  リソースグループを作成中...");
        await _az.EnsureResourceGroupAsync(rgName, azure.Region);

        // パラメータファイル生成
        // リポジトリルートから infra/ を探す
        var repoRoot = FindRepoRoot() ?? Directory.GetCurrentDirectory();
        var templatePath = Path.Combine(repoRoot, "infra", "main.bicep");
        var paramPath = Path.Combine(repoRoot, "infra", "main.bicepparam");

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Bicep テンプレートが見つかりません: {templatePath}");

        // What-if 実行 (非致命的 - 警告のみ)
        Console.WriteLine("\n  what-if を実行中...\n");
        try
        {
            await _az.WhatIfAsync(rgName, templatePath, File.Exists(paramPath) ? paramPath : null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️ what-if で警告/エラーがありましたが、デプロイは続行します: {ex.Message.Split('\n')[0]}");
        }

        if (!_niConfig.Enabled)
        {
            Console.Write("\n  デプロイを実行しますか? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm != "y" && confirm != "yes")
                throw new OperationCanceledException("ユーザーによりデプロイがキャンセルされました。");
        }

        // デプロイ実行
        Console.WriteLine("\n  デプロイ実行中 (数分かかる場合があります)...\n");

        // Fabric利用可否に応じて動的パラメータを設定
        var overrides = new Dictionary<string, string>();

        // リージョンとサフィックスを動的に設定
        overrides["location"] = azure.Region;
        overrides["suffix"] = suffix;

        // デプロイ実行ユーザーの Object ID (RBAC割当用)
        try
        {
            var userOid = (await _az.RunAsync("ad signed-in-user show --query id -o tsv", silent: true)).Trim();
            if (!string.IsNullOrEmpty(userOid))
                overrides["deployerObjectId"] = userOid;
        }
        catch { /* 取得失敗時は空で進行 */ }

        // Functions デプロイ (サブスクリプションのクォータ制限で失敗する場合 false)
        overrides["deployFunctions"] = azure.FunctionsAvailable ? "true" : "false";

        if (azure.FabricAvailable)
        {
            overrides["deployFabric"] = "true";
            if (!string.IsNullOrEmpty(azure.UserUpn))
                overrides["fabricAdminMembers"] = $"['{azure.UserUpn}']";
        }
        else
        {
            overrides["deployFabric"] = "false";
        }

        try
        {
            var output = await _az.DeployAsync(rgName, templatePath, File.Exists(paramPath) ? paramPath : null, overrides);
            await ParseDeploymentOutputAsync(state, output, azure);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("InternalSubscriptionIsOverQuotaForSku") &&
            ex.Message.Contains("serverFarms") &&
            overrides.GetValueOrDefault("deployFunctions") != "false")
        {
            // Functions クォータ不足 → Functions 無しでリトライ (Functions は補助機能)
            Console.WriteLine("\n  ⚠️ Functions クォータ不足を検出。Functions 無しで再デプロイします...\n");
            overrides["deployFunctions"] = "false";
            azure.FunctionsAvailable = false;
            var output = await _az.DeployAsync(rgName, templatePath, File.Exists(paramPath) ? paramPath : null, overrides);
            await ParseDeploymentOutputAsync(state, output, azure);
        }

    }

    private Task ParseDeploymentOutputAsync(SetupState state, string output, AzureConfig azure)
    {
        try
        {
            var deployResult = JsonDocument.Parse(output);
            var outputs = deployResult.RootElement.GetProperty("properties").GetProperty("outputs");

            state.Deployment = new DeploymentResult
            {
                StorageAccountSkills = GetOutputValue(outputs, "skillsStorageName"),
                StorageAccountPortal = GetOutputValue(outputs, "portalStorageName"),
                FoundryEndpoint = GetOutputValue(outputs, "aiServicesEndpoint"),
                FoundryProjectEndpoint = GetOutputValue(outputs, "aiFoundryProjectEndpoint"),
                ContainerAppUrl = GetOutputValue(outputs, "containerAppUrl"),
                AcrLoginServer = GetOutputValue(outputs, "acrLoginServer"),
                FunctionAppName = GetOutputValue(outputs, "functionAppName"),
                KeyVaultUri = GetOutputValue(outputs, "keyVaultUri"),
                AppInsightsConnectionString = GetOutputValue(outputs, "appInsightsConnectionString"),
                PortalBaseUrl = GetOutputValue(outputs, "portalStaticWebEndpoint"),
            };

            // Container App 名を FQDN から抽出
            var fqdn = GetOutputValue(outputs, "containerAppFqdn");
            if (!string.IsNullOrEmpty(fqdn))
                state.Deployment.ContainerAppNameAgent = fqdn.Split('.')[0];
            else
            {
                var caName = GetOutputValue(outputs, "containerAppName");
                if (!string.IsNullOrEmpty(caName))
                    state.Deployment.ContainerAppNameAgent = caName;
            }

            Console.WriteLine("  ✓ デプロイ完了");
            Console.WriteLine($"    Container App: {state.Deployment.ContainerAppUrl}");
            Console.WriteLine($"    Foundry: {state.Deployment.FoundryEndpoint}");
            Console.WriteLine($"    Key Vault: {state.Deployment.KeyVaultUri}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️ デプロイ出力のパースに失敗: {ex.Message}");
            Console.WriteLine("  デプロイ自体は成功している可能性があります。状態を手動で確認してください。");
            state.Deployment = new DeploymentResult();
        }
        return Task.CompletedTask;
    }

    private static string GetOutputValue(JsonElement outputs, string key)
    {
        if (outputs.TryGetProperty(key, out var prop) && prop.TryGetProperty("value", out var val))
            return val.GetString() ?? "";
        return "";
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        // フォールバック: CWD から探す
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
