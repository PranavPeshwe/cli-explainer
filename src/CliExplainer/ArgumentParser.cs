namespace CliExplainer;

internal sealed record ParsedArgs(
    string? FilePath,
    string? Command,
    string? Model,
    bool ListModels,
    bool Debug,
    string[]? SubprocessArgs);

internal static class ArgumentParser
{
    internal static ParsedArgs Parse(string[] args)
    {
        string? filePath = null;
        string? command = null;
        string? model = null;
        bool listModels = false;
        bool debug = false;
        string[]? subprocessArgs = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--":
                    var remaining = args[(i + 1)..];
                    if (remaining.Length == 0)
                        throw new ArgumentException(
                            "Expected a command after '--', but none was provided.");
                    subprocessArgs = remaining;
                    i = args.Length; // exit the for loop
                    break;
                case "-f" or "--file":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("Missing value for -f/--file");
                    filePath = args[++i];
                    break;
                case "-c" or "--command":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("Missing value for -c/--command");
                    command = args[++i];
                    break;
                case "-m" or "--model":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("Missing value for -m/--model");
                    model = args[++i];
                    break;
                case "--list-models":
                    listModels = true;
                    break;
                case "--debug":
                    debug = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return new ParsedArgs(filePath, command, model, listModels, debug, subprocessArgs);
    }
}
