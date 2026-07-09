using System.Text.Json;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ13: Entra ID アプリ作成 - Teams投稿用のアプリ登録
/// </summary>
public class Step14EntraId : ISetupStep
{
    private readonly AzureCliWrapper _az;

    public int StepNumber => 14;
    public string Name => "Entra ID アプリ作成";

    public Step14EntraId(AzureCliWrapper az)
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

        // Public Client (Device Code Flow に必要) を有効化
        await _az.RunAsync(
            $"ad app update --id {appId} --is-fallback-public-client true",
            silent: true);
        Console.WriteLine("    ✓ Public Client Flow 有効化");

        // 2. API権限追加 (Microsoft Graph delegated permissions)
        // User.Read: e1fe6dd8-ba31-4d61-89e7-88639da4683d
        // ChannelMessage.Send: ebf0f66e-9fb1-49e4-a278-222f76911cf4
        // Team.ReadBasic.All: 660b7406-55f1-41ca-a0ed-0b035e182f3e
        // offline_access: 7427e0e9-2fba-42fe-b0c0-848c9e6a8182
        Console.WriteLine("  🔐 API権限を追加中 (管理者同意不要スコープ)...");

        var graphResourceId = "00000003-0000-0000-c000-000000000000"; // Microsoft Graph

        await _az.RunAsync(
            $"ad app permission add --id {appId} " +
            $"--api {graphResourceId} " +
            $"--api-permissions " +
            $"e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope " + // User.Read
            $"ebf0f66e-9fb1-49e4-a278-222f76911cf4=Scope " + // ChannelMessage.Send
            $"660b7406-55f1-41ca-a0ed-0b035e182f3e=Scope " + // Team.ReadBasic.All
            $"7427e0e9-2fba-42fe-b0c0-848c9e6a8182=Scope",   // offline_access
            silent: true);

        Console.WriteLine("    ✓ User.Read (delegated)");
        Console.WriteLine("    ✓ ChannelMessage.Send (delegated)");
        Console.WriteLine("    ✓ offline_access (delegated)");
        Console.WriteLine("    ✓ Team.ReadBasic.All (delegated)");

        // 3. Client Secret 生成 (1年)
        Console.WriteLine("  🔑 Client Secret を生成中 (有効期限: 1年)...");
        var credResult = await _az.RunAsync(
            $"ad app credential reset --id {appId} " +
            $"--years 1 " +
            $"--output json",
            silent: true);

        var credDoc = JsonDocument.Parse(credResult);
        var clientSecret = credDoc.RootElement.GetProperty("password").GetString() ?? "";

