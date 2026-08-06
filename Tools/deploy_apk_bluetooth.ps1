# Build release APK, then push it to a paired phone via Bluetooth OBEX.
# Auto OBEX first; on failure opens Windows Bluetooth File Transfer (fsquirt).
#
# Usage (Unity Editor must be CLOSED unless -SkipBuild):
#   powershell -ExecutionPolicy Bypass -File Tools\deploy_apk_bluetooth.ps1
#   powershell -ExecutionPolicy Bypass -File Tools\deploy_apk_bluetooth.ps1 -SkipBuild
#   powershell -ExecutionPolicy Bypass -File Tools\deploy_apk_bluetooth.ps1 -DeviceName "TECHNO POVA 7 Ultra 5G"
#
# Default device: TECHNO POVA 7 Ultra 5G
# Default APK:    {projectRoot}/SpacePrisonAsurvil.apk

[CmdletBinding()]
param(
    [string] $DeviceName = "TECHNO POVA 7 Ultra 5G",
    [string] $ApkPath = "",
    [string] $ProjectPath = "",
    [switch] $SkipBuild,
    [switch] $SkipEditorLockCheck,
    [switch] $NoFsquirtFallback
)

$ErrorActionPreference = "Stop"

function Get-ProjectRoot {
    if ($ProjectPath) {
        return (Resolve-Path -LiteralPath $ProjectPath).Path
    }
    if ((Split-Path -Leaf $PSScriptRoot) -eq "Tools") {
        return (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
    }
    return (Resolve-Path -LiteralPath $PSScriptRoot).Path
}

function Invoke-FsquirtFallback {
    param(
        [string] $Apk,
        [string] $Device,
        [string] $Reason
    )

    Write-Host ""
    Write-Host "Auto OBEX failed: $Reason"
    Write-Host "Falling back to Windows Bluetooth File Transfer (fsquirt)..."

    $fsquirt = Join-Path $env:SystemRoot "System32\fsquirt.exe"
    if (-not (Test-Path -LiteralPath $fsquirt)) {
        Write-Error "fsquirt.exe not found at $fsquirt. Open Bluetooth settings and send the file manually: $Apk"
    }

    Start-Process -FilePath $fsquirt
    Write-Host @"

Manual steps:
  1. Choose Send files.
  2. Select device: $Device
  3. Browse to APK: $Apk
  4. Accept the transfer on the phone.

"@
}

$root = Get-ProjectRoot

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $root "SpacePrisonAsurvil.apk"
} elseif (-not [System.IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $root $ApkPath
}

$obexProject = Join-Path $root "Tools\BluetoothObexPush\BluetoothObexPush.csproj"
$buildScript = Join-Path $root "Tools\build_apk.ps1"

Write-Host "Project: $root"
Write-Host "APK:     $ApkPath"
Write-Host "Device:  $DeviceName"
Write-Host "Build:   $(-not [bool]$SkipBuild)"

if (-not $SkipBuild) {
    if (-not (Test-Path -LiteralPath $buildScript)) {
        Write-Error "Build script not found: $buildScript"
    }
    $buildArgs = @("-ExecutionPolicy", "Bypass", "-File", $buildScript)
    if ($SkipEditorLockCheck) {
        $buildArgs += "-SkipEditorLockCheck"
    }
    Write-Host "Running release build..."
    & powershell @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "build_apk.ps1 failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $ApkPath)) {
    Write-Error "APK not found: $ApkPath. Build first or pass -ApkPath."
}

if (-not (Test-Path -LiteralPath $obexProject)) {
    Write-Error "OBEX tool project not found: $obexProject"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet SDK not found on PATH. Install .NET 9+ SDK to run BluetoothObexPush."
}

Write-Host "Attempting automatic OBEX push..."
& dotnet run --project $obexProject -c Release -- $ApkPath $DeviceName
$obexCode = $LASTEXITCODE

if ($obexCode -eq 0) {
    Write-Host "Done: APK sent via Bluetooth OBEX to `"$DeviceName`"."
    exit 0
}

if ($NoFsquirtFallback) {
    Write-Error "Auto OBEX failed (exit $obexCode) and -NoFsquirtFallback was set."
}

Invoke-FsquirtFallback -Apk $ApkPath -Device $DeviceName -Reason "exit code $obexCode"
exit $obexCode
