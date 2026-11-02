@echo off
set "GAME=D:\Program Files (x86)\Steam\steamapps\common\Shelldiver"
if not exist "%GAME%\mods" (
    echo BubbleLib is not installed yet. Install it first, then run this again.
    pause
    exit /b 1
)
dotnet build "%~dp0NewGamePlusMod.csproj" -c Release
if errorlevel 1 (
    pause
    exit /b 1
)
if not exist "%GAME%\mods\bubble" mkdir "%GAME%\mods\bubble"
copy /y "%~dp0bin\Release\net9.0\NewGamePlusMod.dll" "%GAME%\mods\bubble\"
pause
