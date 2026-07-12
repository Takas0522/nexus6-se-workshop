using System.Text.RegularExpressions;

namespace NewsAnalysisAgent.Models;

public sealed record SkillReference(string FileName, string TitleJa, string Owner);

public sealed record KpiReference(
    string PhysicalName,
    string LogicalNameJa,
    string? Value = null,
    string? Unit = null,
    string? Table = null);

public sealed record WebReference(string Title, string Url);

public sealed record CategorizedReferences(
    KpiReference[] FabricKpi,
    SkillReference[] Skill,
    WebReference[] Web,
    string[] Ds);

public static partial class ReferenceCatalog
{
    private static readonly Dictionary<string, SkillReference> SkillManifest = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fintech_skill_mortgage-rate-hike.md"] = new("fintech_skill_mortgage-rate-hike.md", "住宅ローン金利上昇インパクト判断", "金融部門 ベテラン審査担当"),
        ["fintech_skill_fx-exposure.md"] = new("fintech_skill_fx-exposure.md", "為替エクスポージャー影響判断", "金融部門 市場リスク担当"),
        ["fintech_skill_card-share.md"] = new("fintech_skill_card-share.md", "カード決済シェア低下シナリオ", "金融部門 決済事業担当"),
        ["mobile_skill_competitor-mnp.md"] = new("mobile_skill_competitor-mnp.md", "競合 MNP 流出シナリオ", "モバイル事業 顧客戦略"),
        ["mobile_skill_fx-impact.md"] = new("mobile_skill_fx-impact.md", "為替による端末調達コスト影響", "モバイル事業 端末調達"),
        ["mobile_skill_boj-installment.md"] = new("mobile_skill_boj-installment.md", "日銀利上げと端末分割払い影響", "モバイル事業 ファイナンス"),
        ["ecommerce_skill_consumer-sentiment.md"] = new("ecommerce_skill_consumer-sentiment.md", "消費者マインド悪化時の需要判断", "EC事業 需要予測"),
        ["ecommerce_skill_fx-crossborder.md"] = new("ecommerce_skill_fx-crossborder.md", "越境 EC 為替コスト影響", "EC事業 海外調達"),
        ["ecommerce_skill_point-competition.md"] = new("ecommerce_skill_point-competition.md", "ポイント競争と販促費判断", "EC事業 CRM・販促")
    };

    private static readonly Dictionary<string, KpiReference> KpiLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["loan_balance"] = new("loan_balance", "ローン残高", Unit: "円"),
        ["avg_interest_rate"] = new("avg_interest_rate", "平均貸出金利", Unit: "%"),
        ["fx_position_pnl"] = new("fx_position_pnl", "FXポジション損益合計", Unit: "円"),
        ["negative_credit_reviews"] = new("negative_credit_reviews", "否認・保留審査件数", Unit: "件"),
        ["overseas_card_amount"] = new("overseas_card_amount", "海外カード決済額", Unit: "円"),
        ["device_subsidy"] = new("device_subsidy", "端末補助額合計", Unit: "円"),
        ["mnp_out_count"] = new("mnp_out_count", "MNP転出件数", Unit: "件"),
        ["installment_payment"] = new("installment_payment", "分割払い月額合計", Unit: "円"),
        ["cancel_ticket_count"] = new("cancel_ticket_count", "解約問い合わせ件数", Unit: "件"),
        ["point_cost"] = new("point_cost", "ポイント還元コスト", Unit: "point"),
        ["campaign_reactions"] = new("campaign_reactions", "キャンペーン反応件数", Unit: "件"),
        ["cart_abandon_count"] = new("cart_abandon_count", "カート離脱件数", Unit: "件"),
        ["cross_border_cost"] = new("cross_border_cost", "越境 EC 仕入コスト", Unit: "円"),
        ["overseas_procurement_cost"] = new("overseas_procurement_cost", "海外調達コスト", Unit: "円"),
        ["mnp_out_rate"] = new("mnp_out_rate", "MNP転出率", Unit: "%"),
        ["device_fx_cost_jpy"] = new("device_fx_cost_jpy", "海外端末仕入コスト", Unit: "円"),
        ["campaign_roi"] = new("campaign_roi", "キャンペーンROI", Unit: "倍"),
        ["crossborder_fx_cost_jpy"] = new("crossborder_fx_cost_jpy", "越境EC為替コスト", Unit: "円"),
        ["fx_position_usd"] = new("fx_position_usd", "USD建てFXポジション", Unit: "USD"),
        ["loan_delinquency_rate"] = new("loan_delinquency_rate", "ローン延滞率", Unit: "%"),
        ["gross_revenue_jpy"] = new("gross_revenue_jpy", "月次売上", Unit: "円"),
        ["total_cost_jpy"] = new("total_cost_jpy", "総コスト", Unit: "円"),
        ["gross_margin_jpy"] = new("gross_margin_jpy", "粗利", Unit: "円"),
        ["gross_margin_rate"] = new("gross_margin_rate", "粗利率", Unit: "%"),
        ["fx_exposure_usd"] = new("fx_exposure_usd", "USD為替エクスポージャー", Unit: "USD"),
        ["fx_exposure_other_jpy"] = new("fx_exposure_other_jpy", "その他通貨為替エクスポージャー", Unit: "円"),
        ["active_customer_count"] = new("active_customer_count", "アクティブ顧客数", Unit: "人"),
        ["churned_customer_count"] = new("churned_customer_count", "離脱顧客数", Unit: "人"),
        ["churn_rate"] = new("churn_rate", "解約率", Unit: "%"),
        ["total_cost_jpy"] = new("total_cost_jpy", "総コスト", Unit: "円"),
        ["ad_revenue_jpy"] = new("ad_revenue_jpy", "広告収益", Unit: "円"),
        ["dau"] = new("dau", "DAU", Unit: "人"),
        ["subscription_revenue_jpy"] = new("subscription_revenue_jpy", "サブスクリプション収益", Unit: "円"),
        ["content_production_cost_jpy"] = new("content_production_cost_jpy", "コンテンツ制作費", Unit: "円"),
        ["streaming_infra_cost_jpy"] = new("streaming_infra_cost_jpy", "配信インフラコスト", Unit: "円"),
        ["game_revenue_jpy"] = new("game_revenue_jpy", "ゲーム売上", Unit: "円"),
        ["in_app_purchase_jpy"] = new("in_app_purchase_jpy", "アプリ内課金額", Unit: "円"),
        ["server_cost_jpy"] = new("server_cost_jpy", "サーバーコスト", Unit: "円"),
        ["concurrent_users"] = new("concurrent_users", "同時接続ユーザー数", Unit: "人")
    };

    public static CategorizedReferences BuildCategorized(
        DivisionRecommendation recommendation,
        WebResearchResult? webResearchResult)
    {
        var skills = recommendation.SourceFiles
            .Concat(recommendation.DataReferences)
            .Select(ResolveSkill)
            .Where(item => item is not null)
            .DistinctBy(item => item!.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item!)
            .ToArray();

        var kpis = recommendation.KpiReferences
            .Select(LocalizeKpi)
            .Concat(recommendation.DataReferences.Select(ResolveKpi).Where(item => item is not null).Select(item => item!))
            .DistinctBy(item => $"{item.Table}.{item.PhysicalName}", StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ds = recommendation.DataReferences
            .Where(value => value.Contains("ds_", StringComparison.OrdinalIgnoreCase) || value.EndsWith("DS.md", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var web = (webResearchResult?.SourceUrls ?? [])
            .Where(IsAllowedWebReference)
            .Select(url => new WebReference(Title: url, Url: url))
            .DistinctBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CategorizedReferences(kpis, skills, web, ds);
    }

    public static string LocalizeText(string value)
    {
        var result = value;
        foreach (var pair in KpiLabels)
        {
            result = result.Replace(pair.Key, pair.Value.LogicalNameJa, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var pair in SkillManifest)
        {
            result = result.Replace(pair.Key, pair.Value.TitleJa, StringComparison.OrdinalIgnoreCase);
        }

        return KpiValueFormatter.FormatText(result);
    }

    public static bool IsAllowedWebReference(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return !(uri.Host.Equals("example.com", StringComparison.OrdinalIgnoreCase) &&
                 uri.AbsolutePath.StartsWith("/mock-", StringComparison.OrdinalIgnoreCase));
    }

    public static SkillReference? ResolveSkill(string value)
    {
        var fileName = FileNameRegex().Matches(value).Select(match => match.Value).FirstOrDefault();
        if (fileName is null)
        {
            return null;
        }

        return SkillManifest.TryGetValue(fileName, out var reference)
            ? reference
            : new SkillReference(fileName, ToTitleCase(fileName), "未設定");
    }

    public static KpiReference LocalizeKpi(KpiReference reference)
    {
        var key = LastToken(reference.PhysicalName);
        if (!KpiLabels.TryGetValue(key, out var label))
        {
            return reference with { LogicalNameJa = string.IsNullOrWhiteSpace(reference.LogicalNameJa) ? key : reference.LogicalNameJa };
        }

        return reference with
        {
            PhysicalName = key,
            LogicalNameJa = label.LogicalNameJa,
            Unit = string.IsNullOrWhiteSpace(reference.Unit) ? label.Unit : reference.Unit
        };
    }

    private static KpiReference? ResolveKpi(string value)
    {
        var key = LastToken(value);
        if (!KpiLabels.TryGetValue(key, out var label))
        {
            return null;
        }

        var table = value.Contains('.', StringComparison.Ordinal) ? value[..value.LastIndexOf('.')] : null;
        return label with { Table = table };
    }

    private static string LastToken(string value)
    {
        var normalized = value.Trim('`', ' ', '[', ']');
        var lastDot = normalized.LastIndexOf('.');
        return lastDot >= 0 ? normalized[(lastDot + 1)..] : normalized;
    }

    private static string ToTitleCase(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ');

    [GeneratedRegex(@"[A-Za-z0-9]+_skill_[A-Za-z0-9_-]+\.md", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();
}
