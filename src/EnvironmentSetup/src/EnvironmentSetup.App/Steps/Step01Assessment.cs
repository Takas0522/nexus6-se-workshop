using EnvironmentSetup.App.Models;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ1: アセスメント - 業務領域、従業員数、通知設定の入力
/// </summary>
public class Step01Assessment : ISetupStep
{
    private readonly bool _useDefaults;
    private readonly NonInteractiveConfig _niConfig;

    public int StepNumber => 1;
    public string Name => "アセスメント";

    public Step01Assessment(bool useDefaults, NonInteractiveConfig niConfig)
    {
        _useDefaults = useDefaults;
        _niConfig = niConfig;
    }

    public Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var input = new AssessmentInput();

        if (_niConfig.Enabled && _niConfig.Domains.Length >= 3 && _niConfig.Employees > 0)
        {
            // 非対話モード: パラメータから直接設定
            input.Domains = [.. _niConfig.Domains];
            input.EmployeeCount = _niConfig.Employees;
            input.NotificationTeam = string.IsNullOrEmpty(_niConfig.NotificationTeam)
                ? "PartnerIQ-Alerts" : _niConfig.NotificationTeam;
            input.NotificationChannels = _niConfig.NotificationChannels.Length >= 1
                ? [.. _niConfig.NotificationChannels]
                : [.. _niConfig.Domains.Select((d, i) => $"{d}-alerts")];
        }
        else
        {
            Console.WriteLine("  業務領域と通知先を設定します。");
            Console.WriteLine("  (Enter でデフォルト値を使用します)\n");

            input.Domains =
            [
                PromptInput("  業務領域① ", "モバイル通信"),
                PromptInput("  業務領域② ", "Eコマース"),
                PromptInput("  業務領域③ ", "フィンテック"),
            ];

            var empStr = PromptInput("  従業員数 ", "5000");
            if (!int.TryParse(empStr, out var empCount) || empCount <= 0)
                throw new ArgumentException("従業員数は正の整数を入力してください。");
            input.EmployeeCount = empCount;

            input.NotificationTeam = PromptInput("  通知チーム名 ", "PartnerIQ-Alerts");

            input.NotificationChannels =
            [
                PromptInput("  通知チャネル① ", "mobile-alerts"),
                PromptInput("  通知チャネル② ", "ecommerce-alerts"),
                PromptInput("  通知チャネル③ ", "fintech-alerts"),
            ];
        }

        // 確認表示
        Console.WriteLine("\n  ─── 入力確認 ───");
        Console.WriteLine($"  業務領域: {string.Join(", ", input.Domains)}");
        Console.WriteLine($"  従業員数: {input.EmployeeCount:N0}名");
        Console.WriteLine($"  通知チーム: {input.NotificationTeam}");
        Console.WriteLine($"  通知チャネル: {string.Join(", ", input.NotificationChannels)}");
        Console.WriteLine();

        if (!_useDefaults && !_niConfig.Enabled)
        {
            Console.Write("  この内容で続行しますか? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm != "y" && confirm != "yes")
                throw new OperationCanceledException("ユーザーにより中断されました。");
        }

        state.Assessment = input;
        return Task.CompletedTask;
    }

    private string PromptInput(string label, string defaultValue)
    {
        if (_useDefaults)
        {
            Console.WriteLine($"{label}[{defaultValue}]: {defaultValue}");
            return defaultValue;
        }

        Console.Write($"{label}[{defaultValue}]: ");
        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(value))
            return defaultValue;
        return value;
    }
}
