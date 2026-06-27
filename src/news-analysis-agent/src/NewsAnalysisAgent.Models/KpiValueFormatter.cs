using System.Globalization;
using System.Text.RegularExpressions;

namespace NewsAnalysisAgent.Models;

public static partial class KpiValueFormatter
{
    private static readonly HashSet<string> RatioPercentFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "avg_interest_rate",
        "interest_rate",
        "loan_delinquency_rate",
        "mnp_out_rate",
        "gross_margin_rate",
        "point_rate"
    };

    public static string Format(KpiReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Value))
        {
            return "—";
        }

        return Format(reference.Value, reference.Unit, reference.PhysicalName);
    }

    public static string Format(string value, string? unit, string? physicalName = null)
    {
        var normalizedUnit = NormalizeUnit(unit);
        if (!TryParseDecimal(value, out var number))
        {
            return AppendUnit(value, normalizedUnit);
        }

        return normalizedUnit switch
        {
            "円" => $"{number:N0} 円",
            "件" or "人" or "個" or "月" or "分" => $"{number:N0} {normalizedUnit}",
            "USD" => $"{number:N0} USD",
            "%" => FormatPercent(number, physicalName),
            "倍" => $"{Trim(number, 2)} 倍",
            "point" => $"{number:N0} point",
            "" or "-" => Trim(number, 4),
            _ => $"{Trim(number, 4)} {normalizedUnit}"
        };
    }

    public static string FormatText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return NumericWithUnitRegex().Replace(text, match =>
        {
            var value = match.Groups["value"].Value;
            var unit = match.Groups["unit"].Value;
            return string.IsNullOrWhiteSpace(unit)
                ? match.Value
                : Format(value, unit);
        });
    }

    private static string FormatPercent(decimal number, string? physicalName)
    {
        var key = LastToken(physicalName);
        var isRatio = !string.IsNullOrWhiteSpace(key) && RatioPercentFields.Contains(key);
        var percent = isRatio && Math.Abs(number) <= 1 ? number * 100 : number;
        var decimals = key.Contains("interest", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        return $"{percent.ToString($"N{decimals}", CultureInfo.InvariantCulture)} %";
    }

    private static string AppendUnit(string value, string unit) =>
        string.IsNullOrWhiteSpace(unit) || unit == "-" ? value : $"{value} {unit}";

    private static string NormalizeUnit(string? unit) => unit?.Trim() switch
    {
        null or "" or "-" => "",
        "JPY" or "jpy" => "円",
        "ratio" => "倍",
        "points" => "point",
        var value => value
    };

    private static bool TryParseDecimal(string value, out decimal number) =>
        decimal.TryParse(
            value.Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out number);

    private static string Trim(decimal number, int maxDecimals)
    {
        var text = number.ToString($"N{maxDecimals}", CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal)
            ? text.TrimEnd('0').TrimEnd('.')
            : text;
    }

    private static string LastToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lastDot = value.LastIndexOf('.');
        return lastDot >= 0 ? value[(lastDot + 1)..] : value;
    }

    [GeneratedRegex(@"(?<![\w.])(?<value>-?\d{1,3}(?:,\d{3})*(?:\.\d+)?|-?\d+(?:\.\d+)?)\s*(?<unit>円|JPY|jpy|%|件|人|個|USD|倍|ratio|point|points)(?!\w)")]
    private static partial Regex NumericWithUnitRegex();
}
