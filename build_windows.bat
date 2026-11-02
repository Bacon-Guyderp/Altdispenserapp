@echo off
REM Builds publish\N64AssetStudio.exe - a single self-contained file that runs on any
REM 64-bit Windows 10/11 PC with nothing else installed.
REM Building it (not running it) needs the free .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1 || (echo The .NET 8 SDK is required to build. Download it from https://dotnet.microsoft.com/download/dotnet/8.0 & exit /b 1)
dotnet test tests\N64AssetStudio.Tests -c Release || goto :error
dotnet publish src\N64AssetStudio -c Release -o publish || goto :error
echo.
echo Built publish\N64AssetStudio.exe
exit /b 0
:error
echo Build failed.
exit /b 1
