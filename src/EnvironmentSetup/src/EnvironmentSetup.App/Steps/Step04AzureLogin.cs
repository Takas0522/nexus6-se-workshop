using System.Text.Json;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ4: Azure ログイン
/// </summary>
public class Step04AzureLogin : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 4;
    public string Name => "Azure ログイン";

    public Step04AzureLogin(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        Console.WriteLine("  Azure ログイン状態を確認中...\n");

        if (await _az.CheckLoginAsync())
        {
            var accountJson = await _az.RunAsync("account show --output json", silent: true);
            var account = JsonDocument.Parse(accountJson);
            var subId = account.RootElement.GetProperty("id").GetString() ?? "";
            var subName = account.RootElement.GetProperty("name").GetString() ?? "";
            var tenantId = account.RootElement.GetProperty("tenantId").GetString() ?? "";

            Console.WriteLine($"  ✓ ログイン済み");
            Console.WriteLine($"    サブスクリプション: {subName} ({subId})");
            Console.WriteLine($"    テナント: {tenantId}\n");

            Console.Write("  このサブスクリプションで続行しますか? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm != "y" && confirm != "yes")
            {
                Console.WriteLine("  再ログインします...\n");
                await _az.LoginAsync();
                accountJson = await _az.RunAsync("account show --output json", silent: true);
                account = JsonDocument.Parse(accountJson);
                subId = account.RootElement.GetProperty("id").GetString() ?? "";
                tenantId = account.RootElement.GetProperty("tenantId").GetString() ?? "";
            }

            state.Azure = new AzureConfig
            {
                SubscriptionId = subId,
                TenantId = tenantId,
                Region = "swedencentral"
            };
        }
        else
        {
            Console.WriteLine("  ログインセッションが無効です。デバイスコードフローでログインします。\n");
            await _az.LoginAsync();

            var accountJson = await _az.RunAsync("account show --output json", silent: true);
            var account = JsonDocument.Parse(accountJson);
            state.Azure = new AzureConfig
            {
                SubscriptionId = account.RootElement.GetProperty("id").GetString() ?? "",
                TenantId = account.RootElement.GetProperty("tenantId").GetString() ?? "",
                Region = "swedencentral"
            };
        }

        Console.WriteLine($"\n  ✓ Azure 構成を保存しました (Region: {state.Azure.Region})");
    }
}
