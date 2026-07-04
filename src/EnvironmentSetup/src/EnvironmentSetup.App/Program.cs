using ConsoleAppFramework;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;
using EnvironmentSetup.App.Steps;

ConsoleApp.Run(args, async (int step = 1, string stateFile = "./setup-state.json", string logDir = "./logs", bool useDefaults = false, bool verbose = false) =>
{
    Console.WriteLine("╔══════════════════════════════════════════════╗");
    Console.WriteLine("║   EnvironmentSetup CLI - 環境構築ツール     ║");
    Console.WriteLine("╚══════════════════════════════════════════════╝");
    Console.WriteLine();

    if (verbose)
        Console.WriteLine("  🔍 Verbose モード: ON\n");

    Directory.CreateDirectory(logDir);

    var stateManager = new StateManager(stateFile);
    var state = await stateManager.LoadAsync();

    await using var copilotService = new CopilotService();
    var azureCli = new AzureCliWrapper(verbose, logDir);

    var steps = new ISetupStep[]
    {
        new Step01Assessment(useDefaults),
        new Step02Analysis(copilotService),
        new Step03Report(copilotService),
        new Step04AzureLogin(azureCli),
        new Step05BicepDeploy(azureCli),
        new Step06DataCreation(copilotService),
        new Step07MedallionSetup(azureCli),
        new Step08OntologyCreation(azureCli),
        new Step09DomainConfigUpload(azureCli),
        new Step10NewsSite(copilotService),
        new Step11SkillDsMd(copilotService),
        new Step12FoundryKnowledge(azureCli),
        new Step13AppDeploy(azureCli),
        new Step14EntraId(azureCli),
    };

    var runner = new StepRunner(steps, stateManager, verbose);
    await runner.ExecuteFromAsync(state, step);

    Console.WriteLine();
    Console.WriteLine("✅ 環境構築が完了しました！");
});
