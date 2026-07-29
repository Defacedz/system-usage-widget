# ============================================================
#  One-line installer for System Widget.
#
#    irm https://raw.githubusercontent.com/Defacedz/system-usage-widget/main/web-install.ps1 | iex
#
#  It downloads this repository, then runs Installer.ps1 elevated, which
#  builds the widget from source on your machine. Read the README before
#  running it - it explains the trusted certificate the installer adds.
#
#  Keep this file ASCII-only: PowerShell 5.1 reads a BOM-less .ps1 as ANSI.
# ============================================================
$ErrorActionPreference = 'Stop'
$repo = 'Defacedz/system-usage-widget'
$branch = 'main'

Write-Host ''
Write-Host '  System Widget - installer' -ForegroundColor Cyan
Write-Host '  Source: https://github.com/Defacedz/system-usage-widget'
Write-Host ''

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Sweep up anything an interrupted earlier run left behind: closing this
# window during the elevated step skips the finally block below. Only
# touch folders older than an hour, so a concurrent run is left alone.
Get-ChildItem $env:TEMP -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'systemwidget-*' -and $_.CreationTime -lt (Get-Date).AddHours(-1) } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$work = Join-Path $env:TEMP ('systemwidget-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null

try {
    $zip = Join-Path $work 'source.zip'
    Write-Host '1/3 Downloading the source...'
    Invoke-WebRequest -Uri "https://github.com/$repo/archive/refs/heads/$branch.zip" `
        -OutFile $zip -UseBasicParsing

    Write-Host '2/3 Extracting...'
    Expand-Archive -Path $zip -DestinationPath $work -Force
    $extracted = Get-ChildItem $work -Directory | Select-Object -First 1
    if (-not $extracted) { throw 'The downloaded archive looks empty.' }
    $installer = Join-Path $extracted.FullName 'Installer.ps1'
    if (-not (Test-Path $installer)) { throw 'Installer.ps1 is missing from the archive.' }

    Write-Host '3/3 Running the installer - accept the administrator prompt.'
    Start-Process powershell -Verb RunAs -Wait -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $installer + '"'))
}
catch {
    Write-Host ''
    Write-Host ('[ERROR] ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'You can also download the repository manually and run Installer.bat.'
}
finally {
    # The installer copied the built exe into Program Files; the sources are
    # only needed during the build.
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
