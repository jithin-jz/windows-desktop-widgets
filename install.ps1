<#
    Installs the desktop widgets.

    One-liner (no clone needed):
        irm https://raw.githubusercontent.com/jithin-jz/windows-desktop-widgets/main/install.ps1 | iex

    From a clone:
        .\install.ps1

    No admin rights, no .NET SDK and no NuGet packages are required. Everything
    it compiles against ships with Windows.
#>
[CmdletBinding()]
param(
    [string]$Repo = 'jithin-jz/windows-desktop-widgets',
    [string]$Branch = 'main',
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'DesktopWidgets'),
    [switch]$NoStartup,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 can still default to TLS 1.0, which GitHub refuses.
# Without this the download step fails with an unhelpful connection error.
try {
    [Net.ServicePointManager]::SecurityProtocol =
        [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
}
catch { }

function Say ($m) { Write-Host $m }
function Good ($m) { Write-Host "  OK    $m" -ForegroundColor Green }
function Warn ($m) { Write-Host "  WARN  $m" -ForegroundColor Yellow }
function Die ($m) { Write-Host "  FAIL  $m" -ForegroundColor Red; exit 1 }

Say ''
Say 'Desktop Widgets - installer'
Say '==========================='

# ---------------------------------------------------------------- requirements
# The whole build depends on the C# compiler that ships inside Windows. It has
# been present since .NET Framework 4, so this check really only guards against
# a stripped-down image or a machine where the feature was turned off.
$csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    Die ".NET Framework 4.x compiler not found at $csc. Enable .NET Framework 4.8 in 'Turn Windows features on or off', then re-run."
}
Good 'in-box C# compiler found'

if ([Environment]::OSVersion.Version.Major -lt 10) {
    Warn 'Windows 10 or later is recommended; the media widget uses WinRT APIs older builds lack.'
}
else {
    Good "Windows build $([Environment]::OSVersion.Version.Build)"
}

# ------------------------------------------------------------------ get source
# Works both from a clone and from the piped one-liner, where $PSScriptRoot is
# empty because there is no script file on disk.
$srcRoot = $null
if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot 'src\WidgetApp.cs'))) {
    $srcRoot = $PSScriptRoot
    Good "installing from local copy: $srcRoot"
}
else {
    Say ''
    Say "  Downloading $Repo ($Branch) ..."
    $tmp = Join-Path $env:TEMP ('dw-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $tmp | Out-Null
    $zip = Join-Path $tmp 'src.zip'
    try {
        Invoke-WebRequest -Uri "https://github.com/$Repo/archive/refs/heads/$Branch.zip" -OutFile $zip -UseBasicParsing
    }
    catch {
        Die "download failed: $($_.Exception.Message)"
    }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $srcRoot = (Get-ChildItem $tmp -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'src\WidgetApp.cs') } |
        Select-Object -First 1).FullName
    if (-not $srcRoot) { Die 'downloaded archive did not contain src\WidgetApp.cs' }
    Good 'source downloaded'
}

# ---------------------------------------------------------------- stop running
$running = Get-Process DesktopWidgets -ErrorAction SilentlyContinue
if ($running) {
    # The build overwrites the exe, which fails while it is loaded.
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 700
    Good 'stopped the running instance'
}

# ------------------------------------------------------------------ copy files
New-Item -ItemType Directory -Force $InstallDir | Out-Null
foreach ($item in 'src', 'launcher', 'fonts', 'build.cmd', 'check-font.ps1',
    'install.ps1', 'install.cmd', 'uninstall.ps1', 'README.md', 'LICENSE') {
    $from = Join-Path $srcRoot $item
    if (Test-Path $from) { Copy-Item $from -Destination $InstallDir -Recurse -Force }
}
Good "files copied to $InstallDir"

