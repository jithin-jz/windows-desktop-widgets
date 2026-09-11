@echo off
rem ---------------------------------------------------------------------------
rem Double-click installer.
rem
rem This exists so nobody has to type or paste a command. Pasting a PowerShell
rem one-liner loses its `irm` prefix and `| iex` suffix easily - especially when
rem the link is copied out of a chat app - and PowerShell then reports the URL
rem itself as an unknown command. A batch file has no such failure mode, and it
rem is also not subject to PowerShell's execution policy.
rem
rem Works two ways:
rem   - next to install.ps1 (extracted zip or a clone): runs the local copy
rem   - on its own: fetches install.ps1 from GitHub
rem
rem Arguments are passed straight through to install.ps1, so switches like
rem -NoStartup or -NoLaunch work here exactly as they do on the script.
rem ---------------------------------------------------------------------------

setlocal
cd /d "%~dp0"

if exist "%~dp0install.ps1" (
    echo Installing from this folder...
    echo.
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
) else (
    echo Fetching the installer from GitHub...
    echo.
    rem Tls12 is forced here too: this download happens before install.ps1 runs,
    rem so the guard inside that script has not taken effect yet. 3072 is Tls12.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object Net.WebClient).DownloadString('https://raw.githubusercontent.com/jithin-jz/windows-desktop-widgets/main/install.ps1'))"
)

echo.
echo ---------------------------------------------------------------------------
pause
