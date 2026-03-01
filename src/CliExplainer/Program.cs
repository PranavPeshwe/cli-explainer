using CliExplainer;

// --- Argument Parsing ---
ParsedArgs parsed;
try
{
    parsed = ArgumentParser.Parse(args);
}
catch (ArgumentException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

// --- Handle --list-models (early exit) ---
if (parsed.ListModels)
{
    try
    {
        var models = await CopilotService.GetAvailableModelsAsync();
        Console.WriteLine("Available models:");
        foreach (var m in models)
            Console.WriteLine($"  {m}");
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync(
            $"Failed to list models. Is GitHub Copilot CLI installed?\n{ex.Message}");
        return 1;
    }
    return 0;
}

// --- Validate mutually exclusive input sources ---
if (parsed.SubprocessArgs is not null && parsed.FilePath is not null)
{
    await Console.Error.WriteLineAsync(
        "Cannot use -f/--file together with -- <command>. Choose one input source.");
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
        await Console.Error.WriteLineAsync(
            $"Failed to execute subprocess: {ex.Message}");
        return 1;
    }

    if (result.ExitCode == 0)
        return 0;

    if (!string.IsNullOrWhiteSpace(result.CombinedOutput))
    {
        Console.Error.WriteLine("--- Subprocess Output ---");
        Console.Error.Write(result.CombinedOutput);
        Console.Error.WriteLine("--- End Subprocess Output ---");
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
        await Console.Error.WriteLineAsync($"File not found: {parsed.FilePath}");
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
    await Console.Error.WriteLineAsync(
        "No error text provided. Pipe input, use -f <file>, or use -- <command>.");
    return 1;
}

// --- Initial Analysis ---
Action<string> onChunk = chunk => Console.Write(chunk);
Action<string> onDebug = msg => Console.Error.WriteLine($"[DEBUG] {msg}");

await using var service = new CopilotService(parsed.Model, parsed.Debug);

try
{
    await service.StartAnalysisAsync(errorText, commandText, onChunk, onDebug);
    Console.WriteLine();
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    await Console.Error.WriteLineAsync(
        $"Error during analysis. Is GitHub Copilot CLI installed?\n{ex.Message}");
    return 1;
}

// --- Interactive REPL Loop ---
if (!useRepl)
    return 0;

while (!cts.Token.IsCancellationRequested)
{
    Console.Write("\nAsk follow-up (or 'exit'): ");
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
        await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    }
}

Console.WriteLine("\nGoodbye.");
return 0;
