using ConsoleAppFramework;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;
using EnvironmentSetup.App.Steps;

ConsoleApp.Run(args, async (
    int step = 1,
    string stateFile = "./setup-state.json",
    string logDir = "./logs",
    bool useDefaults = false,
    bool verbose = false,
    bool nonInteractive = false,
    bool cleanup = false,
    string domains = "",
    int employees = 0,
    string notificationTeam = "",
    string notificationChannels = "",
    string resourceGroup = "",
    string webiqApiKey = "") =>
{
    Console.WriteLine("╔══════════════════════════════════════════════╗");
    Console.WriteLine("║   EnvironmentSetup CLI - 環境構築ツール     ║");
    Console.WriteLine("╚══════════════════════════════════════════════╝");
    Console.WriteLine();

    if (verbose)
        Console.WriteLine("  🔍 Verbose モード: ON");
    if (nonInteractive)
        Console.WriteLine("  🤖 非対話モード: ON");
    Console.WriteLine();

    Directory.CreateDirectory(logDir);

    var stateManager = new StateManager(stateFile);
    var state = await stateManager.LoadAsync();

    // --cleanup: setup-state.json の情報を元にリソースを全削除
    if (cleanup)
    {
        await CleanupResources(state, new AzureCliWrapper(verbose, logDir), stateFile);
        return;
    }

    var niConfig = new NonInteractiveConfig
    {
        Enabled = nonInteractive,
        Domains = string.IsNullOrEmpty(domains) ? [] : domains.Split(',', StringSplitOptions.TrimEntries),
        Employees = employees,
        NotificationTeam = notificationTeam,
        NotificationChannels = string.IsNullOrEmpty(notificationChannels) ? [] : notificationChannels.Split(',', StringSplitOptions.TrimEntries),
        ResourceGroup = resourceGroup,
        WebIqApiKey = webiqApiKey,
    };

    await using var copilotService = new CopilotService();
    var azureCli = new AzureCliWrapper(verbose, logDir);

    var steps = new ISetupStep[]
    {
        new Step01Assessment(useDefaults || nonInteractive, niConfig),
        new Step02Analysis(copilotService),
        new Step03Report(copilotService, niConfig),
        new Step04AzureLogin(azureCli, niConfig),
        new Step05BicepDeploy(azureCli, niConfig),
        new Step06DataCreation(copilotService),
        new Step07MedallionSetup(azureCli),
        new Step08OntologyCreation(azureCli),
        new Step09DomainConfigUpload(),
        new Step10NewsSite(copilotService),
        new Step11SkillDsMd(copilotService),
        new Step12FoundryKnowledge(azureCli),
        new Step13AppDeploy(azureCli, niConfig),
        new Step14EntraId(azureCli),
    };

    var runner = new StepRunner(steps, stateManager, verbose, nonInteractive);
    await runner.ExecuteFromAsync(state, step);

    // 最終サマリー: 構築されたリソースのURL一覧
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║        ✅ 環境構築が完了しました！                      ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("  📋 リソース URL 一覧:");
    Console.WriteLine("  ─────────────────────────────────────────────────────────");

    var dep = state.Deployment;
    var az = state.Azure;

    if (dep != null)
    {
        if (!string.IsNullOrEmpty(dep.ContainerAppUrl))
            Console.WriteLine($"  🌐 WebApp:           {dep.ContainerAppUrl}");

        if (!string.IsNullOrEmpty(az?.ResourceGroup) && !string.IsNullOrEmpty(dep.ContainerAppNameWebapp))
            Console.WriteLine($"  🌐 WebApp (Azure):   https://portal.azure.com/#@/resource/subscriptions/{az!.SubscriptionId}/resourceGroups/{az.ResourceGroup}/providers/Microsoft.App/containerApps/{dep.ContainerAppNameWebapp}");

        if (!string.IsNullOrEmpty(dep.FoundryEndpoint))
            Console.WriteLine($"  🤖 AI Foundry:       {dep.FoundryEndpoint}");

        if (!string.IsNullOrEmpty(dep.FoundryProjectEndpoint))
            Console.WriteLine($"  🤖 Foundry Project:  {dep.FoundryProjectEndpoint}");

        if (!string.IsNullOrEmpty(dep.FabricSqlEndpoint))
            Console.WriteLine($"  🗄️  Fabric SQL:      {dep.FabricSqlEndpoint}");

        if (!string.IsNullOrEmpty(dep.KeyVaultUri))
            Console.WriteLine($"  🔒 Key Vault:        {dep.KeyVaultUri}");

        if (!string.IsNullOrEmpty(dep.PortalBaseUrl))
            Console.WriteLine($"  📊 Portal:           {dep.PortalBaseUrl}");

        if (!string.IsNullOrEmpty(dep.WebIqBaseUrl))
            Console.WriteLine($"  🔍 WebIQ:            {dep.WebIqBaseUrl}");

        if (!string.IsNullOrEmpty(dep.FunctionAppName))
            Console.WriteLine($"  ⚡ Functions:         https://{dep.FunctionAppName}.azurewebsites.net");

        if (!string.IsNullOrEmpty(dep.EntraAppId))
            Console.WriteLine($"  🔑 Entra App ID:     {dep.EntraAppId}");
    }

    if (az != null)
    {
        Console.WriteLine($"  ☁️  Resource Group:   https://portal.azure.com/#@/resource/subscriptions/{az.SubscriptionId}/resourceGroups/{az.ResourceGroup}/overview");

        // Fabric workspace URL
        Console.WriteLine($"  🏭 Fabric:           https://app.fabric.microsoft.com/");
    }

    Console.WriteLine("  ─────────────────────────────────────────────────────────");
    Console.WriteLine();

    // 手動対応が必要な項目を表示
    if (state.ManualActions.Count > 0)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  ⚠️  手動対応が必要な項目があります                     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        foreach (var action in state.ManualActions)
        {
            Console.WriteLine($"  📌 [Step{action.Step}] {action.Target}");
            Console.WriteLine($"     {action.Description}");
            foreach (var detail in action.Details)
            {
                Console.WriteLine($"     {detail}");
            }
            Console.WriteLine();
        }
        Console.WriteLine("  ─────────────────────────────────────────────────────────");
        Console.WriteLine();
    }
});

