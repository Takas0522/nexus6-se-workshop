using System.Text;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ3: レポート作成 - テーブル/ニュース/通知/データ数をレポートとして出力
/// </summary>
public class Step03Report : ISetupStep
{
    private readonly CopilotService _copilot;
    private readonly NonInteractiveConfig _niConfig;

    public int StepNumber => 3;
    public string Name => "レポート作成";

    public Step03Report(CopilotService copilot, NonInteractiveConfig niConfig)
    {
        _copilot = copilot;
        _niConfig = niConfig;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var analysis = state.Analysis
            ?? throw new InvalidOperationException("分析が未完了です。Step 2 を先に実行してください。");

        Console.WriteLine("  Copilot SDK を使用してレポートを生成中...\n");

        var prompt = $"""
            以下の分析結果を元に、環境構築レポートをMarkdown形式で作成してください。

            ## 要件
            - テーブル設計一覧（DB名、テーブル名、想定行数）
            - 想定ニュース（3件のタイトルと概要）
            - 通知内容サンプル（各領域×各ニュース = 9パターンの通知タイトルと要約）
            - データ数サマリ（顧客数、月間トランザクション、6ヶ月合計）
            - マスタデータ一覧

            ## 分析データ
            テーブル数: {analysis.Tables.Count}
            ニュースシナリオ: {analysis.NewsScenarios.Count}件
            総顧客数: {analysis.EstimatedDataCounts.TotalCustomers:N0}
            総レコード数: {analysis.EstimatedDataCounts.TotalRecords:N0}
            期間: {analysis.EstimatedDataCounts.TotalMonths}ヶ月

            テーブル一覧:
            {string.Join("\n", analysis.Tables.Select(t => $"- {t.Database}.{t.TableName}: {t.Description} ({t.EstimatedRows:N0}行)"))}

            ニュースシナリオ:
            {string.Join("\n", analysis.NewsScenarios.Select(n => $"- [{n.Category}] {n.Title}: {n.Summary}"))}

            Markdownのみ出力してください。
            """;

        var report = await _copilot.GenerateAsync(prompt);

        // レポート出力
        var outputDir = Path.GetFullPath("./output");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "report.md");
        await File.WriteAllTextAsync(outputPath, report, ct);

        // コンソール表示
        Console.WriteLine("  ─── レポート ───\n");
        Console.WriteLine(report);
        Console.WriteLine($"\n  📄 レポートを保存しました: {outputPath}\n");

        // 続行確認
        if (!_niConfig.Enabled)
        {
            Console.Write("  続行しますか? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm != "y" && confirm != "yes")
                throw new OperationCanceledException("ユーザーにより中断されました。");
        }

        state.Report = new ReportOutput
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Approved = true,
            OutputPath = outputPath,
            Content = report
        };
    }
}
