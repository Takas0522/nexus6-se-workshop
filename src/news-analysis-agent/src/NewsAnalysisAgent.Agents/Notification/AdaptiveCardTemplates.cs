using System.Text.Json;
using NewsAnalysisAgent.Models;

namespace NewsAnalysisAgent.Agents.Notification;

public static class AdaptiveCardTemplates
{
    private const string Template = """
        {
          "type": "message",
          "attachments": [
            {
              "contentType": "application/vnd.microsoft.card.adaptive",
              "content": {
                "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                "type": "AdaptiveCard",
                "version": "1.5",
                "body": [
                  {
                    "type": "Container",
                    "style": "emphasis",
                    "bleed": true,
                    "items": [
                      {
                        "type": "TextBlock",
                        "text": {{divisionTitle}},
                        "weight": "Bolder",
                        "size": "Large",
                        "color": {{headerColor}},
                        "wrap": true
                      },
                      {
                        "type": "TextBlock",
                        "text": {{headline}},
                        "weight": "Bolder",
                        "wrap": true
                      }
                    ]
                  },
                  {
                    "type": "FactSet",
                    "facts": [
                      { "title": "Risk", "value": {{riskLevel}} },
                      { "title": "Division", "value": {{division}} }
                    ]
                  },
                  {
                    "type": "TextBlock",
                    "text": "Next Actions",
                    "weight": "Bolder",
                    "spacing": "Medium"
                  },
                  {{nextActions}},
                  {
                    "type": "TextBlock",
                    "text": "Data References",
                    "weight": "Bolder",
                    "spacing": "Medium"
                  },
                  {{dataReferences}}
                ],
                "actions": [
                  {
                    "type": "Action.Submit",
                    "title": "確認済みにする",
                    "data": {
                      "division": {{division}},
                      "action": "acknowledge-recommendation"
                    }
                  }
                ]
              }
            }
          ]
        }
        """;

    public static string Build(DivisionRecommendation recommendation, string riskLevel)
    {
        var division = recommendation.Division.ToString();
        return Template
            .Replace("{{divisionTitle}}", JsonSerializer.Serialize($"{division} Web Pulse Recommendation"), StringComparison.Ordinal)
            .Replace("{{headerColor}}", JsonSerializer.Serialize(HeaderColor(recommendation.Division)), StringComparison.Ordinal)
            .Replace("{{headline}}", JsonSerializer.Serialize(recommendation.Headline), StringComparison.Ordinal)
            .Replace("{{riskLevel}}", JsonSerializer.Serialize(riskLevel), StringComparison.Ordinal)
            .Replace("{{division}}", JsonSerializer.Serialize(division), StringComparison.Ordinal)
            .Replace("{{nextActions}}", BuildTextBlocks(recommendation.NextActions), StringComparison.Ordinal)
            .Replace("{{dataReferences}}", BuildTextBlocks(recommendation.DataReferences), StringComparison.Ordinal);
    }

    private static string HeaderColor(DivisionKind division) => division switch
    {
        DivisionKind.Mobile => "Accent",
        DivisionKind.Ecommerce => "Good",
        DivisionKind.Fintech => "Attention",
        _ => "Default"
    };

    private static string BuildTextBlocks(IReadOnlyCollection<string> values)
    {
        var items = values.Count == 0 ? ["（なし）"] : values;
        return string.Join(",\n              ", items.Select(value => $$"""
            {
              "type": "TextBlock",
              "text": {{JsonSerializer.Serialize($"• {value}")}},
              "wrap": true
            }
            """));
    }
}
