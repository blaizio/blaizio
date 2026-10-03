<#
.SYNOPSIS
  Render og.html (this folder) to ../wwwroot/og.jpg, the 1200x630 link-preview card.
.DESCRIPTION
  Headless Chrome (or Edge when Chrome is missing) screenshots the page at exactly 1200x630, then
  System.Drawing re-encodes it as a quality-90 JPEG: the browser's own JPEG output is softer, and
  every platform that shows the card re-encodes it again. Windows only: the card is set in the
  system UI face, as the site is, and the shipped image uses Segoe UI. Look at the result before
  committing it.
#>
$ErrorActionPreference = 'Stop'

$browser = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw 'Neither Chrome nor Edge was found.' }

$page = Join-Path $PSScriptRoot 'og.html'
$out = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\wwwroot')).Path 'og.jpg'
$png = Join-Path ([IO.Path]::GetTempPath()) "blaizio-og-$([guid]::NewGuid()).png"
# A throwaway profile, so a running browser session is never touched.
$profileDir = Join-Path ([IO.Path]::GetTempPath()) "blaizio-og-profile-$([guid]::NewGuid())"

try {
    Write-Host "Screenshotting $page with $browser..."
    & $browser --headless=new --hide-scrollbars --no-first-run --user-data-dir="$profileDir" `
        --window-size=1200,630 --screenshot="$png" $page 2>&1 | Out-Null
    if (-not (Test-Path $png)) { throw 'The browser wrote no screenshot.' }

    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($png)
    try {
        if ($image.Width -ne 1200 -or $image.Height -ne 630) {
            throw "Expected 1200x630, got $($image.Width)x$($image.Height)."
        }
        $jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
        $quality = New-Object System.Drawing.Imaging.EncoderParameters 1
        $quality.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
        $image.Save($out, $jpeg, $quality)
    }
    finally {
        $image.Dispose()
    }
    Write-Host "Done. Wrote $out ($([math]::Round((Get-Item $out).Length / 1KB)) KB)."
}
finally {
    Remove-Item $png -Force -ErrorAction SilentlyContinue
    Remove-Item $profileDir -Recurse -Force -ErrorAction SilentlyContinue
}
