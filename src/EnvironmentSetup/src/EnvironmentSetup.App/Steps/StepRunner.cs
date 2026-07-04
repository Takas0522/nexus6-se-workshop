using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップを順次実行するランナー。エラー時はリトライ/スキップ選択を提供する。
/// </summary>
public class StepRunner
{
    private readonly ISetupStep[] _steps;
    private readonly StateManager _stateManager;

    public StepRunner(ISetupStep[] steps, StateManager stateManager)
    {
        _steps = steps;
        _stateManager = stateManager;
    }

    public async Task ExecuteFromAsync(SetupState state, int startStep, CancellationToken ct = default)
    {
        foreach (var step in _steps.Where(s => s.StepNumber >= startStep))
        {
            if (_stateManager.IsStepCompleted(state, step.StepNumber))
            {
                Console.WriteLine($"  ⏭️  Step {step.StepNumber}: {step.Name} (完了済み - スキップ)");
                continue;
            }

            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"  📋 Step {step.StepNumber}/13: {step.Name}");
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();

            var success = false;
            while (!success)
            {
                try
                {
                    await step.ExecuteAsync(state, ct);
                    await _stateManager.MarkStepCompletedAsync(state, step.StepNumber);
                    Console.WriteLine($"\n  ✅ Step {step.StepNumber} 完了\n");
                    success = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n  ❌ エラー: {ex.Message}\n");
                    Console.Write("  再試行(r) / スキップ(s) / 中断(q): ");
                    var choice = Console.ReadLine()?.Trim().ToLowerInvariant();
                    switch (choice)
                    {
                        case "r":
                            continue;
                        case "s":
                            Console.WriteLine("  ⏭️  スキップしました");
                            success = true;
                            break;
                        default:
                            throw new OperationCanceledException("ユーザーにより中断されました");
                    }
                }
            }
        }
    }
}
