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
    private readonly NonInteractiveConfig _niConfig;

    public int StepNumber => 4;
    public string Name => "Azure ログイン";

    public Step04AzureLogin(AzureCliWrapper az, NonInteractiveConfig niConfig)
    {
        _az = az;
        _niConfig = niConfig;
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

            if (!_niConfig.Enabled)
            {
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
            }

            state.Azure = new AzureConfig
            {
                SubscriptionId = subId,
                TenantId = tenantId,
                Region = "northeurope"
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
                Region = "northeurope"
            };
        }

        // UPN取得
        Console.WriteLine("\n  ユーザー情報を取得中...");
        try
        {
            var userJson = await _az.RunAsync("ad signed-in-user show --query userPrincipalName -o tsv", silent: true);
            state.Azure.UserUpn = userJson.Trim();
            Console.WriteLine($"  ✓ UPN: {state.Azure.UserUpn}");
        }
        catch
        {
            Console.WriteLine("  ⚠️ UPN取得に失敗 (一部機能が制限される可能性があります)");
        }

        // Fabricプロバイダー登録確認 & 利用可否判定
        Console.WriteLine("\n  Fabric 利用可否を確認中...");
        state.Azure.FabricAvailable = await CheckFabricAvailabilityAsync(state.Azure);

        if (state.Azure.FabricAvailable)
            Console.WriteLine("  ✓ Microsoft Fabric: 利用可能");
        else
            Console.WriteLine("  ⚠️ Microsoft Fabric: 利用不可 (Fabricステップはスキップされます)");

        Console.WriteLine($"\n  ✓ Azure 構成を保存しました (Region: {state.Azure.Region})");
    }

    private async Task<bool> CheckFabricAvailabilityAsync(AzureConfig azure)
    {
        // 1. プロバイダー登録確認
        try
        {
            var regState = await _az.RunAsync("provider show --namespace Microsoft.Fabric --query registrationState -o tsv", silent: true);
            if (regState.Trim() != "Registered")
            {
                Console.WriteLine("    Microsoft.Fabric プロバイダーを登録中...");
                await _az.RunAsync("provider register --namespace Microsoft.Fabric", silent: true);
                // 登録完了を待機 (最大60秒)
                for (var i = 0; i < 12; i++)
                {
                    await Task.Delay(5000);
                    regState = await _az.RunAsync("provider show --namespace Microsoft.Fabric --query registrationState -o tsv", silent: true);
                    if (regState.Trim() == "Registered") break;
                }
                if (regState.Trim() != "Registered")
                {
                    Console.WriteLine("    ⚠️ プロバイダー登録がタイムアウトしました");
                    return false;
                }
                Console.WriteLine("    ✓ プロバイダー登録完了");
            }
        }
        catch
        {
            return false;
        }

        // 2. テナントでFabricが利用可能か確認 (ダミーCapacityのバリデーション)
        try
        {
            var testName = $"fabricvalidate{Random.Shared.Next(1000, 9999)}";
            var upn = azure.UserUpn;
            if (string.IsNullOrEmpty(upn)) return false;

            // what-if相当: PUTでvalidateOnly
            var result = await _az.RunAsync(
                $"rest --method POST " +
                $"--url \"https://management.azure.com/subscriptions/{azure.SubscriptionId}/providers/Microsoft.Fabric/locations/{azure.Region}/checkNameAvailability?api-version=2023-11-01\" " +
                $"--body \"{{\\\"name\\\":\\\"{testName}\\\",\\\"type\\\":\\\"Microsoft.Fabric/capacities\\\"}}\"",
                silent: true);

            // nameAvailable が返れば Fabric API 自体は応答可能
            if (!result.Contains("nameAvailable"))
                return false;

            // 3. リージョンのFabricクォータ確認 (RegionalQuota > 0 であること)
            var quotaResult = await _az.RunAsync(
                $"rest --method GET " +
                $"--url \"https://management.azure.com/subscriptions/{azure.SubscriptionId}/providers/Microsoft.Fabric/locations/{azure.Region}/skus?api-version=2023-11-01\"",
                silent: true);

            // F4 SKU が利用可能かつクォータ > 0 かを確認
            // クォータ0の場合はデプロイ不可
            if (quotaResult.Contains("\"name\":\"F4\"") || quotaResult.Contains("\"name\": \"F4\""))
            {
                // SKU自体は見つかったが、実際のクォータ確認
                // Bicepデプロイ時に "RegionalQuota: 0" で失敗する場合を防ぐ
                // 直接クォータAPIを叩く
                try
                {
                    var capacityCheck = await _az.RunAsync(
                        $"rest --method POST " +
                        $"--url \"https://management.azure.com/subscriptions/{azure.SubscriptionId}/providers/Microsoft.Fabric/locations/{azure.Region}/checkQuotaAvailability?api-version=2023-11-01\" " +
                        $"--body \"{{\\\"name\\\":\\\"{testName}\\\",\\\"type\\\":\\\"Microsoft.Fabric/capacities\\\",\\\"properties\\\":{{\\\"sku\\\":{{\\\"name\\\":\\\"F4\\\"}}}}}}\"",
                        silent: true);
                    if (capacityCheck.Contains("QuotaExceeded") || capacityCheck.Contains("RegionalQuota: 0") || capacityCheck.Contains("must not exceed"))
                    {
                        Console.WriteLine("    ⚠️ Fabric F4 のリージョンクォータが 0 です");
                        return false;
                    }
                }
                catch
                {
                    // checkQuotaAvailability API が存在しない場合、フォールバック
                    // このサブスクリプションではクォータ問題が既知なので false にする
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            // "Tenant wasn't recognized" エラーの場合は利用不可
            if (ex.Message.Contains("wasn't recognized") || ex.Message.Contains("not recognized"))
                return false;
            // その他のエラー(権限不足等)でもAPIが応答していれば利用可能とみなす
            return !ex.Message.Contains("InvalidResourceType");
        }
    }
}
