using Microsoft.Extensions.Logging.Abstractions;
using NewsAnalysisAgent.Agents.Infrastructure.Knowledge;

namespace NewsAnalysisAgent.UnitTests.BusinessImpact;

public sealed class LocalFolderKnowledgeProviderTests
{
    [Fact]
    public async Task SearchAsync_ReadsKnowledgeFolderAndReturnsKeywordMatchedSnippets()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "TestArtifacts", "KnowledgeProvider");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(Path.Combine(root, "skill", "mobile"));
        Directory.CreateDirectory(Path.Combine(root, "ds"));
        await File.WriteAllTextAsync(Path.Combine(root, "skill", "mobile", "mobile_skill_fx-impact.md"), "# Mobile FX\n円安 端末コスト MNP 粗利");
        await File.WriteAllTextAsync(Path.Combine(root, "ds", "ds_fintech_ai.md"), "# Fintech DS\nローン 与信 金利");

        var provider = new LocalFolderKnowledgeProvider(root, NullLogger<LocalFolderKnowledgeProvider>.Instance);

        var snippets = await provider.SearchAsync("円安 端末コスト", maxResults: 6, maxSnippetLength: 1000, CancellationToken.None);

        var snippet = Assert.Single(snippets);
        Assert.Equal("mobile", snippet.Division);
        Assert.Equal("mobile_skill_fx-impact", snippet.Topic);
        Assert.Contains("端末コスト", snippet.Snippet);
    }
}
