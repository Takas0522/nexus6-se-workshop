using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Agents.Infrastructure.Knowledge;

public sealed class LocalFolderKnowledgeProvider : IKnowledgeProvider
{
    private static readonly char[] TokenSeparators = [' ', '　', '\t', '\r', '\n', ',', '.', '、', '。', '・', '/', '\\', '-', '_', ':', ';', '(', ')', '[', ']', '【', '】'];

    private readonly string _knowledgeRoot;
    private readonly ILogger<LocalFolderKnowledgeProvider> _logger;

    public LocalFolderKnowledgeProvider(IConfiguration configuration, ILogger<LocalFolderKnowledgeProvider> logger)
        : this(configuration["Knowledge:RootPath"] ?? ResolveDefaultKnowledgeRoot(), logger)
    {
    }

    public LocalFolderKnowledgeProvider(string knowledgeRoot, ILogger<LocalFolderKnowledgeProvider> logger)
    {
        _knowledgeRoot = knowledgeRoot;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(
        string query,
        int maxResults = 6,
        int maxSnippetLength = 1000,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(_knowledgeRoot))
        {
            _logger.LogWarning("Knowledge root {KnowledgeRoot} does not exist.", _knowledgeRoot);
            return [];
        }

        var tokens = ExtractTokens(query).ToArray();
        var candidates = new List<(KnowledgeSnippet Snippet, int Score)>();

        foreach (var path in Directory.EnumerateFiles(_knowledgeRoot, "*.md", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(path, ct);
            var relativePath = Path.GetRelativePath(_knowledgeRoot, path);
            var searchable = relativePath + "\n" + text;
            var score = tokens.Sum(token => CountOccurrences(searchable, token));
            if (score == 0 && tokens.Length > 0)
            {
                continue;
            }

            candidates.Add((new KnowledgeSnippet(
                ResolveDivision(relativePath),
                Path.GetFileNameWithoutExtension(path),
                Truncate(text, maxSnippetLength)), score));
        }

        if (candidates.Count == 0 && tokens.Length > 0)
        {
            return await SearchAsync(string.Empty, maxResults, maxSnippetLength, ct);
        }

        return candidates
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Snippet.Topic, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(static item => item.Snippet)
            .ToArray();
    }

    private static string ResolveDefaultKnowledgeRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "knowledge");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "knowledge");
    }

    private static IEnumerable<string> ExtractTokens(string text) =>
        text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(80);

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string ResolveDivision(string relativePath)
    {
        var value = relativePath.Replace('\\', '/').ToLowerInvariant();
        if (value.Contains("mobile")) return "mobile";
        if (value.Contains("ecommerce")) return "ecommerce";
        if (value.Contains("fintech")) return "fintech";
        return "common";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
