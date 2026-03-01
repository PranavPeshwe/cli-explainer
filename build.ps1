<#
.SYNOPSIS
    Build, test, and publish the CLI Explainer project.

.DESCRIPTION
    Provides commands to build in Debug/Release mode, run tests, publish
    single-file executables, and clean build outputs.

.PARAMETER Command
    The action to perform: debug, release, test, testunit, testint, publish, clean.

.PARAMETER Runtime
    Runtime identifier for the publish command (default: win-x64).
    Examples: win-x64, linux-x64, osx-arm64.

.EXAMPLE
    .\build.ps1 debug
    .\build.ps1 release
    .\build.ps1 test
    .\build.ps1 testunit
    .\build.ps1 publish linux-x64
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("debug", "release", "test", "testunit", "testint", "publish", "clean")]
    [string]$Command,

    [Parameter(Position = 1)]
    [string]$Runtime = "win-x64"
)

$Solution = "CliExplainer.slnx"
$Project  = "src/CliExplainer/CliExplainer.csproj"

function Show-Usage {
    Write-Host "Usage: .\build.ps1 <command> [runtime]" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  debug       Build the solution in Debug mode"
    Write-Host "  release     Build the solution in Release mode"
    Write-Host "  test        Run all tests (unit + integration)"
    Write-Host "  testunit    Run unit tests only"
    Write-Host "  testint     Run integration tests only"
    Write-Host "  publish     Publish single-file executable (default: win-x64)"
    Write-Host "  clean       Clean build outputs"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\build.ps1 debug"
    Write-Host "  .\build.ps1 release"
    Write-Host "  .\build.ps1 test"
    Write-Host "  .\build.ps1 publish linux-x64"
    Write-Host "  .\build.ps1 publish osx-arm64"
}

if (-not $Command) {
    Show-Usage
    exit 1
}

switch ($Command) {
    "debug" {
        Write-Host "Building in Debug mode..." -ForegroundColor Green
        dotnet build $Solution -c Debug
    }
    "release" {
        Write-Host "Building in Release mode..." -ForegroundColor Green
        dotnet build $Solution -c Release
    }
    "test" {
        Write-Host "Running all tests..." -ForegroundColor Green
        dotnet test $Solution --verbosity normal
    }
    "testunit" {
        Write-Host "Running unit tests only..." -ForegroundColor Green
        dotnet test $Solution --filter "Category!=Integration" --verbosity normal
    }
    "testint" {
        Write-Host "Running integration tests only..." -ForegroundColor Green
        dotnet test $Solution --filter "Category=Integration" --verbosity normal
    }
    "publish" {
        Write-Host "Publishing single-file executable for $Runtime..." -ForegroundColor Green
        dotnet publish $Project -c Release -r $Runtime
    }
    "clean" {
        Write-Host "Cleaning build outputs..." -ForegroundColor Green
        dotnet clean $Solution
    }
}

exit $LASTEXITCODE
