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

// --- Read Error Text ---
string? errorText = null;

if (parsed.FilePath is not null)
{
    if (!File.Exists(parsed.FilePath))
    {
        await Console.Error.WriteLineAsync($"File not found: {parsed.FilePath}");
        return 1;
    }
    errorText = await File.ReadAllTextAsync(parsed.FilePath);
}
else if (Console.IsInputRedirected)
{
    errorText = await Console.In.ReadToEndAsync();
}

if (string.IsNullOrWhiteSpace(errorText))
{
    await Console.Error.WriteLineAsync(
        "No error text provided. Pipe input or use -f <file>.");
    return 1;
}

// --- Ctrl+C Handler ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// --- Initial Analysis ---
Action<string> onChunk = chunk => Console.Write(chunk);
Action<string> onDebug = msg => Console.Error.WriteLine($"[DEBUG] {msg}");

await using var service = new CopilotService(parsed.Model, parsed.Debug);

try
{
    await service.StartAnalysisAsync(errorText, parsed.Command, onChunk, onDebug);
    Console.WriteLine();
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    await Console.Error.WriteLineAsync(
        $"Error during analysis. Is GitHub Copilot CLI installed?\n{ex.Message}");
    return 1;
}

// --- Interactive REPL Loop ---
// When stdin is redirected (piped input), there is no interactive terminal for follow-ups.
if (Console.IsInputRedirected)
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