static async Task CleanupResources(SetupState state, AzureCliWrapper az, string stateFile)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  🗑️  環境クリーンアップ                                 ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    var deleted = new List<string>();

    // 1. Azure Resource Group
    var rg = state.Azure?.ResourceGroup;
    if (!string.IsNullOrWhiteSpace(rg))
    {
        Console.Write($"  🗑️ Resource Group '{rg}' を削除中...");
        try
        {
            await az.RunAsync($"group delete --name {rg} --yes --no-wait", silent: true);
            Console.WriteLine(" ✓ (非同期削除開始)");
            deleted.Add($"RG: {rg}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($" ⚠️ {ex.Message[..Math.Min(80, ex.Message.Length)]}");
        }
    }
    else
    {
        Console.WriteLine("  ℹ️ Resource Group: 未設定 (スキップ)");
    }

    // 2. Fabric Workspace
    var wsId = state.Medallion?.WorkspaceId;
    if (string.IsNullOrWhiteSpace(wsId))
    {
        // state に未保存の場合、Fabric API で検索
        try
        {
            Console.Write("  🔍 Fabric Workspace を検索中...");
            var wsJson = await az.RunAsync(
                "rest --method get --url \"https://api.fabric.microsoft.com/v1/workspaces\" " +
                "--resource \"https://api.fabric.microsoft.com\"",
                silent: true);
            var wsDoc = System.Text.Json.JsonDocument.Parse(wsJson);
            foreach (var ws in wsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                var name = ws.GetProperty("displayName").GetString() ?? "";
                if (name.StartsWith("ws-nexus6", StringComparison.OrdinalIgnoreCase))
                {
                    wsId = ws.GetProperty("id").GetString();
                    Console.WriteLine($" 発見: {name} ({wsId})");
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(wsId))
                Console.WriteLine(" 未検出 (スキップ)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($" ⚠️ 検索失敗: {ex.Message[..Math.Min(60, ex.Message.Length)]}");
        }
    }
    if (!string.IsNullOrWhiteSpace(wsId))
    {
        Console.Write($"  🗑️ Fabric Workspace '{wsId}' を削除中...");
        try
        {
            await az.RunAsync(
                $"rest --method DELETE --url \"https://api.fabric.microsoft.com/v1/workspaces/{wsId}\" " +
                "--resource \"https://api.fabric.microsoft.com\"",
                silent: true);
            Console.WriteLine(" ✓");
            deleted.Add($"Fabric WS: {wsId}");
        }
        catch (Exception ex)
        {
            // 404 = already deleted
            if (ex.Message.Contains("404") || ex.Message.Contains("NotFound"))
                Console.WriteLine(" ✓ (既に削除済み)");
            else
                Console.WriteLine($" ⚠️ {ex.Message[..Math.Min(80, ex.Message.Length)]}");
        }
    }
    else
    {
        Console.WriteLine("  ℹ️ Fabric Workspace: 未設定 (スキップ)");
    }

    // 3. Entra ID App
    var appId = state.Deployment?.EntraAppId;
    if (!string.IsNullOrWhiteSpace(appId))
    {
        Console.Write($"  🗑️ Entra ID App '{appId}' を削除中...");
        try
        {
            await az.RunAsync($"ad app delete --id {appId}", silent: true);
            Console.WriteLine(" ✓");
            deleted.Add($"Entra App: {appId}");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("404") || ex.Message.Contains("does not exist"))
                Console.WriteLine(" ✓ (既に削除済み)");
            else
                Console.WriteLine($" ⚠️ {ex.Message[..Math.Min(80, ex.Message.Length)]}");
        }
    }
    else
    {
        Console.WriteLine("  ℹ️ Entra ID App: 未設定 (スキップ)");
    }

    // 4. setup-state.json を削除
    Console.WriteLine();
    if (File.Exists(stateFile))
    {
        File.Delete(stateFile);
        Console.WriteLine($"  🗑️ {stateFile} を削除しました。");
    }

    Console.WriteLine();
    Console.WriteLine("  ───────────────────────────────────────────────");
    Console.WriteLine($"  ✅ クリーンアップ完了 ({deleted.Count} リソース削除)");
    foreach (var item in deleted)
        Console.WriteLine($"     • {item}");
    Console.WriteLine();
    Console.WriteLine("  💡 RG の完全削除には数分かかります。");
    Console.WriteLine("     再構築するには: dotnet run -- --step 1");
    Console.WriteLine();
}
