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
                    "text": "参照データ",
                    "weight": "Bolder",
                    "spacing": "Medium"
                  },
                  {{categorizedReferences}}
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
            .Replace("{{headline}}", JsonSerializer.Serialize(ReferenceCatalog.LocalizeText(recommendation.Headline)), StringComparison.Ordinal)
            .Replace("{{riskLevel}}", JsonSerializer.Serialize(riskLevel), StringComparison.Ordinal)
            .Replace("{{division}}", JsonSerializer.Serialize(division), StringComparison.Ordinal)
            .Replace("{{nextActions}}", BuildNextActionContainers(recommendation.NextActions), StringComparison.Ordinal)
            .Replace("{{categorizedReferences}}", BuildCategorizedReferenceBlocks(recommendation), StringComparison.Ordinal);
    }

    private static string HeaderColor(string division) => division.ToLowerInvariant() switch
    {
        "mobile" or "携帯電話事業" => "Accent",
        "ecommerce" or "sns事業" => "Good",
        "fintech" or "si事業" => "Attention",
        _ => "Default"
    };

    private static string BuildNextActionContainers(IReadOnlyCollection<NextAction> actions)
    {
        if (actions.Count == 0)
        {
            return """
            {
              "type": "TextBlock",
              "text": "（なし）",
              "wrap": true
            }
            """;
        }

        return string.Join(",\n              ", actions.Select((action, index) => $$"""
            {
              "type": "Container",
              "spacing": "Small",
              "items": [
                {
                  "type": "TextBlock",
                  "text": {{JsonSerializer.Serialize($"{ToCircledNumber(index + 1)} {ReferenceCatalog.LocalizeText(action.Title)}")}},
                  "weight": "Bolder",
                  "color": "Accent",
                  "wrap": true
                },
                {
                  "type": "TextBlock",
                  "text": {{JsonSerializer.Serialize(ReferenceCatalog.LocalizeText(action.Body))}},
                  "spacing": "Small",
                  "wrap": true
                }
              ]
            }
            """));
    }

    private static string BuildCategorizedReferenceBlocks(DivisionRecommendation recommendation)
    {
        var references = recommendation.CategorizedReferences ?? new CategorizedReferences(
            recommendation.KpiReferences.Select(ReferenceCatalog.LocalizeKpi).ToArray(),
            recommendation.SourceFiles.Select(ReferenceCatalog.ResolveSkill).Where(item => item is not null).Select(item => item!).ToArray(),
            [],
            recommendation.DataReferences.Where(value => value.Contains("ds_", StringComparison.OrdinalIgnoreCase)).ToArray());

        var blocks = new[]
        {
            BuildFabricKpiTable(references.FabricKpi),
            BuildSkillTable(references.Skill),
            BuildWebTable(references.Web.Where(item => ReferenceCatalog.IsAllowedWebReference(item.Url)).ToArray()),
            BuildDsFactSet(references.Ds)
        }.Where(static block => !string.IsNullOrWhiteSpace(block));

        return string.Join(",\n              ", blocks);
    }

    private static string BuildFabricKpiTable(IReadOnlyCollection<KpiReference> kpis)
    {
        if (kpis.Count == 0)
        {
            return BuildEmptyReferenceBlock("参照データ (Fabric KPI)");
        }

        return $$"""
            {
              "type": "TextBlock",
              "text": "参照データ (Fabric KPI)",
              "weight": "Bolder",
              "spacing": "Small",
              "wrap": true
            },
            {
              "type": "Table",
              "columns": [
                { "width": 2 },
                { "width": 1 }
              ],
              "rows": [
                {{BuildTableRow(["論理名", "値"], header: true)}},
                {{string.Join(",\n                ", kpis.Select(kpi => BuildTableRow([
                    kpi.LogicalNameJa,
                    KpiValueFormatter.Format(kpi)
                ])))}}
              ]
            }
            """;
    }

    private static string BuildSkillTable(IReadOnlyCollection<SkillReference> skills)
    {
        if (skills.Count == 0)
        {
            return BuildEmptyReferenceBlock("根拠 (Source)");
        }

        return $$"""
            {
              "type": "TextBlock",
              "text": "根拠 (Source)",
              "weight": "Bolder",
              "spacing": "Small",
              "wrap": true
            },
            {
              "type": "Table",
              "columns": [
                { "width": 2 },
                { "width": 1 }
              ],
              "rows": [
                {{BuildTableRow(["日本語タイトル", "担当"], header: true)}},
                {{string.Join(",\n                ", skills.Select(skill => BuildTableRow([skill.TitleJa, skill.Owner])))}}
              ]
            }
            """;
    }

    private static string BuildWebTable(IReadOnlyCollection<WebReference> webReferences)
    {
        if (webReferences.Count == 0)
        {
            return string.Empty;
        }

        return $$"""
            {
              "type": "TextBlock",
              "text": "Web リファレンス",
              "weight": "Bolder",
              "spacing": "Small",
              "wrap": true
            },
            {
              "type": "Table",
              "columns": [
                { "width": 1 }
              ],
              "rows": [
                {{BuildTableRow(["タイトル/URL"], header: true)}},
                {{string.Join(",\n                ", webReferences.Select(item => BuildTableRow([string.IsNullOrWhiteSpace(item.Title) ? item.Url : item.Title])))}}
              ]
            }
            """;
    }

    private static string BuildDsFactSet(IReadOnlyCollection<string> dsReferences)
    {
        if (dsReferences.Count == 0)
        {
            return string.Empty;
        }

        return $$"""
            {
              "type": "TextBlock",
              "text": "DS 定義",
              "weight": "Bolder",
              "spacing": "Small",
              "wrap": true
            },
            {
              "type": "FactSet",
              "facts": [
                {{string.Join(",\n                ", dsReferences.Select(item => $$"""{ "title": "DS.md", "value": {{JsonSerializer.Serialize(item)}} }"""))}}
              ]
            }
            """;
    }

    private static string BuildEmptyReferenceBlock(string title) => $$"""
            {
              "type": "TextBlock",
              "text": {{JsonSerializer.Serialize($"{title}: （なし）")}},
              "wrap": true,
              "spacing": "Small"
            }
            """;

    private static string BuildTableRow(IReadOnlyList<string> values, bool header = false) => $$"""
            {
              "type": "TableRow",
              "cells": [
                {{string.Join(",\n                ", values.Select(value => $$"""
                {
                  "type": "TableCell",
                  "items": [
                    {
                      "type": "TextBlock",
                      "text": {{JsonSerializer.Serialize(value)}},
                      "wrap": true{{(header ? ",\n                      \"weight\": \"Bolder\"" : string.Empty)}}
                    }
                  ]
                }
                """))}}
              ]
            }
            """;

    private static string ToCircledNumber(int value) => value is >= 1 and <= 10
        ? char.ConvertFromUtf32(0x245F + value)
        : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
