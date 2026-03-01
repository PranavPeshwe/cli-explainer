# CLI Explainer

A lightweight .NET 8 console application that explains terminal/CLI errors in plain English and provides actionable fixes. It sends error output to an LLM via the GitHub Copilot SDK, then enters an interactive prompt loop for follow-up questions.

## Installation

### Windows (via winget)

```
winget install PranavPeshwe.CliExplainer
```

This installs `cli-explainer` as a portable executable and adds it to your PATH.

### Manual Download

Download the latest release for your platform from the [Releases page](https://github.com/PranavPeshwe/cli-explainer/releases):

| Platform | Archive |
|---|---|
| Windows x64 | `cli-explainer-win-x64.zip` |
| Linux x64 | `cli-explainer-linux-x64.zip` |
| macOS ARM64 | `cli-explainer-osx-arm64.zip` |

Extract the archive and place the executable in a directory on your PATH.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [GitHub Copilot CLI](https://docs.github.com/en/copilot/github-copilot-in-the-cli) installed and authenticated

## Usage

CLI Explainer reads error text from piped stdin, a file, or by running a command directly. It analyzes the error with an LLM and streams a structured diagnosis with a **Root Cause** and **Fix**.

```
cli-explainer [options]
cli-explainer [options] -- <command> [command-args...]
```

### Options

| Flag | Description |
|---|---|
| `-f`, `--file <path>` | Read error text from a file |
| `-c`, `--command <text>` | Provide the command that caused the error (gives the LLM better context) |
| `-m`, `--model <name>` | Override the default AI model |
| `--list-models` | List all available models and exit |
| `--debug` | Print internal Copilot SDK diagnostic events to stderr |
| `-- <command>` | Execute `<command>` as a subprocess; on failure, analyze its output |

### Input Sources

- **Piped input**: `some_command 2>&1 | cli-explainer`
- **File input**: `cli-explainer -f error.log`
- **Subprocess execution**: `cli-explainer -- dotnet build`

When input is piped, the tool prints the analysis and exits. When input is read from a file or a subprocess fails, an interactive REPL starts after the initial analysis, allowing follow-up questions. In subprocess mode, if the command succeeds (exit code 0), nothing is printed. Type `exit`, `quit`, or press `Ctrl+C` to end the session.

## Examples

Pipe a failing command's output directly:

```bash
dotnet build 2>&1 | cli-explainer -c "dotnet build"
```

Read error output from a file with a command for context:

```bash
cli-explainer -f build-errors.log -c "npm install"
```

Use a specific model:

```bash
cli-explainer -f error.txt -m gpt-5
```

List available models:

```bash
cli-explainer --list-models
```

Run a command and analyze errors automatically:

```bash
cli-explainer -- dotnet build
cli-explainer --debug -- npm install
cli-explainer -m gpt-5 -- dotnet test CliExplainer.sln --filter "Category!=Integration"
```

Everything after `--` is passed to the subprocess. Flags like `--debug` or `-m` must appear before `--`.

## Building

```bash
# Restore and build
dotnet build CliExplainer.sln

# Publish as a single-file executable (Windows x64)
dotnet publish src/CliExplainer -c Release -r win-x64

# Publish for Linux x64
dotnet publish src/CliExplainer -c Release -r linux-x64

# Publish for macOS ARM
dotnet publish src/CliExplainer -c Release -r osx-arm64
```

The published binary will be in `src/CliExplainer/bin/Release/net8.0/<runtime>/publish/`.

## Running Tests

```bash
# Run all tests (requires Copilot CLI installed and authenticated)
dotnet test CliExplainer.sln

# Run only unit tests (no Copilot CLI needed)
dotnet test CliExplainer.sln --filter "Category!=Integration"

# Run only integration tests
dotnet test CliExplainer.sln --filter "Category=Integration"
```

## Project Structure

```
CliExplainer.sln                  Solution file
src/CliExplainer/
  CliExplainer.csproj              Main project
  Program.cs                       Entry point, input reading, REPL loop
  CopilotService.cs                GitHub Copilot SDK wrapper
  ArgumentParser.cs                CLI argument parsing
  SubprocessRunner.cs              Subprocess execution and output capture
tests/CliExplainer.Tests/
  CliExplainer.Tests.csproj        Test project (xUnit)
  CopilotServiceTests.cs           Unit and integration tests
```
