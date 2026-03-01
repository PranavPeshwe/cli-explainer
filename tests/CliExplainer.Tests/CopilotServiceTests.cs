namespace CliExplainer.Tests;

public class CopilotServiceTests
{
    [Fact]
    public void SystemPrompt_ContainsRequiredSections()
    {
        Assert.Contains("Root Cause", CopilotService.SystemPrompt);
        Assert.Contains("Fix", CopilotService.SystemPrompt);
    }

    [Theory]
    [InlineData(new[] { "-c", "npm run build", "-f", "error.txt" },
                "npm run build", "error.txt", null, false, false)]
    [InlineData(new[] { "--command", "dotnet build", "--file", "log.txt", "--debug" },
                "dotnet build", "log.txt", null, false, true)]
    [InlineData(new[] { "-m", "gpt-5", "--list-models" },
                null, null, "gpt-5", true, false)]
    [InlineData(new[] { "-f", "err.log", "-c", "make", "-m", "claude-sonnet-4.5", "--debug" },
                "make", "err.log", "claude-sonnet-4.5", false, true)]
    public void ArgumentParsing_ExtractsValues(
        string[] args,
        string? expectedCommand,
        string? expectedFile,
        string? expectedModel,
        bool expectedListModels,
        bool expectedDebug)
    {
        var parsed = ArgumentParser.Parse(args);

        Assert.Equal(expectedCommand, parsed.Command);
        Assert.Equal(expectedFile, parsed.FilePath);
        Assert.Equal(expectedModel, parsed.Model);
        Assert.Equal(expectedListModels, parsed.ListModels);
        Assert.Equal(expectedDebug, parsed.Debug);
        Assert.Null(parsed.SubprocessArgs);
    }

    [Fact]
    public void ArgumentParsing_ThrowsOnMissingValue()
    {
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(new[] { "-f" }));
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(new[] { "-c" }));
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(new[] { "-m" }));
    }

    [Fact]
    public void ArgumentParsing_ThrowsOnUnknownArg()
    {
        Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(new[] { "--unknown" }));
    }

    // --- Subprocess argument parsing tests ---

    [Fact]
    public void ArgumentParsing_DoubleDash_CapturesSubprocessArgs()
    {
        var parsed = ArgumentParser.Parse(
            new[] { "--debug", "--", "dotnet", "build", "--no-restore" });

        Assert.True(parsed.Debug);
        Assert.NotNull(parsed.SubprocessArgs);
        Assert.Equal(new[] { "dotnet", "build", "--no-restore" }, parsed.SubprocessArgs);
        Assert.Null(parsed.Command);
        Assert.Null(parsed.FilePath);
    }

    [Fact]
    public void ArgumentParsing_DoubleDash_WithExplicitCommand()
    {
        var parsed = ArgumentParser.Parse(
            new[] { "-c", "my custom label", "--", "npm", "install" });

        Assert.Equal("my custom label", parsed.Command);
        Assert.Equal(new[] { "npm", "install" }, parsed.SubprocessArgs);
    }

    [Fact]
    public void ArgumentParsing_DoubleDash_Empty_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ArgumentParser.Parse(new[] { "--" }));
        Assert.Contains("command after '--'", ex.Message);
    }

    [Fact]
    public void ArgumentParsing_DoubleDash_StopsParsingFlags()
    {
        var parsed = ArgumentParser.Parse(
            new[] { "--", "myapp", "--debug" });

        Assert.False(parsed.Debug);
        Assert.Equal(new[] { "myapp", "--debug" }, parsed.SubprocessArgs);
    }

    // --- SubprocessRunner tests ---

    [Fact]
    public async Task SubprocessRunner_SuccessfulCommand_ReturnsZeroExitCode()
    {
        var result = await SubprocessRunner.RunAsync(
            new[] { "dotnet", "--version" });

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    [Fact]
    public async Task SubprocessRunner_FailingCommand_ReturnsNonZeroExitCode()
    {
        var result = await SubprocessRunner.RunAsync(
            new[] { "dotnet", "build", "nonexistent_project_xyz.csproj" });

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.CombinedOutput));
    }

    [Fact]
    public async Task SubprocessRunner_CommandNotFound_Returns127()
    {
        var result = await SubprocessRunner.RunAsync(
            new[] { "this_command_definitely_does_not_exist_12345" });

        Assert.Equal(127, result.ExitCode);
        Assert.Contains("not found", result.StandardError,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubprocessRunner_CapturesBothStreams()
    {
        var result = await SubprocessRunner.RunAsync(
            new[] { "dotnet", "--info" });

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.CombinedOutput));
    }

    // --- Integration tests ---

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CopilotService_ListModels()
    {
        var models = await CopilotService.GetAvailableModelsAsync();

        Assert.NotNull(models);
        Assert.NotEmpty(models);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CopilotService_StreamResponse()
    {
        await using var service = new CopilotService();
        var chunks = new List<string>();

        await service.StartAnalysisAsync(
            errorText: "'npm' is not recognized as an internal or external command",
            commandText: "npm install",
            onChunk: chunk => chunks.Add(chunk),
            onDebug: _ => { });

        Assert.True(chunks.Count > 1,
            $"Expected multiple streaming chunks, got {chunks.Count}");

        var fullResponse = string.Join("", chunks);
        Assert.False(string.IsNullOrWhiteSpace(fullResponse),
            "Response should not be empty");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CopilotService_MaintainsContext()
    {
        await using var service = new CopilotService();
        var initialChunks = new List<string>();
        var followUpChunks = new List<string>();

        await service.StartAnalysisAsync(
            errorText: "'npm' is not recognized as an internal or external command",
            commandText: "npm install",
            onChunk: chunk => initialChunks.Add(chunk),
            onDebug: _ => { });

        Assert.NotEmpty(initialChunks);

        await service.AskFollowUpAsync(
            question: "What if I use yarn instead?",
            onChunk: chunk => followUpChunks.Add(chunk),
            onDebug: _ => { });

        Assert.NotEmpty(followUpChunks);

        var followUpResponse = string.Join("", followUpChunks);
        Assert.False(string.IsNullOrWhiteSpace(followUpResponse),
            "Follow-up response should not be empty");
    }
}
