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
        new Step10NewsSite(copilotService),
        new Step11SkillDsMd(copilotService),
        new Step12FoundryKnowledge(azureCli),
        new Step13AppDeploy(azureCli, niConfig),
        new Step14EntraId(azureCli),
    };

    var runner = new StepRunner(steps, stateManager, verbose, nonInteractive);
    await runner.ExecuteFromAsync(state, step);

    Console.WriteLine();
    Console.WriteLine("✅ 環境構築が完了しました！");
});
