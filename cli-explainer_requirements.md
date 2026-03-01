# Product Requirements Document: CLI Explainer

## 1. Product Overview
**Name:** CLI Explainer
**Description:** A lightweight, single-executable .NET 8 console application that explains terminal/CLI errors in plain English and provides actionable fixes. It intercepts error output (stderr) via pipeline or reads from a file, and sends it to an LLM using the GitHub Copilot SDK. After providing the initial diagnosis, it enters an interactive prompt loop allowing the user to ask follow-up questions.
**Target Audience:** Developers, sysadmins, and DevOps engineers using Windows, Linux, or macOS.
**Tech Stack:** .NET 8 (C#), Console Application (net8.0), GitHub.Copilot.SDK (v0.1.29, technical preview), xUnit (for testing).

## 2. Core Features & Capabilities

### 2.1 Command Line Interface (CLI)
Must operate strictly as a command-line tool. No GUI components should be included.
- **Input Sources:**
  - Must read from `Console.In` if data is piped (e.g., `dotnet run 2>&1 | cli-explainer.exe`).
  - `-f`, `--file <path>`: Read error text from a file.
- **Argument Support:**
  - `-c`, `--command <text>`: Optionally provide the command that caused the error to give the LLM better context.
  - `-m`, `--model <name>`: Override the default AI model.
  - `--list-models`: List all available models retrieved from the Copilot SDK and exit.
  - `--debug`: Print internal Copilot SDK diagnostic events to stderr.
- **Output:** Streams the LLM response directly to `Console.Out` in real-time.

### 2.2 Interactive REPL Loop
After the initial error analysis is streamed to the console, the application must not exit immediately.
- **Prompt Mode:** Present an interactive prompt (e.g., `> ` or `Ask follow-up: `).
- **Session Continuity:** The user can type follow-up questions (e.g., "What if I use yarn instead of npm?" or "Explain the second point more").
- **Streaming Repl:** Stream the LLM's response to the follow-up question to the console, then await the next input.
- **Exit Condition:** Typing `exit`, `quit`, or pressing Ctrl+C gracefully terminates the session.
- **Piped Input Behavior:** When error text is read from piped stdin (`Console.IsInputRedirected == true`), stdin is at EOF after reading. The REPL loop must be skipped in this case, since `Console.ReadLine()` would return `null` immediately. The REPL is only available when error text is provided via `-f`/`--file`.

### 2.3 LLM Integration (GitHub Copilot SDK)
Authentication and LLM inference must be handled entirely by the `GitHub.Copilot.SDK` NuGet package.
- **Initialization:** Use `CopilotClientOptions { AutoStart = true, UseStdio = true }`.
- **Session Creation:** `CreateSessionAsync` requires an `OnPermissionRequest` handler. Use `PermissionHandler.ApproveAll` to auto-approve all permission requests.
- **System Prompt:** Must be strictly defined to enforce the output format. Use `SystemMessageConfig { Content = prompt }` in `SessionConfig`.
  - *Draft Prompt:* "You are an expert sysadmin and developer. Analyze the command the user attempted (if provided) and the error output it produced. Output a structured response with exactly two sections: 1) **Root Cause:** A concise, plain-English explanation of why it failed. 2) **Fix:** The exact terminal command or specific code change needed to fix it. Do not include introductory/outro fluff."
- **Interactive Session:** The SDK session must be kept alive to maintain conversation history for the REPL loop.
- **Streaming:** Responses must be streamed chunk-by-chunk to the CLI output using `AssistantMessageDeltaEvent`. Access streamed text via `delta.Data.DeltaContent`. Enable streaming by setting `SessionConfig.Streaming = true`. Wait for `SessionIdleEvent` to detect response completion.
- **Default Model:** `claude-3.7-sonnet` (or the latest available Claude model supported by the SDK, falling back to `claude-opus-4.6`). Set via `SessionConfig.Model`.

## 3. Architecture & Code Structure

### 3.1 Project Setup
- **Solution:** `CliExplainer.sln`
- **Main Project:** `CliExplainer.csproj`
  - `<TargetFramework>net8.0</TargetFramework>`
  - `<OutputType>Exe</OutputType>`
- **Test Project:** `CliExplainer.Tests.csproj` (xUnit)

### 3.2 Key Classes
1. `Program.cs`: Entry point. Uses top-level statements. Delegates argument parsing to `ArgumentParser`, handles input reading (pipe vs file), initializes the Copilot service, and manages the interactive REPL loop.
2. `ArgumentParser.cs`: Extracts CLI arguments into a `ParsedArgs` record. Separated from `Program.cs` to enable unit testing of argument parsing without process-level invocation.
3. `CopilotService.cs`: Encapsulates all GitHub Copilot SDK logic.
   - Manages stateful `Session` to allow follow-up questions.
   - `public static async Task<List<string>> GetAvailableModelsAsync()`
   - `public async Task StartAnalysisAsync(string errorText, string? commandText, Action<string> onChunk, Action<string> onDebug)`
   - `public async Task AskFollowUpAsync(string question, Action<string> onChunk, Action<string> onDebug)`

## 4. Testing Requirements

Implementation must include a suite of xUnit tests covering core logic without mocking the actual LLM network calls (use integration-style testing with real SDK).

### 4.1 Required Test Cases
1. `SystemPrompt_ContainsRequiredSections`: Asserts the hardcoded system prompt demands "Root Cause" and "Fix".
2. `ArgumentParsing_ExtractsValues`: Verifies logic that extracts `-c "npm run"` and `-f "error.txt"`.
3. `CopilotService_ListModels`: Real SDK call to ensure models are returned.
4. `CopilotService_StreamResponse`: Real SDK call sending a dummy error ("'npm' is not recognized"). Asserts that multiple chunks are received via the callback action.
5. `CopilotService_MaintainsContext`: Tests sending an initial query, waiting for completion, and then sending a follow-up query to verify the session remains active.

## 5. Deployment / CI Requirements
- The app must publish cleanly as a single-file executable (`PublishSingleFile=true`, `SelfContained=true`).
- Must handle graceful degradation if the user does not have the Copilot CLI installed (catch initialization exceptions and show a stderr message instructing them to install prerequisites).
- **CI Pipeline:** GitHub Actions workflow (`.github/workflows/ci.yml`) triggers on every push:
  - Builds the solution and runs unit tests.
  - Publishes single-file executables for `win-x64`, `linux-x64`, and `osx-arm64` (uploaded as build artifacts).
  - On pushes to `main`/`master`, creates a GitHub Release with all platform executables as zip archives.

## 6. Implementation Constraints & Guidelines
- **Strictly CLI:** No `UseWindowsForms`, no UI threads, no P/Invoke for console hiding.
- **No API Keys:** Never request, hardcode, or store GitHub tokens. Rely entirely on the ambient authentication provided by the Copilot SDK CLI bridge.
- **Impersonal Output:** The LLM's system prompt must forbid conversational filler like "Here is the explanation...".
- **C# 12 / .NET 8 Features:** Utilize raw string literals (`"""`) for the system prompt, top-level statements, and nullable reference types (`<Nullable>enable</Nullable>`).