using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CliExplainer;

internal sealed record SubprocessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string CombinedOutput);

internal static class SubprocessRunner
{
    private static readonly char[] ShellChars = ['|', '>', '<', ';'];

    internal static bool RequiresShell(string command)
        => command.IndexOfAny(ShellChars) >= 0
           || command.Contains("&&")
           || command.Contains("||");

    internal static async Task<SubprocessResult> RunAsync(
        string[] subprocessArgs,
        CancellationToken cancellationToken = default,
        TextWriter? stdoutForward = null,
        TextWriter? stderrForward = null)
    {
        var executable = subprocessArgs[0];
        var arguments = subprocessArgs.Length > 1
            ? string.Join(' ', subprocessArgs[1..].Select(QuoteIfNeeded))
            : string.Empty;

        var fullCommand = string.IsNullOrEmpty(arguments)
            ? executable
            : $"{executable} {arguments}";

        using var process = new Process();

        if (RequiresShell(fullCommand))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{fullCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
            else
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{fullCommand.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
        }
        else
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var combinedBuilder = new StringBuilder();
        var lockObj = new object();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (lockObj)
            {
                stdoutBuilder.AppendLine(e.Data);
                combinedBuilder.AppendLine(e.Data);
                stdoutForward?.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (lockObj)
            {
                stderrBuilder.AppendLine(e.Data);
                combinedBuilder.AppendLine(e.Data);
                stderrForward?.WriteLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            var msg = $"Command not found: {executable}";
            return new SubprocessResult(
                ExitCode: 127,
                StandardOutput: "",
                StandardError: msg,
                CombinedOutput: msg);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new SubprocessResult(
            ExitCode: process.ExitCode,
            StandardOutput: stdoutBuilder.ToString(),
            StandardError: stderrBuilder.ToString(),
            CombinedOutput: combinedBuilder.ToString());
    }

    private static string QuoteIfNeeded(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
