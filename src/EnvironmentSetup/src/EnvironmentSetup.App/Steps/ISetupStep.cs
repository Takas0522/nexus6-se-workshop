using EnvironmentSetup.App.Models;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// 各セットアップステップが実装するインターフェース
/// </summary>
public interface ISetupStep
{
    int StepNumber { get; }
    string Name { get; }
    Task ExecuteAsync(SetupState state, CancellationToken ct = default);
}
