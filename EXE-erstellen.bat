@echo off
setlocal
cd /d "%~dp0"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
echo.
echo  BrightnessTray - EXE erstellen
echo  ------------------------------
echo  [1] Klein (ca. 1 MB)   - braucht das .NET 8 Desktop Runtime (ist bei dir durch das SDK schon da)
echo  [2] Eigenstaendig      - laeuft auf jedem Windows 10/11 ohne Installation (ca. 60-70 MB)
echo.
choice /c 12 /n /m "Auswahl (1 oder 2): "
if errorlevel 2 (set SC=true) else (set SC=false)

dotnet publish -c Release -r win-x64 --self-contained %SC% -o "%~dp0EXE" ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=%SC% -p:DebugType=none -p:DebugSymbols=false
if errorlevel 1 (
  echo.
  echo  FEHLER beim Erstellen. Bitte die Meldung oben kopieren.
  pause
  exit /b 1
)
echo.
echo  Fertig: %~dp0EXE\BrightnessTray.exe
echo  Die EXE kannst du verschieben, wohin du willst (z. B. nach C:\Programme\BrightnessTray).
explorer "%~dp0EXE"
pause
