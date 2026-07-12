using System.Globalization;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ6: データ作成 - 各事業部の risk_summary Seed CSV を生成して output/seed に保存。
/// Step07 で Bronze にアップロード → Notebook 実行 → Gold テーブル化される。
/// </summary>
public class Step06DataCreation : ISetupStep
{
    private readonly CopilotService _copilot;

    public int StepNumber => 6;
    public string Name => "データ作成";

    public Step06DataCreation(CopilotService copilot)
    {
        _copilot = copilot;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var analysis = state.Analysis
            ?? throw new InvalidOperationException("分析が未完了です。Step 2 を先に実行してください。");
        var assessment = state.Assessment
            ?? throw new InvalidOperationException("アセスメントが未完了です。Step 1 を先に実行してください。");

        Console.WriteLine("  事業部 KPI Seed データを生成中...\n");

        var seedDir = Path.GetFullPath("./output/seed");
        Directory.CreateDirectory(seedDir);

        // 各事業部ごとに risk_summary CSV を生成
        foreach (var domain in assessment.Domains)
        {
            var divisionId = NormalizeDivisionId(domain);
            var tableName = $"{divisionId}_risk_summary";
            var csvPath = Path.Combine(seedDir, $"{tableName}.csv");

            Console.WriteLine($"  📊 {domain} ({tableName}) の KPI データを生成中...");

            // Copilot SDK で業種固有の KPI データを生成
            var prompt = $"""
                以下の事業部のKPI実績データをCSV形式で生成してください。

                事業部: {domain}
                従業員規模: {assessment.EmployeeCount}名

                CSVヘッダー: year_month,metric_name,metric_value,metric_unit,description
                
                要件:
                - 2025-01〜2026-06 の18ヶ月分
                - 各月に5〜8個のKPI指標を含める (合計90〜144行)
                - 指標例: 売上高, 営業利益率, 顧客数, ARPU, 解約率, NPS, 稼働率, コスト比率など
                - 値は業界に適したリアルな数値
                - metric_unit は 百万円, %, 千人, 円, ポイント, 件 などを使用
                - descriptionは20文字以内の簡潔な説明
                - ヘッダー行を含め、CSVのみ出力 (マークダウンや説明不要)

                CSVのみ出力してください。
                """;

            try
            {
                var csvContent = await _copilot.GenerateAsync(prompt);
                csvContent = CleanCsvOutput(csvContent);
                await File.WriteAllTextAsync(csvPath, csvContent, ct);
                var lineCount = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;
                Console.WriteLine($"    ✓ {lineCount} 行生成 → {csvPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ Copilot 生成失敗、フォールバックデータを使用: {ex.Message}");
                var fallback = GenerateFallbackCsv(domain, divisionId);
                await File.WriteAllTextAsync(csvPath, fallback, ct);
                Console.WriteLine($"    ✓ フォールバックデータ生成 → {csvPath}");
            }
        }

        // 汎用テーブルの seed CSV も生成 (analysis.Tables がある場合)
        foreach (var table in analysis.Tables)
        {
            var csvPath = Path.Combine(seedDir, $"{table.TableName}.csv");
            if (!File.Exists(csvPath))
            {
                var header = string.Join(",", table.Columns.Select(c => c.Name));
                await File.WriteAllTextAsync(csvPath, header + "\n", ct);
            }
        }

        Console.WriteLine($"\n  ✓ Seed データ生成完了: {seedDir}");
    }

    private static string CleanCsvOutput(string raw)
    {
        var lines = raw.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("```"))
            .ToList();
        var headerIdx = lines.FindIndex(l => l.Contains("year_month") && l.Contains("metric_name"));
        if (headerIdx > 0)
            lines = lines.Skip(headerIdx).ToList();
        return string.Join("\n", lines).Trim() + "\n";
    }

    private static string GenerateFallbackCsv(string domain, string divisionId)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("year_month,metric_name,metric_value,metric_unit,description");

        var metrics = new[]
        {
            ("売上高", 100.0, 180.0, "百万円", "月次売上高"),
            ("営業利益率", 8.0, 15.0, "%", "営業利益÷売上高"),
            ("顧客数", 50.0, 120.0, "千人", "アクティブ顧客数"),
            ("解約率", 1.5, 4.0, "%", "月次解約率"),
            ("NPS", 30.0, 60.0, "ポイント", "顧客推奨度指数"),
            ("稼働率", 95.0, 99.9, "%", "サービス稼働率"),
        };

        var rng = new Random(divisionId.GetHashCode());
        for (var y = 2025; y <= 2026; y++)
        {
            var maxMonth = y == 2026 ? 6 : 12;
            for (var m = 1; m <= maxMonth; m++)
            {
                var ym = $"{y}-{m:D2}";
                foreach (var (name, min, max, unit, desc) in metrics)
                {
                    var value = Math.Round(min + rng.NextDouble() * (max - min), 1);
                    sb.AppendLine($"{ym},{name},{value.ToString(CultureInfo.InvariantCulture)},{unit},{desc}");
                }
            }
        }

        return sb.ToString();
    }

    private static string NormalizeDivisionId(string domain)
    {
        // Fabric テーブル名は ASCII のみ対応。日本語ドメイン名をローマ字化
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["エンターテイメント"] = "entertainment",
            ["ゲーム"] = "game",
            ["sns"] = "sns",
            ["携帯電話"] = "mobile",
            ["si"] = "si",
            ["フィンテック"] = "fintech",
            ["eコマース"] = "ecommerce",
            ["通信"] = "telecom",
            ["メディア"] = "media",
            ["広告"] = "advertising",
            ["金融"] = "finance",
            ["保険"] = "insurance",
            ["不動産"] = "realestate",
            ["製造"] = "manufacturing",
            ["物流"] = "logistics",
            ["小売"] = "retail",
            ["教育"] = "education",
            ["医療"] = "healthcare",
            ["ヘルスケア"] = "healthcare",
        };

        var normalized = domain.ToLowerInvariant()
            .Replace("事業", "")
            .Replace("部門", "")
            .Replace("　", "")
            .Replace(" ", "")
            .Trim();

        // 完全一致
        if (map.TryGetValue(normalized, out var ascii))
            return ascii;

        // 部分一致
        foreach (var (jp, en) in map)
        {
            if (normalized.Contains(jp))
                return en;
        }

        // ASCII文字のみならそのまま使用
        if (normalized.All(c => c <= 127))
            return normalized.Replace(" ", "_");

        // フォールバック: ハッシュベースの安定名
        var hash = (uint)normalized.GetHashCode() & 0xFFFF;
        return $"div_{hash:x4}";
    }
}
