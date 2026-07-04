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
    private readonly bool _verbose;
    private readonly bool _nonInteractive;

    public StepRunner(ISetupStep[] steps, StateManager stateManager, bool verbose = false, bool nonInteractive = false)
    {
        _steps = steps;
        _stateManager = stateManager;
        _verbose = verbose;
        _nonInteractive = nonInteractive;
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
            Console.WriteLine($"  📋 Step {step.StepNumber}/14: {step.Name}");
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
                    Console.WriteLine($"\n  ❌ エラー: {ex.Message}");
                    if (_verbose)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"\n  [Stack Trace]");
                        Console.WriteLine($"  {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"\n  [Inner Exception] {ex.InnerException.Message}");
                            Console.WriteLine($"  {ex.InnerException.StackTrace}");
                        }
                        Console.ResetColor();
                    }
                    Console.WriteLine();

                    if (_nonInteractive)
                    {
                        // 非対話モード: エラー時は即座に中断
                        throw new InvalidOperationException(
                            $"Step {step.StepNumber} ({step.Name}) が失敗しました: {ex.Message}", ex);
                    }

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
