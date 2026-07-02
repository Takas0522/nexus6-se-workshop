using GitHub.Copilot;

namespace EnvironmentSetup.App.Services;

/// <summary>
/// GitHub Copilot SDK のラッパーサービス。
/// AI生成（分析、レポート、データ、ニュース、Skill）に使用する。
/// </summary>
public class CopilotService : IAsyncDisposable
{
    private CopilotClient? _client;
    private CopilotSession? _session;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _client = new CopilotClient();
        await _client.StartAsync();
        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-5",
            OnPermissionRequest = PermissionHandler.ApproveAll
        });
        _initialized = true;
    }

    /// <summary>
    /// プロンプトを送信し、アシスタントの応答を取得する
    /// </summary>
    public async Task<string> GenerateAsync(string prompt)
    {
        await InitializeAsync();

        var done = new TaskCompletionSource<string>();
        var content = "";

        using var subscription = _session!.On<SessionEvent>(evt =>
        {
            if (evt is AssistantMessageEvent msg)
                content = msg.Data.Content;
            else if (evt is SessionIdleEvent)
                done.TrySetResult(content);
            else if (evt is SessionErrorEvent err)
                done.TrySetException(new InvalidOperationException($"Copilot error: {err.Data.Message}"));
        });

        await _session.SendAsync(new MessageOptions { Prompt = prompt });
        return await done.Task;
    }

    /// <summary>
    /// システムメッセージ付きの新しいセッションでプロンプトを送信する
    /// </summary>
    public async Task<string> GenerateWithContextAsync(string systemContext, string prompt)
    {
        var fullPrompt = $"""
            <context>
            {systemContext}
            </context>

            {prompt}
            """;
        return await GenerateAsync(fullPrompt);
    }

    public async ValueTask DisposeAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
        if (_client != null)
        {
            await _client.StopAsync();
            _client = null;
        }
        _initialized = false;
    }
}
