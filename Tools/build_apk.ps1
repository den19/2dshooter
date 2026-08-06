# Release build: SpacePrisonAsurvil.apk in the project root.
# - Bumps Version patch + Bundle Version Code (inside Unity)
# - Signs with C:/git/MyUnity6.4KeyStore/user.keystore alias asurvil
# Usage (Unity Editor must be CLOSED):
#   powershell -ExecutionPolicy Bypass -File Tools\build_apk.ps1
# Secrets (gitignored):
#   Copy Tools\keystore.local.ps1.example → Tools\keystore.local.ps1
#   Set $KeystorePassword = "..."  (same password for keystore and alias)
# Or set env ANDROID_KEYSTORE_PASS before running.

[CmdletBinding()]
param(
    [string] $UnityPath = $env:UNITY_EDITOR,
    [string] $ProjectPath = "",
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

function Resolve-UnityEditor {
    param([string] $Preferred, [string] $Root)

    if ($Preferred -and (Test-Path -LiteralPath $Preferred)) {
        return (Resolve-Path -LiteralPath $Preferred).Path
    }

    $versionFile = Join-Path $Root "ProjectSettings\ProjectVersion.txt"
    $version = $null
    if (Test-Path -LiteralPath $versionFile) {
        $line = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
        if ($line -match '^m_EditorVersion:\s*(.+)$') {
            $version = $Matches[1].Trim()
        }
    }

    $candidates = @()
    if ($version) {
        $candidates += "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    }
    $candidates += "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"

    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) {
            return (Resolve-Path -LiteralPath $c).Path
        }
    }

    throw "Unity Editor not found. Pass -UnityPath or set UNITY_EDITOR to Unity.exe."
}

function Test-UnityProjectLock {
    param([string] $Root)
    $lockFile = Join-Path $Root "Temp\UnityLockfile"
    return (Test-Path -LiteralPath $lockFile)
}

function Import-KeystorePassword {
    param([string] $Root)

    if (-not [string]::IsNullOrEmpty($env:ANDROID_KEYSTORE_PASS)) {
        Write-Host "Using ANDROID_KEYSTORE_PASS from environment."
        return
    }

    $local = Join-Path $Root "Tools\keystore.local.ps1"
    if (-not (Test-Path -LiteralPath $local)) {
        Write-Error @"
Release signing password missing.
1. Copy Tools\keystore.local.ps1.example → Tools\keystore.local.ps1
2. Set `$KeystorePassword = "your_password" (same for keystore and alias asurvil)
3. Re-run: powershell -ExecutionPolicy Bypass -File Tools\build_apk.ps1
Or set env ANDROID_KEYSTORE_PASS.
"@
    }

    . $local
    if ([string]::IsNullOrWhiteSpace($KeystorePassword) -or $KeystorePassword -eq "YOUR_PASSWORD") {
        Write-Error "Tools\keystore.local.ps1 must set `$KeystorePassword to a real password (not YOUR_PASSWORD)."
    }

    $env:ANDROID_KEYSTORE_PASS = $KeystorePassword
    Write-Host "Loaded keystore password from Tools\keystore.local.ps1"
}

$root = Get-ProjectRoot
$unity = Resolve-UnityEditor -Preferred $UnityPath -Root $root
$apkPath = Join-Path $root "SpacePrisonAsurvil.apk"
$logPath = Join-Path $root "build_apk.log"
$method = "SpacePrisonAsurvil.Editor.AndroidApkBuilder.BuildFromBatch"

Write-Host "Project: $root"
Write-Host "Unity:   $unity"
Write-Host "Output:  $apkPath"
Write-Host "Log:     $logPath"

Import-KeystorePassword -Root $root

if (-not $SkipEditorLockCheck -and (Test-UnityProjectLock -Root $root)) {
    Write-Error @"
Unity Editor appears to have this project open (Temp\UnityLockfile exists).
Close the Editor, then re-run:
  powershell -ExecutionPolicy Bypass -File Tools\build_apk.ps1
Or pass -SkipEditorLockCheck if you know the lock is stale.
"@
}

$keystorePath = "C:\git\MyUnity6.4KeyStore\user.keystore"
if (-not (Test-Path -LiteralPath $keystorePath)) {
    Write-Error "Keystore not found: $keystorePath"
}

$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $root,
    "-executeMethod", $method,
    "-logFile", $logPath
)

Write-Host "Starting Unity release batch build (version bump + signed APK)..."
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru
$code = $proc.ExitCode

if ($code -ne 0) {
    Write-Host "Unity exited with code $code. Last log lines:"
    if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Tail 80
    }
    exit $code
}

if (-not (Test-Path -LiteralPath $apkPath)) {
    Write-Error "Build reported success but APK was not found at $apkPath. See $logPath"
}

$item = Get-Item -LiteralPath $apkPath
Write-Host ("OK: {0} ({1:N1} MB)" -f $item.FullName, ($item.Length / 1MB))
exit 0
