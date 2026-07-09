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

    var stateManager = new StateManager(stateFile);
    var state = await stateManager.LoadAsync();

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
        new Step09DomainConfigUpload(azureCli),
        new Step10NewsSite(copilotService, azureCli),
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
});
