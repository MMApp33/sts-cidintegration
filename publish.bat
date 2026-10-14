@echo off
setlocal
cd /d "%~dp0"

echo.
echo === Publishing app ===
if exist "artifacts\publish" rmdir /s /q "artifacts\publish"
mkdir "artifacts\publish" 2>nul

dotnet publish "STS.CID.Easy\STS.CID.Easy.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "artifacts\publish"

if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo.
echo === Building Setup.exe ===

set "ISCC="
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if "%ISCC%"=="" (
  echo.
  echo Inno Setup 6 is not installed.
  echo Install it from https://jrsoftware.org/isdl.php  ^(free^)
  echo Then run this file again, or open installer\ScanToServe.CidEasy.iss and click Compile.
  exit /b 1
)

"%ISCC%" "installer\ScanToServe.CidEasy.iss"
if errorlevel 1 (
  echo Setup compile failed.
  exit /b 1
)

echo.
echo Done.
echo   artifacts\ScanToServe-CID-Easy-Setup.exe
echo.
echo Upload that Setup.exe to a GitHub Release and give clients the download URL.
echo They double-click it and install like any other Windows app.
exit /b 0
