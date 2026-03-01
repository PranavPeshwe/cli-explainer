using GitHub.Copilot.SDK;

namespace CliExplainer;

public sealed class CopilotService : IAsyncDisposable
{
    internal const string SystemPrompt = """
        You are an expert sysadmin and developer. Analyze the command the user attempted (if provided) and the error output it produced. Output a structured response with exactly two sections:
        1) **Root Cause:** A concise, plain-English explanation of why it failed.
        2) **Fix:** The exact terminal command or specific code change needed to fix it.
        Do not include introductory/outro fluff.
        """;

    private readonly string? _model;
    private readonly bool _debug;
    private CopilotClient? _client;
    private CopilotSession? _session;
    private Action<string> _currentChunkHandler = _ => { };
    private Action<string> _currentDebugHandler = _ => { };
    private TaskCompletionSource<bool>? _completionSource;

    public CopilotService(string? model = null, bool debug = false)
    {
        _model = model;
        _debug = debug;
    }

    public static async Task<List<string>> GetAvailableModelsAsync()
    {
        using var client = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = true,
            UseStdio = true
        });

        await client.StartAsync();
        var models = await client.ListModelsAsync();
        await client.StopAsync();

        return models.Select(m => m.Id).ToList();
    }

    public async Task StartAnalysisAsync(
        string errorText,
        string? commandText,
        Action<string> onChunk,
        Action<string> onDebug)
    {
        _client = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = true,
            UseStdio = true
        });

        await _client.StartAsync();

        var config = new SessionConfig
        {
            Streaming = true,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Content = SystemPrompt
            }
        };

        if (_model is not null)
        {
            config.Model = _model;
        }

        _session = await _client.CreateSessionAsync(config);

        _currentChunkHandler = onChunk;
        _currentDebugHandler = onDebug;
        _completionSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    if (delta.Data.DeltaContent is not null)
                        _currentChunkHandler(delta.Data.DeltaContent);
                    break;
                case SessionIdleEvent:
                    _completionSource?.TrySetResult(true);
                    break;
                case SessionErrorEvent err:
                    if (_debug)
                        _currentDebugHandler($"[SDK Error] {err.Data.Message}");
                    _completionSource?.TrySetException(
                        new InvalidOperationException($"Session error: {err.Data.Message}"));
                    break;
                case SessionWarningEvent warn:
                    if (_debug)
                        _currentDebugHandler($"[SDK Warning] {warn.Data.Message}");
                    break;
                case SessionInfoEvent info:
                    if (_debug)
                        _currentDebugHandler($"[SDK Info] {info.Data.Message}");
                    break;
            }
        });

        var userMessage = commandText is not null
            ? $"Command: {commandText}\n\nError output:\n{errorText}"
            : $"Error output:\n{errorText}";

        await _session.SendAsync(new MessageOptions { Prompt = userMessage });
        await _completionSource.Task;
    }

    public async Task AskFollowUpAsync(
        string question,
        Action<string> onChunk,
        Action<string> onDebug)
    {
        if (_session is null)
            throw new InvalidOperationException(
                "StartAnalysisAsync must be called before AskFollowUpAsync.");

        _currentChunkHandler = onChunk;
        _currentDebugHandler = onDebug;
        _completionSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await _session.SendAsync(new MessageOptions { Prompt = question });
        await _completionSource.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }

        if (_client is not null)
        {
            await _client.StopAsync();
            _client.Dispose();
            _client = null;
        }
    }
}
