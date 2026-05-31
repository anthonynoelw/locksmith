@echo off
setlocal enabledelayedexpansion
set "HOOK_DIR=.husky"
set "COMMIT_MSG_HOOK=.husky\commit-msg"

echo Checking prerequisites...

node --version >nul 2>&1
if %errorlevel% neq 0 (
  echo Error: Node.js is not installed. Install from https://nodejs.org/
  exit /b 1
)
for /f "tokens=*" %%v in ('node --version') do echo   Node.js %%v

dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
  echo Error: .NET SDK is not installed. Install from https://dot.net/
  exit /b 1
)
for /f "tokens=*" %%v in ('dotnet --version') do echo   .NET    %%v

echo.
echo Installing npm dependencies...
call npm install
if %errorlevel% neq 0 (
  echo Error: npm install failed.
  exit /b 1
)

echo.
echo Configuring git hooks...
if not exist "%HOOK_DIR%" mkdir "%HOOK_DIR%"
if not exist "%COMMIT_MSG_HOOK%" (
  echo npx --no -- commitlint --edit "%%1"> "%COMMIT_MSG_HOOK%"
  echo   Created %COMMIT_MSG_HOOK%
) else (
  echo   %COMMIT_MSG_HOOK% already exists
)

echo.
echo Restoring .NET packages...
dotnet restore
if %errorlevel% neq 0 (
  echo Error: dotnet restore failed.
  exit /b 1
)

echo.
echo Setup complete.
endlocal