# ----------------------------------------------------------------------- build
Say ''
Say '  Building ...'
$build = Join-Path $InstallDir 'build.cmd'
& cmd.exe /c "`"$build`"" 2>&1 | ForEach-Object { Write-Host "    $_" }
if ($LASTEXITCODE -ne 0) { Die 'build failed (output above)' }
$exe = Join-Path $InstallDir 'bin\DesktopWidgets.exe'
if (-not (Test-Path $exe)) { Die 'build reported success but produced no exe' }
Good ('built DesktopWidgets.exe ({0:N0} KB)' -f ((Get-Item $exe).Length / 1KB))

# --------------------------------------------------------------------- weather
# Optional. The weather card shows a hint instead of data until this exists.
$stateDir = Join-Path $env:LOCALAPPDATA 'KiroDesktopWidgets'
New-Item -ItemType Directory -Force $stateDir | Out-Null
$weatherPath = Join-Path $stateDir 'weather.json'
if (Test-Path $weatherPath) {
    Good 'weather already configured'
}
else {
    Say ''
    Say '  Weather is optional. Press Enter to skip; you can add it later.'
    $city = Read-Host '    City name to display (e.g. Kozhikode)'
    if ($city) {
        $lat = Read-Host '    Latitude  (e.g. 11.2448)'
        $lon = Read-Host '    Longitude (e.g. 75.7721)'
        $latOk = 0.0
        $lonOk = 0.0
        if ([double]::TryParse($lat, [ref]$latOk) -and [double]::TryParse($lon, [ref]$lonOk)) {
            @{ name = $city; lat = $latOk; lon = $lonOk } |
                ConvertTo-Json | Set-Content -Path $weatherPath -Encoding UTF8
            Good "weather set to $city"
        }
        else {
            Warn 'latitude/longitude were not numbers - skipping weather setup'
        }
    }
    else {
        Warn 'weather skipped'
    }
}

# --------------------------------------------------------------------- startup
$launcher = Join-Path $InstallDir 'launcher\DesktopWidgets.vbs'
if (-not (Test-Path $launcher)) { Die "launcher missing at $launcher" }

if (-not $NoStartup) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'DesktopWidgets.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($lnk)
    # wscript rather than powershell: it launches with no console window and no
    # taskbar flash, which matters for something that runs at every logon.
    $sc.TargetPath = "$env:WINDIR\System32\wscript.exe"
    $sc.Arguments = "`"$launcher`""
    $sc.WorkingDirectory = $InstallDir
    $sc.Description = 'Desktop Widgets'
    $sc.Save()
    Good "starts at logon ($lnk)"
}
else {
    Warn 'startup entry skipped (-NoStartup)'
}

# ------------------------------------------------------------------- font note
$fontDir = Join-Path $InstallDir 'fonts'
# Note: -Include returns nothing on a bare directory path unless -Recurse is
# used, so filter on the extension instead.
$fontFiles = @(Get-ChildItem $fontDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -eq '.otf' -or $_.Extension -eq '.ttf' })
Say ''
if ($fontFiles.Count -gt 0) {
    Good "display font present: $($fontFiles[0].Name)"
}
else {
    Warn 'Anurati font not present - the day banner falls back to Century Gothic.'
    Say  '        It is not shipped in this repo because it is licensed for personal'
    Say  '        use only, and bundling it would be redistribution. For the intended'
    Say  '        look, download Anurati (by Emmeran Richard) and drop the .otf into:'
    Say  "            $fontDir"
    Say  ('        Then verify with: powershell -File "' + (Join-Path $InstallDir 'check-font.ps1') + '"')
}

# ---------------------------------------------------------------------- launch
if (-not $NoLaunch) {
    Start-Process -FilePath "$env:WINDIR\System32\wscript.exe" -ArgumentList "`"$launcher`""
    Start-Sleep -Seconds 2
    if (Get-Process DesktopWidgets -ErrorAction SilentlyContinue) {
        Good 'widgets are running'
    }
    else {
        Warn 'widgets did not appear to start; try running the launcher manually'
    }
}

Say ''
Say 'Done.'
Say "  Installed to : $InstallDir"
Say "  Layout/state : $stateDir"
Say '  Right-click any widget to lock positions, reset them, or exit.'
Say ("  Uninstall    : powershell -File `"" + (Join-Path $InstallDir 'uninstall.ps1') + "`"")
Say ''
