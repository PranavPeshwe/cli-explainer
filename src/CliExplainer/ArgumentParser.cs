namespace CliExplainer;

internal sealed record ParsedArgs(
    string? FilePath,
    string? Command,
    string? Model,
    bool ListModels,
    bool Debug);

internal static class ArgumentParser
{
    internal static ParsedArgs Parse(string[] args)
    {
        string? filePath = null;
        string? command = null;
        string? model = null;
        bool listModels = false;
        bool debug = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
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

        return new ParsedArgs(filePath, command, model, listModels, debug);
    }
}
