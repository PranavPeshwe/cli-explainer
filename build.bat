@echo off
setlocal enabledelayedexpansion

set SOLUTION=CliExplainer.slnx
set PROJECT=src\CliExplainer\CliExplainer.csproj

if "%~1"=="" goto usage
if /i "%~1"=="debug"   goto build_debug
if /i "%~1"=="release" goto build_release
if /i "%~1"=="test"    goto test
if /i "%~1"=="testunit" goto test_unit
if /i "%~1"=="testint" goto test_integration
if /i "%~1"=="publish" goto publish
if /i "%~1"=="clean"   goto clean
goto usage

:build_debug
echo Building in Debug mode...
dotnet build %SOLUTION% -c Debug
exit /b %errorlevel%

:build_release
echo Building in Release mode...
dotnet build %SOLUTION% -c Release
exit /b %errorlevel%

:test
echo Running all tests...
dotnet test %SOLUTION% --verbosity normal
exit /b %errorlevel%

:test_unit
echo Running unit tests only...
dotnet test %SOLUTION% --filter "Category!=Integration" --verbosity normal
exit /b %errorlevel%

:test_integration
echo Running integration tests only...
dotnet test %SOLUTION% --filter "Category=Integration" --verbosity normal
exit /b %errorlevel%

:publish
set RID=%~2
if "%RID%"=="" set RID=win-x64
echo Publishing single-file executable for %RID%...
dotnet publish %PROJECT% -c Release -r %RID%
exit /b %errorlevel%

:clean
echo Cleaning build outputs...
dotnet clean %SOLUTION%
exit /b %errorlevel%

:usage
echo Usage: build.bat ^<command^> [options]
echo.
echo Commands:
echo   debug       Build the solution in Debug mode
echo   release     Build the solution in Release mode
echo   test        Run all tests (unit + integration)
echo   testunit    Run unit tests only
echo   testint     Run integration tests only
echo   publish     Publish single-file executable (default: win-x64)
echo   clean       Clean build outputs
echo.
echo Examples:
echo   build.bat debug
echo   build.bat release
echo   build.bat test
echo   build.bat testunit
echo   build.bat publish linux-x64
echo   build.bat publish osx-arm64
exit /b 1
