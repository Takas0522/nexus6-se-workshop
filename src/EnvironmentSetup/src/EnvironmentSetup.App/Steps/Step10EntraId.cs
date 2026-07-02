using System.Text.Json;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ10: Entra ID アプリ作成 - Teams投稿用のアプリ登録
/// </summary>
public class Step10EntraId : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 10;
    public string Name => "Entra ID アプリ作成";

    public Step10EntraId(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure
            ?? throw new InvalidOperationException("Azure 構成が未完了です。Step 4 を先に実行してください。");
        var deployment = state.Deployment
            ?? throw new InvalidOperationException("デプロイが未完了です。Step 5 を先に実行してください。");

        Console.WriteLine("  Entra ID アプリケーションを作成中...\n");

        var appName = "nexus6-teams-notification";

        // 1. アプリ登録
        Console.WriteLine($"  📋 アプリ登録: {appName}");
        var createResult = await _az.RunAsync(
            $"ad app create --display-name {appName} " +
            $"--sign-in-audience AzureADMyOrg " +
            $"--output json",
            silent: true);

        var appDoc = JsonDocument.Parse(createResult);
        var appId = appDoc.RootElement.GetProperty("appId").GetString()
            ?? throw new InvalidOperationException("アプリID の取得に失敗しました");
        var objectId = appDoc.RootElement.GetProperty("id").GetString() ?? "";

        Console.WriteLine($"    App ID: {appId}");

        // 2. API権限追加 (Microsoft Graph delegated permissions)
        // User.Read: e1fe6dd8-ba31-4d61-89e7-88639da4683d
        // ChannelMessage.Send: ebf0f66e-9fb1-49e4-a278-222f76911cf4
        // Team.ReadBasic.All: 660b7406-55f1-41ca-a0ed-0b035e182f3e
        Console.WriteLine("  🔐 API権限を追加中 (管理者同意不要スコープ)...");

        var graphResourceId = "00000003-0000-0000-c000-000000000000"; // Microsoft Graph

        await _az.RunAsync(
            $"ad app permission add --id {appId} " +
            $"--api {graphResourceId} " +
            $"--api-permissions " +
            $"e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope " + // User.Read
            $"ebf0f66e-9fb1-49e4-a278-222f76911cf4=Scope " + // ChannelMessage.Send
            $"660b7406-55f1-41ca-a0ed-0b035e182f3e=Scope",   // Team.ReadBasic.All
            silent: true);

        Console.WriteLine("    ✓ User.Read (delegated)");
        Console.WriteLine("    ✓ ChannelMessage.Send (delegated)");
        Console.WriteLine("    ✓ Team.ReadBasic.All (delegated)");

        // 3. Client Secret 生成 (6ヶ月)
        Console.WriteLine("  🔑 Client Secret を生成中 (有効期限: 6ヶ月)...");
        var credResult = await _az.RunAsync(
            $"ad app credential reset --id {appId} " +
            $"--years 0.5 " +
            $"--output json",
            silent: true);

        var credDoc = JsonDocument.Parse(credResult);
        var clientSecret = credDoc.RootElement.GetProperty("password").GetString() ?? "";

        // 4. Key Vault にシークレット保存
        if (!string.IsNullOrEmpty(deployment.KeyVaultUri))
        {
            var vaultName = new Uri(deployment.KeyVaultUri).Host.Split('.')[0];
            Console.WriteLine($"  🔒 Key Vault ({vaultName}) にシークレットを保存中...");

            await _az.RunAsync(
                $"keyvault secret set --vault-name {vaultName} " +
                $"--name teams-app-client-secret " +
                $"--value {clientSecret}",
                silent: true);

            await _az.RunAsync(
                $"keyvault secret set --vault-name {vaultName} " +
                $"--name teams-app-client-id " +
                $"--value {appId}",
                silent: true);

            Console.WriteLine("    ✓ teams-app-client-secret");
            Console.WriteLine("    ✓ teams-app-client-id");
        }
        else
        {
            Console.WriteLine("  ⚠️ Key Vault が未設定のため、シークレットを手動で保存してください。");
            Console.WriteLine($"    Client ID: {appId}");
            Console.WriteLine($"    Client Secret: {clientSecret[..8]}...(ログに全文は表示しません)");
        }

        // 5. Container App の環境変数更新
        if (!string.IsNullOrEmpty(azure.ResourceGroup))
        {
            Console.WriteLine("  📦 Container App の環境変数を更新中...");
            try
            {
                await _az.RunAsync(
                    $"containerapp update --name ca-nexus6-hosted-agent " +
                    $"--resource-group {azure.ResourceGroup} " +
                    $"--set-env-vars " +
                    $"Teams__Graph__ClientId={appId} " +
                    $"Teams__Graph__TenantId={azure.TenantId}",
                    silent: true);
                Console.WriteLine("    ✓ 環境変数更新完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ 環境変数更新に失敗: {ex.Message}");
            }
        }

        // 状態保存
        deployment.EntraAppId = appId;
        deployment.EntraAppTenantId = azure.TenantId;

        Console.WriteLine($"\n  ✓ Entra ID アプリ作成完了");
        Console.WriteLine($"    App Name: {appName}");
        Console.WriteLine($"    App ID: {appId}");
        Console.WriteLine($"    権限: User.Read, ChannelMessage.Send, Team.ReadBasic.All (delegated, 管理者同意不要)");
    }
}
