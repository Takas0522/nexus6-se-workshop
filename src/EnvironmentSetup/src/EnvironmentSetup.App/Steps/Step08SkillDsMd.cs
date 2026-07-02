using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ8: Skill/DS.md 作成 - Fabric/Foundry用のSkillとDS.mdを生成
/// </summary>
public class Step08SkillDsMd : ISetupStep
{
    private readonly CopilotService _copilot;

    public int StepNumber => 8;
    public string Name => "Skill/DS.md 作成";

    public Step08SkillDsMd(CopilotService copilot)
    {
        _copilot = copilot;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var analysis = state.Analysis
            ?? throw new InvalidOperationException("分析が未完了です。Step 2 を先に実行してください。");
        var assessment = state.Assessment
            ?? throw new InvalidOperationException("アセスメントが未完了です。Step 1 を先に実行してください。");

        Console.WriteLine("  Copilot SDK を使用して Skill/DS.md を生成中...\n");

        var outputDir = Path.GetFullPath("./output/skills");
        var dsDir = Path.Combine(outputDir, "ds");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(dsDir);

        var systemContext = """
            あなたはAI Foundry/Fabricのスキル設計の専門家です。
            ニュース分析エージェントが使用するスキルとデータソース定義を作成します。

            スキルの構成:
            - 各業務領域に3つずつのスキル（合計9スキル）
            - 各スキルはニュースシナリオに対応するインパクト判断ロジックを含む
            
            DS.mdの構成:
            - fabric-ontology.ds.md: Fabricのデータモデル・関係性を定義
            - agent-data-mapping.ds.md: Hosted Agentが使用するデータマッピング
            """;

        // 各業務領域×シナリオのスキル生成
        for (var domainIdx = 0; domainIdx < assessment.Domains.Count; domainIdx++)
        {
            var domain = assessment.Domains[domainIdx];
            var domainSlug = GetDomainSlug(domain, domainIdx);

            for (var scenarioIdx = 0; scenarioIdx < analysis.NewsScenarios.Count && scenarioIdx < 3; scenarioIdx++)
            {
                var scenario = analysis.NewsScenarios[scenarioIdx];
                var fileName = $"{domainSlug}_skill_{scenario.Id}.md";

                Console.WriteLine($"  📝 {fileName} を生成中...");

                var prompt = $"""
                    以下の条件でスキル定義Markdownを作成してください。

                    業務領域: {domain}
                    ニュースシナリオ: {scenario.Title} ({scenario.Category})
                    影響概要: {scenario.Summary}

                    スキルMDの構成:
                    - タイトル（日本語）
                    - 概要（このスキルが何を判断するか）
                    - 入力パラメータ（ニュース情報、業務データ）
                    - 判断ロジック（閾値、条件分岐）
                    - 出力（インパクトスコア、推奨アクション）
                    - 参照データソース（Fabric テーブル名）

                    Markdownのみ出力してください。
                    """;

                var skillMd = await _copilot.GenerateWithContextAsync(systemContext, prompt);
                await File.WriteAllTextAsync(Path.Combine(outputDir, fileName), skillMd, ct);
            }
        }

        // fabric-ontology.ds.md 生成
        Console.WriteLine("\n  📝 fabric-ontology.ds.md を生成中...");
        var ontologyPrompt = $"""
            以下のテーブル構成に基づいて、Fabricオントロジー定義の DS.md を作成してください。

            テーブル一覧:
            {string.Join("\n", analysis.Tables.Select(t => $"- {t.Database}.{t.TableName}: {t.Description}"))}

            DS.mdの構成:
            - データソース概要
            - エンティティ定義（各テーブルのエンティティとしての意味）
            - リレーション定義（テーブル間の関係性）
            - メダリオン構成（Bronze/Silver/Gold レイヤー定義）
            - KPI定義（Gold層の集計テーブル）

            Markdownのみ出力してください。
            """;
        var ontologyMd = await _copilot.GenerateWithContextAsync(systemContext, ontologyPrompt);
        await File.WriteAllTextAsync(Path.Combine(dsDir, "fabric-ontology.ds.md"), ontologyMd, ct);

        // agent-data-mapping.ds.md 生成
        Console.WriteLine("  📝 agent-data-mapping.ds.md を生成中...");
        var mappingPrompt = $"""
            以下の情報に基づいて、Hosted Agent用のデータマッピング DS.md を作成してください。

            業務領域: {string.Join(", ", assessment.Domains)}
            エージェント構成:
            - Agent 1: Web情報収集 (Bing Grounding)
            - Agent 2: ビジネスインパクト評価 (Fabric KPIデータ参照)
            - Agent 3: 事業部別レコメンド (各領域の業務データ参照)
            - Agent 4: 通知 (Teams送信)

            テーブル一覧:
            {string.Join("\n", analysis.Tables.Select(t => $"- {t.Database}.{t.TableName}"))}

            DS.mdの構成:
            - エージェントごとに使用するデータソースのマッピング
            - 各テーブルからの取得クエリパターン
            - データフロー（どのエージェントがどのデータを読み書きするか）
            - スキルとデータの対応関係

            Markdownのみ出力してください。
            """;
        var mappingMd = await _copilot.GenerateWithContextAsync(systemContext, mappingPrompt);
        await File.WriteAllTextAsync(Path.Combine(dsDir, "agent-data-mapping.ds.md"), mappingMd, ct);

        Console.WriteLine($"\n  ✓ Skill/DS.md 生成完了: {outputDir}");
    }

    private static string GetDomainSlug(string domain, int index)
    {
        // 日本語ドメイン名をスラッグ化
        return index switch
        {
            0 => "mobile",
            1 => "ecommerce",
            2 => "fintech",
            _ => $"domain{index + 1}"
        };
    }
}
