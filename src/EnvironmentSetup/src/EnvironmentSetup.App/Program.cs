using ConsoleAppFramework;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;
using EnvironmentSetup.App.Steps;

ConsoleApp.Run(args, async (int step = 1, string stateFile = "./setup-state.json", string logDir = "./logs", bool useDefaults = false) =>
{
    Console.WriteLine("╔══════════════════════════════════════════════╗");
    Console.WriteLine("║   EnvironmentSetup CLI - 環境構築ツール     ║");
    Console.WriteLine("╚══════════════════════════════════════════════╝");
    Console.WriteLine();

    Directory.CreateDirectory(logDir);

    var stateManager = new StateManager(stateFile);
    var state = await stateManager.LoadAsync();

    await using var copilotService = new CopilotService();
    var azureCli = new AzureCliWrapper();

    var steps = new ISetupStep[]
    {
        new Step01Assessment(useDefaults),
        new Step02Analysis(copilotService),
        new Step03Report(copilotService),
        new Step04AzureLogin(azureCli),
        new Step05BicepDeploy(azureCli),
        new Step06DataCreation(copilotService),
        new Step07NewsSite(copilotService),
        new Step08SkillDsMd(copilotService),
        new Step09AppDeploy(azureCli),
        new Step10EntraId(azureCli),
    };

    var runner = new StepRunner(steps, stateManager);
    await runner.ExecuteFromAsync(state, step);

    Console.WriteLine();
    Console.WriteLine("✅ 環境構築が完了しました！");
});
