@echo off
setlocal
echo ========================================================
echo Building 'The Moat' RimWorld Mod...
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set MANAGED=E:\SteamGames\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed
set HARMONY=E:\SteamGames\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll
set OUT_DLL=Assemblies\TheMoat.dll
set MOD_DIR=E:\SteamGames\Steam\steamapps\common\RimWorld\Mods\TheMoat

cd /d "%~dp0"

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at %CSC%
    pause
    exit /b 1
)

"%CSC%" /target:library /out:%OUT_DLL% /r:"%MANAGED%\netstandard.dll" /r:"%MANAGED%\Assembly-CSharp.dll" /r:"%MANAGED%\UnityEngine.CoreModule.dll" /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" /r:"%MANAGED%\UnityEngine.TextRenderingModule.dll" /r:"%MANAGED%\UnityEngine.dll" /r:"%MANAGED%\Unity.Mathematics.dll" /r:"%HARMONY%" Source\*.cs

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Build succeeded! Copying to version assemblies and RimWorld game directory...
    copy /Y %OUT_DLL% 1.6\Assemblies\TheMoat.dll >nul
    if /I "%~dp0"=="%MOD_DIR%\" (
        echo [INFO] Building inside RimWorld\Mods\TheMoat, no copy needed.
    ) else if exist "%MOD_DIR%" (
        xcopy /Y /S /I /Q . "%MOD_DIR%\" >nul
        echo [SUCCESS] Synchronized with RimWorld\Mods\TheMoat!
    )
) else (
    echo [FAIL] Build failed with errors.
)

echo ========================================================
