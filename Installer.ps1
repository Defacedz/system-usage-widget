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
    $sensorLib = Join-Path $here 'lib\LibreHardwareMonitorLib.dll'
    # LibreHardwareMonitor 0.9.6 plus the assemblies it loads at runtime.
    $libNames = @(
        'LibreHardwareMonitorLib.dll', 'HidSharp.dll', 'System.Memory.dll',
        'System.Runtime.CompilerServices.Unsafe.dll', 'System.Buffers.dll',
        'RAMSPDToolkit-NDD.dll', 'DiskInfoToolkit.dll', 'BlackSharp.Core.dll'
    )
    $libFiles = $libNames | ForEach-Object { Join-Path $here ('lib\' + $_) }
    if (-not (Test-Path $source) -or -not (Test-Path $manifest)) {
        Fail 'SystemWidget.cs or app.manifest not found next to this script.'
    }
    foreach ($libFile in $libFiles) {
        if (-not (Test-Path $libFile)) { Fail ('Missing library: ' + $libFile) }
    }

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Fail 'This script must run as administrator (use Installer.bat).'
    }

    Write-Host '1/8 Stopping running instances...'
    Get-Process -Name 'SystemWidget' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400

    # Builds up to 2026.08.24 embedded LibreHardwareMonitor 0.9.3, which
    # installs WinRing0 - a driver on Microsoft's vulnerable-driver blocklist
    # (CVE-2020-14979: any local process can reach ring 0 through it). It was
    # registered here as the service R0SystemWidget. Remove it on the way in,
    # otherwise updating leaves the hole open on every machine that ran an
    # older build.
    Write-Host '2/8 Removing the old WinRing0 driver, if present...'
    $legacyService = Get-Service -Name 'R0SystemWidget' -ErrorAction SilentlyContinue
    if ($legacyService) {
        if ($legacyService.Status -eq 'Running') {
            Stop-Service -Name 'R0SystemWidget' -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 400
        }
        & sc.exe delete 'R0SystemWidget' | Out-Null
        Write-Host '      service R0SystemWidget deleted.'
    }
    Remove-Item (Join-Path (Join-Path $env:ProgramFiles 'SystemWidget') 'SystemWidget.sys') `
        -Force -ErrorAction SilentlyContinue

    Write-Host '3/8 Building...'
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
        /r:System.Management.dll `
        /r:"$here\lib\LibreHardwareMonitorLib.dll" `
        /r:"$wpf\PresentationFramework.dll" /r:"$wpf\PresentationCore.dll" `
        /r:"$wpf\WindowsBase.dll" $source
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $temporaryExe)) {
        Fail 'Build failed (see messages above).'
    }

    Write-Host '4/8 Local signing certificate...'
    $subject = 'CN=SystemWidget Local'
    $certificate = Get-ChildItem Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
            -CertStoreLocation 'Cert:\LocalMachine\My' -NotAfter (Get-Date).AddYears(10)
    }

    Write-Host '5/8 Trusting the certificate...'
    $certificateFile = Join-Path $env:TEMP 'SystemWidgetLocal.cer'
    Export-Certificate -Cert $certificate -FilePath $certificateFile | Out-Null
    Import-Certificate -FilePath $certificateFile `
        -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $certificateFile `
        -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Remove-Item $certificateFile -Force -ErrorAction SilentlyContinue

    Write-Host '6/8 Signing and installing...'
    $signature = Set-AuthenticodeSignature -FilePath $temporaryExe `
        -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid') {
        Fail ('Invalid signature: ' + $signature.StatusMessage)
    }
    $destinationDirectory = Join-Path $env:ProgramFiles 'SystemWidget'
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item $temporaryExe (Join-Path $destinationDirectory 'SystemWidget.exe') -Force
    Copy-Item $libFiles $destinationDirectory -Force
    Remove-Item $temporaryExe -Force -ErrorAction SilentlyContinue

    # LibreHardwareMonitor 0.9.6 reads the CPU sensor through PawnIO, the
    # signed driver that replaced WinRing0. PawnIO ships as its own installer,
    # so fetch the official signed build and run it quietly. Everything is
    # checked before anything executes: exact SHA-256 of the pinned release,
    # then the Authenticode signature. Without PawnIO the widget still runs -
    # the CPU thermometer simply stays blank.
    Write-Host '7/8 CPU sensor driver (PawnIO)...'
    $pawnKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO'
    )
    $pawnInstalled = $false
    foreach ($pawnKey in $pawnKeys) {
        if (Test-Path $pawnKey) { $pawnInstalled = $true }
    }
    if ($pawnInstalled) {
        Write-Host '      already installed.'
    }
    else {
        $pawnUrl = 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe'
        $pawnSha = '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'
        $pawnExe = Join-Path $env:TEMP 'PawnIO_setup.exe'
        try {
            Remove-Item $pawnExe -Force -ErrorAction SilentlyContinue
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $pawnUrl -OutFile $pawnExe -UseBasicParsing
            $hash = (Get-FileHash $pawnExe -Algorithm SHA256).Hash
            if ($hash -ne $pawnSha) { throw ('unexpected SHA-256: ' + $hash) }
            $pawnSignature = Get-AuthenticodeSignature $pawnExe
            if ($pawnSignature.Status -ne 'Valid') { throw ('signature ' + $pawnSignature.Status) }
            Write-Host ('      signed by ' + $pawnSignature.SignerCertificate.Subject)
            $run = Start-Process $pawnExe -ArgumentList '/quiet', '/norestart' -Wait -PassThru
            if ($run.ExitCode -ne 0 -and $run.ExitCode -ne 3010) {
                throw ('installer returned ' + $run.ExitCode)
            }
            Write-Host '      installed.'
        }
        catch {
            Write-Host ('      skipped (' + $_.Exception.Message + ').') -ForegroundColor Yellow
            Write-Host '      CPU thermometer stays blank; install PawnIO from https://pawnio.eu to enable it.'
        }
        finally {
            Remove-Item $pawnExe -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host '8/8 Starting...'
    # Started from this elevated script, the widget gets the administrator
    # rights the embedded sensor library needs to read the CPU temperature.
    Start-Process (Join-Path $destinationDirectory 'SystemWidget.exe')
    Write-Host "`n[OK] System Widget is installed and running." -ForegroundColor Green
    Write-Host 'Right-click the widget for language, opacity, autostart and quit.'
    Read-Host 'Press Enter to close'
}
catch {
    Fail $_.Exception.Message
}
