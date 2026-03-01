using CliExplainer;

// --- Argument Parsing ---
ParsedArgs parsed;
try
{
    parsed = ArgumentParser.Parse(args);
}
catch (ArgumentException ex)
{
    WriteColored(ex.Message, ConsoleColor.Red);
    return 1;
}

// --- Handle --list-models (early exit) ---
if (parsed.ListModels)
{
    try
    {
        var models = await CopilotService.GetAvailableModelsAsync();
        WriteColored("Available models:", ConsoleColor.Green);
        foreach (var m in models)
            WriteColored($"  {m}", ConsoleColor.Green);
    }
    catch (Exception ex)
    {
        WriteColored(
            $"Failed to list models. Is GitHub Copilot CLI installed?\n{ex.Message}",
            ConsoleColor.Red);
        return 1;
    }
    return 0;
}

// --- Validate mutually exclusive input sources ---
if (parsed.SubprocessArgs is not null && parsed.FilePath is not null)
{
    WriteColored(
        "Cannot use -f/--file together with -- <command>. Choose one input source.",
        ConsoleColor.Red);
    return 1;
}

// --- Ctrl+C Handler ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// --- Read Error Text ---
string? errorText = null;
string? commandText = parsed.Command;
bool useRepl;

if (parsed.SubprocessArgs is not null)
{
    // --- Subprocess Mode ---
    SubprocessResult result;
    try
    {
        result = await SubprocessRunner.RunAsync(parsed.SubprocessArgs, cts.Token);
    }
    catch (OperationCanceledException)
    {
        return 130;
    }
    catch (Exception ex)
    {
        WriteColored($"Failed to execute subprocess: {ex.Message}", ConsoleColor.Red);
        return 1;
    }

    if (result.ExitCode == 0)
        return 0;

    if (!string.IsNullOrWhiteSpace(result.CombinedOutput))
    {
        WriteColored("--- Subprocess Output ---", ConsoleColor.Yellow);
        Console.Error.Write(result.CombinedOutput);
        WriteColored("--- End Subprocess Output ---", ConsoleColor.Yellow);
        Console.Error.WriteLine();
    }

    errorText = result.CombinedOutput;
    commandText ??= string.Join(' ', parsed.SubprocessArgs);
    useRepl = true;
}
else if (parsed.FilePath is not null)
{
    if (!File.Exists(parsed.FilePath))
    {
        WriteColored($"File not found: {parsed.FilePath}", ConsoleColor.Red);
        return 1;
    }
    errorText = await File.ReadAllTextAsync(parsed.FilePath);
    useRepl = true;
}
else if (Console.IsInputRedirected)
{
    errorText = await Console.In.ReadToEndAsync();
    useRepl = false;
}
else
{
    errorText = null;
    useRepl = false;
}

if (string.IsNullOrWhiteSpace(errorText))
{
    WriteColored(
        "No error text provided. Pipe input, use -f <file>, or use -- <command>.",
        ConsoleColor.Red);
    return 1;
}

// --- Initial Analysis ---
Action<string> onChunk = chunk => WriteChunkColored(chunk, ConsoleColor.Cyan);
Action<string> onDebug = msg => WriteColored($"[DEBUG] {msg}", ConsoleColor.DarkGray);

await using var service = new CopilotService(parsed.Model, parsed.Debug);

try
{
    await service.StartAnalysisAsync(errorText, commandText, onChunk, onDebug);
    Console.WriteLine();
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    WriteColored(
        $"Error during analysis. Is GitHub Copilot CLI installed?\n{ex.Message}",
        ConsoleColor.Red);
    return 1;
}

// --- Interactive REPL Loop ---
if (!useRepl)
    return 0;

while (!cts.Token.IsCancellationRequested)
{
    Console.WriteLine();
    WriteChunkColored("Ask follow-up (or 'exit'): ", ConsoleColor.Red);
    string? input = Console.ReadLine();

    if (input is null ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(input))
        continue;

    try
    {
        await service.AskFollowUpAsync(input, onChunk, onDebug);
        Console.WriteLine();
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        WriteColored($"Error: {ex.Message}", ConsoleColor.Red);
    }
}

WriteColored("\nGoodbye.", ConsoleColor.Green);
return 0;

// --- Color helpers ---

static void WriteColored(string message, ConsoleColor color)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Error.WriteLine(message);
    Console.ForegroundColor = prev;
}

static void WriteChunkColored(string text, ConsoleColor color)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ForegroundColor = prev;
}
