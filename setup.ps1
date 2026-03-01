<#
.SYNOPSIS
    Checks that all prerequisites for building and running CLI Explainer are present.

.DESCRIPTION
    Verifies the presence and version of:
      - .NET SDK (8.0 or later required)
      - GitHub Copilot CLI (required for running the app and integration tests)
      - Git (optional, for version control)
    Reports pass/fail status for each check and provides install guidance for
    anything that is missing.

.EXAMPLE
    .\setup.ps1
#>

$script:AllPassed = $true

function Write-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail,
        [string]$FixHint
    )
    if ($Passed) {
        Write-Host "  [PASS] $Name" -ForegroundColor Green
        if ($Detail) { Write-Host "         $Detail" -ForegroundColor DarkGray }
    } else {
        Write-Host "  [FAIL] $Name" -ForegroundColor Red
        if ($Detail) { Write-Host "         $Detail" -ForegroundColor Yellow }
        if ($FixHint) { Write-Host "         Fix: $FixHint" -ForegroundColor Yellow }
        $script:AllPassed = $false
    }
}

Write-Host ""
Write-Host "CLI Explainer - Build Environment Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- .NET SDK ---
Write-Host "Checking .NET SDK..." -ForegroundColor White
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $dotnetVersion = & dotnet --version 2>$null
    $major = ($dotnetVersion -split '\.')[0]
    if ([int]$major -ge 8) {
        Write-Check ".NET SDK installed" $true "Version: $dotnetVersion"
    } else {
        Write-Check ".NET SDK version" $false `
            "Found version $dotnetVersion, but 8.0 or later is required." `
            "Download from https://dotnet.microsoft.com/download/dotnet/8.0"
    }

    # Check that the SDK can target net8.0 (SDK >= 8.0 can target net8.0)
    if ([int]$major -ge 8) {
        Write-Check "net8.0 targeting support" $true "SDK $dotnetVersion can target net8.0"
    } else {
        Write-Check "net8.0 targeting support" $false `
            "SDK $dotnetVersion cannot target net8.0. Version 8.0 or later is required." `
            "Install from https://dotnet.microsoft.com/download/dotnet/8.0"
    }
} else {
    Write-Check ".NET SDK installed" $false `
        "dotnet command not found." `
        "Install from https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host ""

# --- GitHub Copilot CLI ---
Write-Host "Checking GitHub Copilot CLI..." -ForegroundColor White
$copilotCmd = Get-Command copilot -ErrorAction SilentlyContinue
if (-not $copilotCmd) {
    $copilotCmd = Get-Command github-copilot-cli -ErrorAction SilentlyContinue
}
if ($copilotCmd) {
    $copilotPath = $copilotCmd.Source
    Write-Check "GitHub Copilot CLI installed" $true "Path: $copilotPath"
} else {
    # The SDK downloads its own bundled CLI at build time, so this is a soft check
    Write-Check "GitHub Copilot CLI" $false `
        "copilot command not found in PATH. The SDK bundles its own CLI at build time, but authentication may still require the CLI." `
        "See https://docs.github.com/en/copilot/github-copilot-in-the-cli"
}

# Check GitHub authentication via gh CLI
$ghCmd = Get-Command gh -ErrorAction SilentlyContinue
if ($ghCmd) {
    $authStatus = & gh auth status 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Check "GitHub CLI authenticated" $true "gh auth status: OK"
    } else {
        Write-Check "GitHub CLI authenticated" $false `
            "gh is installed but not authenticated." `
            "Run: gh auth login"
    }
} else {
    Write-Check "GitHub CLI (gh)" $false `
        "gh command not found. Needed for Copilot authentication." `
        "Install from https://cli.github.com/"
}

Write-Host ""

# --- Git ---
Write-Host "Checking Git..." -ForegroundColor White
$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if ($gitCmd) {
    $gitVersion = & git --version 2>$null
    Write-Check "Git installed" $true $gitVersion
} else {
    Write-Check "Git installed" $false `
        "git command not found (optional, for version control)." `
        "Install from https://git-scm.com/downloads"
}

Write-Host ""

# --- NuGet restore check ---
Write-Host "Checking NuGet package restore..." -ForegroundColor White
if ($dotnetCmd) {
    $restoreResult = & dotnet restore CliExplainer.sln 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Check "NuGet restore" $true "All packages restored successfully"
    } else {
        Write-Check "NuGet restore" $false `
            "Package restore failed. Check your network connection and NuGet sources." `
            "Run: dotnet restore CliExplainer.sln --verbosity detailed"
    }
} else {
    Write-Check "NuGet restore" $false "Skipped (.NET SDK not found)"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($script:AllPassed) {
    Write-Host "All checks passed. You are ready to build." -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor White
    Write-Host "  .\build.ps1 debug       Build in Debug mode"
    Write-Host "  .\build.ps1 test        Run all tests"
    Write-Host "  .\build.ps1 publish     Publish single-file executable"
    exit 0
} else {
    Write-Host "Some checks failed. Please resolve the issues above." -ForegroundColor Red
    exit 1
}
