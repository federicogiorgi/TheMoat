@echo off
setlocal
echo ========================================================
echo Building 'The Moat' RimWorld Mod...
echo ========================================================

set MOD_DIR=E:\SteamGames\Steam\steamapps\common\RimWorld\Mods\TheMoat

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet not found. Install the .NET 10 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

rem Builds 1.6\Assemblies\TheMoat.dll and 1.7\Assemblies\TheMoat.dll, see TheMoat.csproj
dotnet build TheMoat.csproj -c Release -nologo -v quiet

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Build succeeded! Copying to the RimWorld game directory...
    if /I "%~dp0"=="%MOD_DIR%\" (
        echo [INFO] Building inside RimWorld\Mods\TheMoat, no copy needed.
    ) else (
        rem Only the files the game needs: no git history, sources or build script
        robocopy . "%MOD_DIR%" /MIR /NJH /NJS /NFL /NDL /NP /XD .git .vs .vscode .idea Source obj bin /XF build.bat .gitignore TheMoat.csproj >nul
        if errorlevel 8 (
            echo [FAIL] Could not copy the mod to %MOD_DIR%
        ) else (
            echo [SUCCESS] Synchronized with RimWorld\Mods\TheMoat!
        )
    )
) else (
    echo [FAIL] Build failed with errors.
)

echo ========================================================
