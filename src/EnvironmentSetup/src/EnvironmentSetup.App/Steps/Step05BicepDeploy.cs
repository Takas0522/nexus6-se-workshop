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

    public int StepNumber => 5;
    public string Name => "Bicep デプロイ";

    public Step05BicepDeploy(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure
            ?? throw new InvalidOperationException("Azure ログインが未完了です。Step 4 を先に実行してください。");

        // リソースグループ名の決定
        var suffix = Random.Shared.Next(1000, 9999).ToString();
        var defaultRgName = $"rg-nexus6-{suffix}";

        Console.Write($"  リソースグループ名 [{defaultRgName}]: ");
        var rgName = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(rgName))
            rgName = defaultRgName;

        azure.ResourceGroup = rgName;

        Console.WriteLine($"\n  リソースグループ: {rgName}");
        Console.WriteLine($"  リージョン: {azure.Region}");
        Console.WriteLine($"  テンプレート: infra/main.bicep\n");

        // リソースグループ作成
        Console.WriteLine("  リソースグループを作成中...");
        await _az.EnsureResourceGroupAsync(rgName, azure.Region);

        // パラメータファイル生成
        var templatePath = Path.GetFullPath("infra/main.bicep");
        var paramPath = Path.GetFullPath("infra/main.bicepparam");

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Bicep テンプレートが見つかりません: {templatePath}");

        // What-if 実行
        Console.WriteLine("\n  what-if を実行中...\n");
        await _az.WhatIfAsync(rgName, templatePath, File.Exists(paramPath) ? paramPath : null);

        Console.Write("\n  デプロイを実行しますか? (y/n): ");
        var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (confirm != "y" && confirm != "yes")
            throw new OperationCanceledException("ユーザーによりデプロイがキャンセルされました。");

        // デプロイ実行
        Console.WriteLine("\n  デプロイ実行中 (数分かかる場合があります)...\n");
        var output = await _az.DeployAsync(rgName, templatePath, File.Exists(paramPath) ? paramPath : null);

        // 出力パース
        try
        {
            var deployResult = JsonDocument.Parse(output);
            var outputs = deployResult.RootElement.GetProperty("properties").GetProperty("outputs");

            state.Deployment = new DeploymentResult
            {
                StorageAccountSkills = GetOutputValue(outputs, "skillsStorageName"),
                StorageAccountPortal = GetOutputValue(outputs, "portalStorageName"),
                FoundryEndpoint = GetOutputValue(outputs, "foundryEndpoint"),
                ContainerAppUrl = GetOutputValue(outputs, "containerAppUrl"),
                AcrLoginServer = GetOutputValue(outputs, "acrLoginServer"),
                FunctionAppName = GetOutputValue(outputs, "functionAppName"),
                KeyVaultUri = GetOutputValue(outputs, "keyVaultUri"),
                AppInsightsConnectionString = GetOutputValue(outputs, "appInsightsConnectionString"),
                PortalBaseUrl = GetOutputValue(outputs, "portalStaticWebEndpoint"),
            };

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
    }

    private static string GetOutputValue(JsonElement outputs, string key)
    {
        if (outputs.TryGetProperty(key, out var prop) && prop.TryGetProperty("value", out var val))
            return val.GetString() ?? "";
        return "";
    }
}
