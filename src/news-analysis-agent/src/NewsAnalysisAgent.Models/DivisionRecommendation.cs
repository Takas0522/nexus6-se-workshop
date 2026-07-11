namespace NewsAnalysisAgent.Models;

public sealed record DivisionRecommendation(
    string Division,
    string Headline,
    NextAction[] NextActions,
    string[] DataReferences)
{
    public string[] SourceFiles { get; init; } = [];
    public KpiReference[] KpiReferences { get; init; } = [];
    public CategorizedReferences? CategorizedReferences { get; init; }
}

public sealed record NextAction(string Title, string Body)
{
    public static NextAction FromText(string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new NextAction("対応確認", string.Empty);
        }

        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        if (newline > 0)
        {
            return new NextAction(text[..newline].Trim(), text[(newline + 1)..].Trim());
        }

        var period = text.IndexOf('。');
        if (period is > 0 and < 40 && period + 1 < text.Length)
        {
            return new NextAction(text[..(period + 1)].Trim(), text[(period + 1)..].Trim());
        }

        return text.Length <= 30
            ? new NextAction(text, string.Empty)
            : new NextAction(text[..30].TrimEnd('、', '。', ' '), text[30..].TrimStart('、', '。', ' '));
    }
}
