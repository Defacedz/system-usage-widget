# ============================================================
#  Installs SystemWidget.exe (uiAccess) - RUN AS ADMINISTRATOR
#  1. builds SystemWidget.cs with the csc.exe shipped in Windows
#  2. creates/reuses a local certificate and signs the exe
#  3. trusts that certificate on this machine (required by uiAccess)
#  4. installs into Program Files and starts it
#
#  Keep this file ASCII-only: PowerShell 5.1 reads a BOM-less .ps1 as
#  ANSI, so an accented character here would corrupt the script.
# ============================================================
$ErrorActionPreference = 'Stop'

function Fail($message) {
    Write-Host "`n[ERROR] $message" -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}

try {
    $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    $source = Join-Path $here 'SystemWidget.cs'
    $manifest = Join-Path $here 'app.manifest'
    if (-not (Test-Path $source) -or -not (Test-Path $manifest)) {
        Fail 'SystemWidget.cs or app.manifest not found next to this script.'
    }

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Fail 'This script must run as administrator (use Installer.bat).'
    }

    Write-Host '1/6 Stopping running instances...'
    Get-Process -Name 'SystemWidget' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400

    Write-Host '2/6 Building...'
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
        $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    }
    $compiler = Join-Path $framework 'csc.exe'
    if (-not (Test-Path $compiler)) {
        Fail 'C# compiler (.NET Framework 4) not found.'
    }

    $temporaryExe = Join-Path $env:TEMP 'SystemWidget.exe'
    Remove-Item $temporaryExe -Force -ErrorAction SilentlyContinue
    $wpf = Join-Path $framework 'WPF'
    # /codepage:65001 is a safety net: the source is UTF-8 with a BOM, but an
    # editor that strips the BOM would otherwise mangle the translated strings.
    & $compiler /nologo /target:winexe /out:$temporaryExe /win32manifest:$manifest /codepage:65001 `
        /r:System.dll /r:System.Core.dll /r:System.Xaml.dll `
        /r:System.Runtime.Serialization.dll /r:Microsoft.CSharp.dll `
        /r:"$wpf\PresentationFramework.dll" /r:"$wpf\PresentationCore.dll" `
        /r:"$wpf\WindowsBase.dll" $source
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $temporaryExe)) {
        Fail 'Build failed (see messages above).'
    }

    Write-Host '3/6 Local signing certificate...'
    $subject = 'CN=SystemWidget Local'
    $certificate = Get-ChildItem Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
            -CertStoreLocation 'Cert:\LocalMachine\My' -NotAfter (Get-Date).AddYears(10)
    }

    Write-Host '4/6 Trusting the certificate...'
    $certificateFile = Join-Path $env:TEMP 'SystemWidgetLocal.cer'
    Export-Certificate -Cert $certificate -FilePath $certificateFile | Out-Null
    Import-Certificate -FilePath $certificateFile `
        -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $certificateFile `
        -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Remove-Item $certificateFile -Force -ErrorAction SilentlyContinue

    Write-Host '5/6 Signing and installing...'
    $signature = Set-AuthenticodeSignature -FilePath $temporaryExe `
        -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid') {
        Fail ('Invalid signature: ' + $signature.StatusMessage)
    }
    $destinationDirectory = Join-Path $env:ProgramFiles 'SystemWidget'
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item $temporaryExe (Join-Path $destinationDirectory 'SystemWidget.exe') -Force
    Remove-Item $temporaryExe -Force -ErrorAction SilentlyContinue

    Write-Host '6/6 Starting...'
    Start-Process (Join-Path $destinationDirectory 'SystemWidget.exe')
    Write-Host "`n[OK] System Widget is installed and running." -ForegroundColor Green
    Write-Host 'Right-click the widget for language, opacity, autostart and quit.'
    Read-Host 'Press Enter to close'
}
catch {
    Fail $_.Exception.Message
}
