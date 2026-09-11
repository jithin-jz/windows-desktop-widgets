# Lists the font families WPF can actually see in the fonts folder.
#
# The banner asks for the family name "Anurati". A font file whose internal
# family name differs - "Anurati Regular", say - will not match, and the banner
# will quietly fall back to Century Gothic. Run this after dropping a font in to
# confirm the name, then update BannerFontSpec() in src\WidgetApp.cs if needed.

Add-Type -AssemblyName PresentationCore

$dir = Join-Path $PSScriptRoot 'fonts'
if (-not (Test-Path $dir)) { Write-Host "No fonts folder at $dir" -ForegroundColor Red; exit 1 }

# -Include matches nothing on a bare directory path without -Recurse, so the
# extension is checked explicitly here.
$files = Get-ChildItem $dir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -eq '.otf' -or $_.Extension -eq '.ttf' }
Write-Host "Font files in $dir :" -ForegroundColor Cyan
if (-not $files) { Write-Host "  (none - drop Anurati.otf here)" -ForegroundColor Yellow }
else { $files | ForEach-Object { Write-Host "  $($_.Name)  ($([math]::Round($_.Length/1KB)) KB)" } }

$uri = (New-Object System.Uri(($dir + [IO.Path]::DirectorySeparatorChar))).AbsoluteUri
$families = [Windows.Media.Fonts]::GetFontFamilies($uri)

Write-Host "`nFamily names WPF reports:" -ForegroundColor Cyan
if (-not $families) { Write-Host "  (none)" -ForegroundColor Yellow }
else {
    foreach ($f in $families) {
        $name = ($f.Source -split '#')[-1]
        $match = if ($name -eq 'Anurati') { 'MATCHES the banner' } else { "does NOT match - banner expects 'Anurati'" }
        $colour = if ($name -eq 'Anurati') { 'Green' } else { 'Yellow' }
        Write-Host "  $name    -> $match" -ForegroundColor $colour
    }
}
