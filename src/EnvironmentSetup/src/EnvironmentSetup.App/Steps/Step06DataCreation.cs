using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ6: データ作成 - Copilot SDKでデータ生成しFabric SQLに投入
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
        var deployment = state.Deployment
            ?? throw new InvalidOperationException("デプロイが未完了です。Step 5 を先に実行してください。");

        Console.WriteLine("  Copilot SDK を使用してデモデータを生成中...\n");

        var outputDir = Path.GetFullPath("./output/data");
        Directory.CreateDirectory(outputDir);

        // 各データベースのデータを生成
        var databases = analysis.Tables
            .Select(t => t.Database)
            .Distinct()
            .ToList();

        foreach (var db in databases)
        {
            var tables = analysis.Tables.Where(t => t.Database == db).ToList();
            Console.WriteLine($"  📊 {db} のデータを生成中 ({tables.Count} テーブル)...");

            var prompt = $"""
                以下のテーブル定義に基づいて、リアルなデモデータのINSERT文を生成してください。
                データベース: {db}
                
                テーブル:
                {string.Join("\n", tables.Select(t => $"- {t.TableName} ({t.EstimatedRows}行): {t.Description}\n  列: {string.Join(", ", t.Columns.Select(c => $"{c.Name}({c.Type})"))}"))}

                要件:
                - 日本語のリアルなデモデータ
                - 各テーブルの最初の100行分のINSERT文を生成
                - SQL Server構文 (nvarchar, datetime2等)
                - 値は業務領域にふさわしいリアルなデータ

                INSERT文のみ出力してください。
                """;

            var sql = await _copilot.GenerateAsync(prompt);
            var filePath = Path.Combine(outputDir, $"{db}_data.sql");
            await File.WriteAllTextAsync(filePath, sql, ct);
            Console.WriteLine($"    → {filePath}");
        }

        // Fabric SQL への投入
        if (!string.IsNullOrEmpty(deployment.FabricSqlEndpoint))
        {
            Console.WriteLine("\n  Fabric SQL にデータを投入中...");
            Console.WriteLine($"    エンドポイント: {deployment.FabricSqlEndpoint}");

            // Note: 実際の投入は sqlcmd や ADO.NET で行う
            // ここではファイル生成までとし、投入コマンドを案内
            Console.WriteLine("\n  ℹ️  生成されたSQLファイルを Fabric SQL に投入するには:");
            Console.WriteLine($"    sqlcmd -S {deployment.FabricSqlEndpoint} -d <db_name> -G -i <file.sql>");
        }
        else
        {
            Console.WriteLine("\n  ⚠️ Fabric SQL エンドポイントが未設定です。");
            Console.WriteLine("  生成されたSQLファイルを手動で投入してください。");
        }

        Console.WriteLine($"\n  ✓ データファイル生成完了: {outputDir}");
    }
}
