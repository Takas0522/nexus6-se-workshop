using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NewsAnalysisAgent.Tools;

public interface IWebPageFetchPlugin
{
    Task<string> FetchAsync(string url, CancellationToken ct = default);
}

public sealed partial class WebPageFetchPlugin : IWebPageFetchPlugin
{
    private const int MaxBytes = 256 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebPageFetchPlugin> _logger;

    public WebPageFetchPlugin(HttpClient httpClient, ILogger<WebPageFetchPlugin> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout > RequestTimeout)
        {
            _httpClient.Timeout = RequestTimeout;
        }
    }

    public async Task<string> FetchAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only absolute HTTP(S) URLs can be fetched.", nameof(url));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("NewsAnalysisAgent/1.0");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping non-text response {MediaType} from {Url}", mediaType, url);
            return string.Empty;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var bytes = await ReadLimitedAsync(stream, ct);
        var charset = response.Content.Headers.ContentType?.CharSet;
        var html = Decode(bytes, charset);
        return ExtractReadableText(html);
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream(capacity: MaxBytes);
        var chunk = new byte[8192];
        while (buffer.Length < MaxBytes)
        {
            var remaining = MaxBytes - (int)buffer.Length;
            var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), ct);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string Decode(byte[] bytes, string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset).GetString(bytes);
            }
            catch (ArgumentException)
            {
                // Fall back to UTF-8 below.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string ExtractReadableText(string html)
    {
        var text = ScriptStyleRegex().Replace(html, " ");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text.Length <= MaxBytes ? text : text[..MaxBytes];
    }

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
