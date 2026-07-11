using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ10: ニュースサイト作成 - Copilot SDKでデモニュースサイトを生成し src/news-portal/ にコピー
/// Container App の Docker ビルド時に最新の成果物が含まれるようにする
/// </summary>
public class Step10NewsSite : ISetupStep
{
    private readonly CopilotService _copilot;

    public int StepNumber => 10;
    public string Name => "ニュースサイト作成";

    public Step10NewsSite(CopilotService copilot)
    {
        _copilot = copilot;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var analysis = state.Analysis
            ?? throw new InvalidOperationException("分析が未完了です。Step 2 を先に実行してください。");
        var assessment = state.Assessment
            ?? throw new InvalidOperationException("アセスメントが未完了です。Step 1 を先に実行してください。");

        Console.WriteLine("  Copilot SDK を使用してデモニュースサイトを生成中...\n");

        var outputDir = Path.GetFullPath("./output/news-portal");
        Directory.CreateDirectory(outputDir);

        // 既存 news-portal のテンプレートを参照
        var templateContext = """
            既存のニュースポータルサイトは以下の構成です:
            - index.html: メインページ（ヘッダー、カテゴリタブ、速報ティッカー、記事カード）
            - article-*.html: 個別記事ページ
            - sitemap.xml, robots.txt
            
            デザイン: 赤いヘッダー、新聞風レイアウト、日本語コンテンツ
            """;

        // index.html 生成
        Console.WriteLine("  📰 index.html を生成中...");
        var indexPrompt = $"""
            {templateContext}

            以下のニュースシナリオに基づいて、日本語のニュースポータルサイトの index.html を生成してください。
            
            ニュースシナリオ:
            {string.Join("\n", analysis.NewsScenarios.Select((n, i) => $"{i + 1}. [{n.Category}] {n.Title}"))}

            業務領域: {string.Join(", ", assessment.Domains)}

            要件:
            - 完全なHTML5ドキュメント
            - インラインCSS（赤系のヘッダー、ニュース風レイアウト）
            - 3記事へのリンク (article-1.html, article-2.html, article-3.html)
            - レスポンシブデザイン
            - 日本語コンテンツ

            HTMLのみ出力してください。
            """;
        var indexHtml = await _copilot.GenerateAsync(indexPrompt);
        indexHtml = ExtractHtml(indexHtml);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "index.html"), indexHtml, ct);

        // 各記事ページ生成
        for (var i = 0; i < analysis.NewsScenarios.Count && i < 3; i++)
        {
            var scenario = analysis.NewsScenarios[i];
            Console.WriteLine($"  📰 article-{i + 1}.html を生成中 ({scenario.Title})...");

            var articlePrompt = $"""
                {templateContext}

                以下のニュースシナリオの記事ページ (article-{i + 1}.html) を生成してください。

                タイトル: {scenario.Title}
                カテゴリ: {scenario.Category}
                概要: {scenario.Summary}
                影響領域: {string.Join(", ", scenario.ImpactDomains)}

                要件:
                - 完全なHTML5ドキュメント
                - index.htmlと統一されたデザイン
                - 800〜1200文字程度の記事本文
                - 公開日、カテゴリ、関連記事リンク
                - ←トップに戻るリンク

                HTMLのみ出力してください。
                """;
            var articleHtml = await _copilot.GenerateAsync(articlePrompt);
            articleHtml = ExtractHtml(articleHtml);
            await File.WriteAllTextAsync(Path.Combine(outputDir, $"article-{i + 1}.html"), articleHtml, ct);
        }

        // sitemap.xml / robots.txt
        var sitemap = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>/index.html</loc></url>
              <url><loc>/article-1.html</loc></url>
              <url><loc>/article-2.html</loc></url>
              <url><loc>/article-3.html</loc></url>
            </urlset>
            """;
        await File.WriteAllTextAsync(Path.Combine(outputDir, "sitemap.xml"), sitemap, ct);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "robots.txt"), "User-agent: *\nAllow: /\nSitemap: /sitemap.xml\n", ct);

        Console.WriteLine($"\n  ✓ ニュースサイト生成完了: {outputDir}");

        // 生成した成果物を src/news-portal/ にコピー（Docker ビルドで最新版が含まれるように）
        var repoRoot = FindRepoRoot();
        var srcNewsPortal = Path.Combine(repoRoot, "src", "news-portal");
        Directory.CreateDirectory(srcNewsPortal);

        Console.WriteLine($"\n  📂 src/news-portal/ に成果物をコピー中...");
        foreach (var file in Directory.GetFiles(outputDir))
        {
            var destFile = Path.Combine(srcNewsPortal, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            Console.WriteLine($"    → {Path.GetFileName(file)}");
        }
        Console.WriteLine($"    ✓ {Directory.GetFiles(outputDir).Length} ファイルをコピー完了");
        Console.WriteLine("    ℹ️  Step 13 の Docker ビルドで Container App に含まれます");
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    private static string ExtractHtml(string response)
    {
        // ```html ... ``` ブロックがある場合は抽出
        var start = response.IndexOf("```html", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start = response.IndexOf('\n', start) + 1;
            var end = response.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return response[start..end].Trim();
        }

        // <!DOCTYPE or <html で始まる部分を抽出
        var docStart = response.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
        if (docStart < 0)
            docStart = response.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
        if (docStart >= 0)
            return response[docStart..].Trim();

        return response;
    }
}
