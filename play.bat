@echo off
rem Zeus online - speelbare demo. Dubbelklik: bouwt en start het spel (menu -> wedstrijd / editor / meter).
cd /d "%~dp0"
set GODOT=%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe
if not exist "%GODOT%" (
  echo Godot 4.7.2 .NET niet gevonden op: %GODOT%
  pause
  exit /b 1
)
dotnet build godot\Zeus.Godot.csproj --nologo -v q
if errorlevel 1 (
  echo Bouwen mislukt.
  pause
  exit /b 1
)
start "" "%GODOT%" --path godot
