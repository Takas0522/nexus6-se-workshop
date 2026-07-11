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

        var repoRoot = FindRepoRoot();
        var srcNewsPortal = Path.Combine(repoRoot, "src", "news-portal");

        var domains = assessment.Domains;
        var scenarios = analysis.NewsScenarios;

        // Copilot SDK でニュースサイト生成を依頼
        // → SDK はワークスペースの src/news-portal/ に直接出力する
        Console.WriteLine("  📰 ニュースサイト生成を依頼中...");
        var prompt = $"""
            src/news-portal/ 配下に以下のファイルを生成（上書き）してください。

            ニュースシナリオ:
            {string.Join("\n", scenarios.Select((n, i) => $"{i + 1}. [{n.Category}] {n.Title}: {n.Summary}"))}

            業務領域: {string.Join(", ", domains)}

            生成するファイル:
            1. index.html - メインページ
               - <!DOCTYPE html> で始まる完全なHTML5
               - インラインCSS（赤系ヘッダー、新聞風レイアウト）
               - ヘッダーに「Nexus6 ニュースポータル」
               - 業務領域のナビゲーションタブ
               - 速報ティッカー（各シナリオの見出し）
               - 3記事カード（article-1.html, article-2.html, article-3.html へのリンク）
               - レスポンシブデザイン、日本語、年号2026年

            2. article-1.html, article-2.html, article-3.html - 各記事ページ
               - index.htmlと統一デザイン
               - 800〜1200文字の記事本文
               - 公開日2026年、カテゴリ、影響領域
               - ←トップに戻るリンク (index.html)

            すべてのファイルを実際に生成してください。
            """;
        await _copilot.GenerateAsync(prompt);

        // sitemap.xml / robots.txt は固定生成
        var sitemap = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>/index.html</loc></url>
              <url><loc>/article-1.html</loc></url>
              <url><loc>/article-2.html</loc></url>
              <url><loc>/article-3.html</loc></url>
            </urlset>
            """;
        await File.WriteAllTextAsync(Path.Combine(srcNewsPortal, "sitemap.xml"), sitemap, ct);
        await File.WriteAllTextAsync(Path.Combine(srcNewsPortal, "robots.txt"), "User-agent: *\nAllow: /\nSitemap: /sitemap.xml\n", ct);

        // Copilot SDK が src/news-portal/ に出力したファイルを検証
        var indexPath = Path.Combine(srcNewsPortal, "index.html");
        if (!File.Exists(indexPath) || !IsValidHtml(await File.ReadAllTextAsync(indexPath, ct)))
        {
            Console.WriteLine("    ⚠️ index.html が有効なHTMLではありません。フォールバックテンプレートを使用します。");
            await File.WriteAllTextAsync(indexPath, BuildFallbackIndexHtml(domains, scenarios), ct);
        }

        for (var i = 0; i < scenarios.Count && i < 3; i++)
        {
            var articlePath = Path.Combine(srcNewsPortal, $"article-{i + 1}.html");
            if (!File.Exists(articlePath) || !IsValidHtml(await File.ReadAllTextAsync(articlePath, ct)))
            {
                Console.WriteLine($"    ⚠️ article-{i + 1}.html が有効なHTMLではありません。フォールバックを使用します。");
                await File.WriteAllTextAsync(articlePath, BuildFallbackArticleHtml(scenarios[i], i + 1, domains), ct);
            }
        }

        // src/news-portal/ の成果物を output/news-portal/ にコピー（アーカイブ）
        Console.WriteLine($"\n  📂 src/news-portal/ → output/news-portal/ にコピー中...");
        foreach (var file in Directory.GetFiles(srcNewsPortal))
        {
            var destFile = Path.Combine(outputDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            Console.WriteLine($"    → {Path.GetFileName(file)}");
        }

        Console.WriteLine($"\n  ✓ ニュースサイト生成完了");
        Console.WriteLine($"    src/news-portal/: {Directory.GetFiles(srcNewsPortal).Length} ファイル");
        Console.WriteLine("    ℹ️  Step 13 の Docker ビルドで Container App に含まれます");
    }

    private static bool IsValidHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        return content.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("<html", StringComparison.OrdinalIgnoreCase);
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

    private static string BuildFallbackIndexHtml(List<string> domains, List<NewsScenario> scenarios)
    {
        var navTabs = string.Join("\n", domains.Select(d => $"            <span class=\"nav-tab\">{d}</span>"));
        var ticker = string.Join(" ｜ ", scenarios.Select(s => $"【{s.Category}】{s.Title}"));
        var cards = string.Join("\n", scenarios.Select((s, i) => $"""
                    <div class="card">
                      <span class="category">{s.Category}</span>
                      <h3><a href="article-{i + 1}.html">{s.Title}</a></h3>
                      <p>{s.Summary}</p>
                    </div>
            """));

        return $$"""
            <!DOCTYPE html>
            <html lang="ja">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>Nexus6 ニュースポータル</title>
              <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { font-family: 'Noto Sans JP', sans-serif; background: #f5f5f5; color: #333; }
                header { background: #c0392b; color: white; padding: 1rem 2rem; }
                header h1 { font-size: 1.5rem; }
                .nav { display: flex; gap: 1rem; padding: 0.5rem 2rem; background: #e74c3c; }
                .nav-tab { color: white; cursor: pointer; padding: 0.3rem 0.8rem; border-radius: 4px; font-size: 0.9rem; }
                .nav-tab:hover { background: rgba(255,255,255,0.2); }
                .ticker { background: #2c3e50; color: #ecf0f1; padding: 0.5rem 2rem; overflow: hidden; white-space: nowrap; font-size: 0.85rem; }
                .ticker span { animation: scroll 30s linear infinite; display: inline-block; }
                @keyframes scroll { from { transform: translateX(100%); } to { transform: translateX(-100%); } }
                main { max-width: 1000px; margin: 2rem auto; padding: 0 1rem; }
                .card { background: white; border-radius: 8px; padding: 1.5rem; margin-bottom: 1rem; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                .card h3 a { color: #c0392b; text-decoration: none; }
                .card h3 a:hover { text-decoration: underline; }
                .card .category { display: inline-block; background: #e74c3c; color: white; padding: 2px 8px; border-radius: 3px; font-size: 0.75rem; margin-bottom: 0.5rem; }
                .card p { color: #666; margin-top: 0.5rem; line-height: 1.6; }
                footer { text-align: center; padding: 2rem; color: #999; font-size: 0.8rem; }
              </style>
            </head>
            <body>
              <header><h1>📰 Nexus6 ニュースポータル</h1></header>
              <div class="nav">
            {{navTabs}}
              </div>
              <div class="ticker"><span>{{ticker}}</span></div>
              <main>
            {{cards}}
              </main>
              <footer>&copy; 2026 Nexus6 News Portal</footer>
            </body>
            </html>
            """;
    }

    private static string BuildFallbackArticleHtml(NewsScenario scenario, int articleNum, List<string> domains)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="ja">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{{scenario.Title}} - Nexus6 ニュースポータル</title>
              <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { font-family: 'Noto Sans JP', sans-serif; background: #f5f5f5; color: #333; }
                header { background: #c0392b; color: white; padding: 1rem 2rem; }
                header h1 { font-size: 1.5rem; }
                main { max-width: 800px; margin: 2rem auto; padding: 0 1rem; }
                .meta { color: #999; font-size: 0.85rem; margin-bottom: 1rem; }
                .category { display: inline-block; background: #e74c3c; color: white; padding: 2px 8px; border-radius: 3px; font-size: 0.75rem; }
                article { background: white; border-radius: 8px; padding: 2rem; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                article h2 { color: #c0392b; margin-bottom: 1rem; }
                article p { line-height: 1.8; margin-bottom: 1rem; }
                .back { display: inline-block; margin-top: 2rem; color: #c0392b; text-decoration: none; }
                .back:hover { text-decoration: underline; }
                .impact { margin-top: 1.5rem; padding: 1rem; background: #fdf2f2; border-radius: 4px; }
                .impact h4 { color: #c0392b; margin-bottom: 0.5rem; }
                footer { text-align: center; padding: 2rem; color: #999; font-size: 0.8rem; }
              </style>
            </head>
            <body>
              <header><h1>📰 Nexus6 ニュースポータル</h1></header>
              <main>
                <article>
                  <div class="meta">
                    <span class="category">{{scenario.Category}}</span> | 2026年7月11日
                  </div>
                  <h2>{{scenario.Title}}</h2>
                  <p>{{scenario.Summary}}</p>
                  <p>本ニュースは{{string.Join("、", domains)}}の各事業領域に影響を及ぼす可能性があります。特に{{string.Join("、", scenario.ImpactDomains)}}への影響が注目されます。</p>
                  <p>企業は戦略的対応を検討し、事業リスクの最小化と機会の最大化を図る必要があります。詳細な影響分析については、AI分析エンジンによるレポートをご確認ください。</p>
                  <div class="impact">
                    <h4>影響領域</h4>
                    <p>{{string.Join("、", scenario.ImpactDomains)}}</p>
                  </div>
                  <a href="index.html" class="back">← トップに戻る</a>
                </article>
              </main>
              <footer>&copy; 2026 Nexus6 News Portal</footer>
            </body>
            </html>
            """;
    }
}
