@echo off
setlocal

set MOD_DIR=E:\SteamGames\Steam\steamapps\common\RimWorld\Mods\TheMoat

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo dotnet not found. Install the .NET 10 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

rem Builds 1.6\Assemblies\TheMoat.dll and 1.7\Assemblies\TheMoat.dll, see TheMoat.csproj
dotnet build TheMoat.csproj -c Release -nologo -v quiet
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

if /I "%~dp0"=="%MOD_DIR%\" (
    echo Built in place.
    exit /b 0
)

rem Only the files the game needs: no git history, sources or build script
robocopy . "%MOD_DIR%" /MIR /NJH /NJS /NFL /NDL /NP /XD .git .vs .vscode .idea Source obj bin Screenshots /XF build.bat .gitignore TheMoat.csproj >nul
if errorlevel 8 (
    echo Could not copy the mod to %MOD_DIR%
    exit /b 1
)
echo Built and copied to %MOD_DIR%
