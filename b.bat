@echo off
@pushd %~dp0
set DOTNET_CLI_UI_LANGUAGE=en

@dotnet run --project ".\build\build.csproj" --no-launch-profile -- %*
@popd