        // 4. Key Vault にシークレット保存
        if (!string.IsNullOrEmpty(deployment.KeyVaultUri))
        {
            var vaultName = new Uri(deployment.KeyVaultUri).Host.Split('.')[0];
            Console.WriteLine($"  🔒 Key Vault ({vaultName}) にシークレットを保存中...");

            // publicNetworkAccess を確認
            var publicAccess = "Enabled";
            try
            {
                var kvJson = await _az.RunAsync(
                    $"keyvault show --name {vaultName} --query properties.publicNetworkAccess -o tsv",
                    silent: true);
                publicAccess = kvJson.Trim();
            }
            catch { }

            var kvSuccess = false;

            if (publicAccess.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("    ⚠️ publicNetworkAccess: Disabled (Azure Policy による制限)");
                Console.WriteLine("    → 外部からの KV アクセス不可。手動登録情報を表示します。");
            }
            else
            {
                try
                {
                    // RBAC ロール割り当て (Key Vault Secrets Officer)
                    try
                    {
                        var userOid = await _az.RunAsync("ad signed-in-user show --query id -o tsv", silent: true);
                        await _az.RunAsync(
                            $"role assignment create --role \"Key Vault Secrets Officer\" " +
                            $"--assignee {userOid.Trim()} " +
                            $"--scope /subscriptions/{azure.SubscriptionId}/resourceGroups/{azure.ResourceGroup}/providers/Microsoft.KeyVault/vaults/{vaultName}",
                            silent: true);
                        await Task.Delay(10000);
                    }
                    catch { }

                    // Client Secret は特殊文字を含むため一時ファイル経由で設定
                    var secretFilePath = Path.GetFullPath("./output/tmp_secret.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(secretFilePath)!);
                    await File.WriteAllTextAsync(secretFilePath, clientSecret);

                    await _az.RunAsync(
                        $"keyvault secret set --vault-name {vaultName} " +
                        $"--name teams-app-client-secret " +
                        $"--file \"{secretFilePath}\" --encoding utf-8",
                        silent: true);

                    await File.WriteAllTextAsync(secretFilePath, appId);
                    await _az.RunAsync(
                        $"keyvault secret set --vault-name {vaultName} " +
                        $"--name teams-app-client-id " +
                        $"--file \"{secretFilePath}\" --encoding utf-8",
                        silent: true);

                    File.Delete(secretFilePath);
                    Console.WriteLine("    ✓ teams-app-client-secret");
                    Console.WriteLine("    ✓ teams-app-client-id");
                    kvSuccess = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ⚠️ Key Vault 書き込み失敗: {ex.Message[..Math.Min(150, ex.Message.Length)]}");
                }
            }

            if (!kvSuccess)
            {
                Console.WriteLine();
                Console.WriteLine("    ╔══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("    ║  📋 Key Vault 手動登録が必要です                            ║");
                Console.WriteLine("    ╠══════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"    ║  Vault: {vaultName,-52}║");
                Console.WriteLine("    ╠══════════════════════════════════════════════════════════════╣");
                Console.WriteLine("    ║  以下を Azure Portal > Key Vault > Secrets で登録:         ║");
                Console.WriteLine("    ╠══════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"    ║  Name:  teams-app-client-id                                ║");
                Console.WriteLine($"    ║  Value: {appId,-52}║");
                Console.WriteLine("    ╠══════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"    ║  Name:  teams-app-client-secret                            ║");
                Console.WriteLine($"    ║  Value: {clientSecret,-52}║");
                Console.WriteLine("    ╚══════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
            }
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
            var agentAppName = deployment.ContainerAppNameAgent;
            Console.WriteLine($"  📦 Container App ({agentAppName}) の環境変数を更新中...");
            try
            {
                await _az.RunAsync(
                    $"containerapp update --name {agentAppName} " +
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

        // 6. Device Code Flow でユーザーの Refresh Token を取得
        Console.WriteLine("\n  🔑 Device Code Flow でユーザー認証を実行中...");
        Console.WriteLine("    (Teams投稿用の委任トークンを取得します)\n");

        var refreshToken = await AcquireRefreshTokenViaDeviceCodeAsync(appId, azure.TenantId, ct);

        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Key Vault に保存
            if (!string.IsNullOrEmpty(deployment.KeyVaultUri))
            {
                var vaultName2 = new Uri(deployment.KeyVaultUri).Host.Split('.')[0];
                await _az.RunAsync(
                    $"keyvault secret set --vault-name {vaultName2} " +
                    $"--name teams-graph-refresh-token " +
                    $"--value \"{refreshToken}\"",
                    silent: true);
                Console.WriteLine("    ✓ teams-graph-refresh-token → Key Vault 保存完了");
            }

            // Container App 環境変数に追加
            if (!string.IsNullOrEmpty(azure.ResourceGroup))
            {
                try
                {
                    await _az.RunAsync(
                        $"containerapp update --name {deployment.ContainerAppNameAgent} " +
                        $"--resource-group {azure.ResourceGroup} " +
                        $"--set-env-vars " +
                        $"\"Teams__Graph__RefreshToken={refreshToken}\"",
                        silent: true);
                    Console.WriteLine("    ✓ Container App 環境変数更新完了");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ⚠️ 環境変数更新に失敗: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("    ⚠️ Refresh Token の取得に失敗しました。後で手動設定してください。");
        }

        // 状態保存
        deployment.EntraAppId = appId;
        deployment.EntraAppTenantId = azure.TenantId;

        Console.WriteLine($"\n  ✓ Entra ID アプリ作成完了");
        Console.WriteLine($"    App Name: {appName}");
        Console.WriteLine($"    App ID: {appId}");
        Console.WriteLine($"    権限: User.Read, ChannelMessage.Send, Team.ReadBasic.All (delegated, 管理者同意不要)");
    }

    /// <summary>
    /// Device Code Flow でユーザーを認証し、Refresh Token を取得する
    /// </summary>
    private static async Task<string> AcquireRefreshTokenViaDeviceCodeAsync(
        string clientId, string tenantId, CancellationToken ct)
    {
        using var http = new HttpClient();
        var scopes = "https://graph.microsoft.com/ChannelMessage.Send https://graph.microsoft.com/Team.ReadBasic.All offline_access";

        // 1. Device Code リクエスト
        var deviceCodeResponse = await http.PostAsync(
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/devicecode",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scopes
            }),
            ct);

        var deviceCodeBody = await deviceCodeResponse.Content.ReadAsStringAsync(ct);
        if (!deviceCodeResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"    ❌ Device Code 取得失敗: {deviceCodeBody}");
            return "";
        }

        using var dcDoc = JsonDocument.Parse(deviceCodeBody);
        var userCode = dcDoc.RootElement.GetProperty("user_code").GetString() ?? "";
        var deviceCode = dcDoc.RootElement.GetProperty("device_code").GetString() ?? "";
        var verificationUri = dcDoc.RootElement.GetProperty("verification_uri").GetString() ?? "";
        var interval = dcDoc.RootElement.TryGetProperty("interval", out var intProp) ? intProp.GetInt32() : 5;
        var expiresIn = dcDoc.RootElement.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 900;

        Console.WriteLine($"    ┌──────────────────────────────────────────────────┐");
        Console.WriteLine($"    │  以下のURLにアクセスしてコードを入力してください │");
        Console.WriteLine($"    │  URL:  {verificationUri,-40} │");
        Console.WriteLine($"    │  Code: {userCode,-40} │");
        Console.WriteLine($"    └──────────────────────────────────────────────────┘");
        Console.WriteLine($"    ⏳ 認証待ち中... (最大 {expiresIn / 60} 分)");

        // 2. ポーリングでトークン取得
        var deadline = DateTime.UtcNow.AddSeconds(expiresIn);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);

            var tokenResponse = await http.PostAsync(
                $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode
                }),
                ct);

            var tokenBody = await tokenResponse.Content.ReadAsStringAsync(ct);
            using var tokenDoc = JsonDocument.Parse(tokenBody);

            if (tokenResponse.IsSuccessStatusCode)
            {
                var rt = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rtProp)
                    ? rtProp.GetString() ?? ""
                    : "";
                if (!string.IsNullOrEmpty(rt))
                {
                    Console.WriteLine("    ✅ 認証成功！Refresh Token を取得しました。");
                    return rt;
                }
                Console.WriteLine("    ⚠️ Access Token は取得できましたが、Refresh Token がありません（offline_access スコープを確認）。");
                return "";
            }

            var error = tokenDoc.RootElement.TryGetProperty("error", out var errProp)
                ? errProp.GetString() ?? ""
                : "";

            if (error == "authorization_pending")
                continue;

            if (error == "slow_down")
            {
                interval += 5;
                continue;
            }

            // expired_token, access_denied, etc.
            Console.WriteLine($"    ❌ 認証失敗: {error}");
            return "";
        }

        Console.WriteLine("    ❌ タイムアウト: 認証が完了しませんでした。");
        return "";
    }
}
