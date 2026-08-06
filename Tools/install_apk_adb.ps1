# Build release APK, then install it on a USB-connected phone via adb.
#
# Usage (Unity Editor must be CLOSED unless -SkipBuild):
#   powershell -ExecutionPolicy Bypass -File Tools\install_apk_adb.ps1
#   powershell -ExecutionPolicy Bypass -File Tools\install_apk_adb.ps1 -SkipBuild
#   powershell -ExecutionPolicy Bypass -File Tools\install_apk_adb.ps1 -Serial 1445125581103151
#
# Default APK: {projectRoot}/SpacePrisonAsurvil.apk
# Package id:  com.densappstudio.asurvil
#
# If install fails with signature mismatch:
#   adb uninstall com.densappstudio.asurvil
# then re-run with -SkipBuild.

[CmdletBinding()]
param(
    [string] $ApkPath = "",
    [string] $ProjectPath = "",
    [string] $AdbPath = "",
    [string] $Serial = "",
    [switch] $SkipBuild,
    [switch] $SkipEditorLockCheck
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

function Get-UnityEditorVersion {
    param([string] $Root)
    $versionFile = Join-Path $Root "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile)) {
        return $null
    }
    $line = Get-Content -LiteralPath $versionFile |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1
    if ($line -match '^m_EditorVersion:\s*(.+)$') {
        return $Matches[1].Trim()
    }
    return $null
}

function Resolve-Adb {
    param(
        [string] $Preferred,
        [string] $Root
    )

    $candidates = @()
    if ($Preferred) { $candidates += $Preferred }
    if ($env:ADB) { $candidates += $env:ADB }

    foreach ($sdk in @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT)) {
        if ($sdk) {
            $candidates += (Join-Path $sdk "platform-tools\adb.exe")
        }
    }

    $candidates += (Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe")

    $unityVersion = Get-UnityEditorVersion -Root $Root
    if ($unityVersion) {
        $candidates += "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    }
    $candidates += "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"

    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) {
            return (Resolve-Path -LiteralPath $c).Path
        }
    }

    $fromPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw @"
adb.exe not found.
Install Android platform-tools, or set -AdbPath / `$env:ADB / ANDROID_HOME.
Tried LOCALAPPDATA\Android\Sdk and Unity AndroidPlayer SDK.
"@
}

function Get-AdbDeviceLines {
    param([string] $Adb)
    & $Adb start-server | Out-Null
    $raw = & $Adb devices
    $lines = @()
    foreach ($line in $raw) {
        if ($line -match '^\s*$' -or $line -match '^List of devices') {
            continue
        }
        if ($line -match '^(\S+)\s+(\S+)') {
            $lines += [pscustomobject]@{
                Serial = $Matches[1]
                State  = $Matches[2]
            }
        }
    }
    return $lines
}

$root = Get-ProjectRoot

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $root "SpacePrisonAsurvil.apk"
} elseif (-not [System.IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $root $ApkPath
}

$buildScript = Join-Path $root "Tools\build_apk.ps1"
$adb = Resolve-Adb -Preferred $AdbPath -Root $root

Write-Host "Project: $root"
Write-Host "APK:     $ApkPath"
Write-Host "ADB:     $adb"
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

$devices = @(Get-AdbDeviceLines -Adb $adb)
if ($devices.Count -eq 0) {
    Write-Error "No adb devices. Connect USB, enable USB debugging, then retry."
}

$unauthorized = @($devices | Where-Object { $_.State -eq "unauthorized" })
if ($unauthorized.Count -gt 0) {
    $list = ($unauthorized | ForEach-Object { "  $($_.Serial)`tunauthorized" }) -join "`n"
    Write-Error @"
Phone is connected but unauthorized. On the phone: Accept / allow USB debugging (RSA fingerprint), then retry.
Unauthorized:
$list
"@
}

$ready = @($devices | Where-Object { $_.State -eq "device" })
if ($ready.Count -eq 0) {
    $list = ($devices | ForEach-Object { "  $($_.Serial)`t$($_.State)" }) -join "`n"
    Write-Error @"
No device in 'device' state. Current adb devices:
$list
"@
}

$targetSerial = $Serial
if ([string]::IsNullOrWhiteSpace($targetSerial)) {
    if ($ready.Count -gt 1) {
        $list = ($ready | ForEach-Object { "  $($_.Serial)" }) -join "`n"
        Write-Error @"
Multiple devices attached. Pass -Serial <id>:
$list
"@
    }
    $targetSerial = $ready[0].Serial
} else {
    $match = $ready | Where-Object { $_.Serial -eq $targetSerial } | Select-Object -First 1
    if (-not $match) {
        Write-Error "Serial '$targetSerial' not found among ready devices. Run: `"$adb`" devices"
    }
}

Write-Host "Device:  $targetSerial"
Write-Host "Installing (adb install -r)..."

& $adb -s $targetSerial install -r $ApkPath
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Host @"
adb install failed (exit $code).
If signature mismatch with an older build:
  `"$adb`" -s $targetSerial uninstall com.densappstudio.asurvil
then re-run with -SkipBuild.
"@
    exit $code
}

Write-Host "Done: installed $ApkPath on $targetSerial (package com.densappstudio.asurvil)."
exit 0
