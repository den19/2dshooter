# Known phones for ADB / Bluetooth deploy (shared across DensAppStudio projects).
# Dot-source from Tools/install_apk_adb.ps1 and Tools/deploy_apk_bluetooth.ps1.
# Not a secret — safe to commit.

$script:KnownPhones = @(
    [pscustomobject]@{
        Id            = "pova"
        BluetoothName = "TECNO POVA 7 Ultra 5G"
        Aliases       = @(
            "pova",
            "TECNO POVA 7 Ultra 5G",
            "TECHNO POVA 7 Ultra 5G",
            "POVA 7",
            "POVA"
        )
        AdbSerial     = "1445125581103151"
        AdbModelRegex = "POVA.?7|TECNO_POVA_7"
    }
    [pscustomobject]@{
        Id            = "camon"
        BluetoothName = "TECNO CAMON 20 Pro"
        Aliases       = @(
            "camon",
            "camon20",
            "CAMON 20",
            "CAMON 20 Pro",
            "TECNO CAMON 20 Pro",
            "второй",
            "second"
        )
        AdbSerial     = "1026225377001850"
        AdbModelRegex = "CAMON.?20|TECNO_CAMON_20|CK7n|TECNO_CK7n"
    }
)

function Get-KnownPhoneHelp {
    $lines = @($script:KnownPhones | ForEach-Object {
        $serial = if ($_.AdbSerial) { $_.AdbSerial } else { "(via adb model)" }
        "  -Phone $($_.Id)`t$($_.BluetoothName)`tADB $serial"
    })
    return ($lines -join "`n")
}

function Resolve-KnownPhone {
    param(
        [string] $Phone = "",
        [string] $DeviceName = ""
    )

    $token = if (-not [string]::IsNullOrWhiteSpace($Phone)) {
        $Phone.Trim()
    } elseif (-not [string]::IsNullOrWhiteSpace($DeviceName)) {
        $DeviceName.Trim()
    } else {
        return $null
    }

    foreach ($p in $script:KnownPhones) {
        if ([string]::Equals($p.Id, $token, [StringComparison]::OrdinalIgnoreCase)) {
            return $p
        }
        if ([string]::Equals($p.BluetoothName, $token, [StringComparison]::OrdinalIgnoreCase)) {
            return $p
        }
        if ($p.AdbSerial -and [string]::Equals($p.AdbSerial, $token, [StringComparison]::OrdinalIgnoreCase)) {
            return $p
        }
        foreach ($a in $p.Aliases) {
            if ([string]::Equals($a, $token, [StringComparison]::OrdinalIgnoreCase)) {
                return $p
            }
        }
    }

    return $null
}

function Get-BluetoothDeviceCandidates {
    param(
        [string] $Phone = "",
        [string] $DeviceName = ""
    )

    $resolved = Resolve-KnownPhone -Phone $Phone -DeviceName $DeviceName
    if ($resolved) {
        return @($resolved.BluetoothName)
    }

    if (-not [string]::IsNullOrWhiteSpace($DeviceName)) {
        return @($DeviceName.Trim())
    }
    if (-not [string]::IsNullOrWhiteSpace($Phone)) {
        throw @"
Unknown -Phone '$Phone'. Known phones:
$(Get-KnownPhoneHelp)
Or pass -DeviceName with the exact Windows Bluetooth name.
"@
    }

    # Default: try all known phones (OBEX picks the first found / reachable).
    return @($script:KnownPhones | ForEach-Object { $_.BluetoothName })
}

function Resolve-AdbSerialForPhone {
    param(
        [string] $Adb,
        [object[]] $ReadyDevices,
        [string] $Phone = "",
        [string] $Serial = ""
    )

    if (-not [string]::IsNullOrWhiteSpace($Serial)) {
        return $Serial.Trim()
    }

    $resolved = Resolve-KnownPhone -Phone $Phone
    if (-not $resolved -and -not [string]::IsNullOrWhiteSpace($Phone)) {
        throw @"
Unknown -Phone '$Phone'. Known phones:
$(Get-KnownPhoneHelp)
Or pass -Serial <adb-serial>.
"@
    }

    if (-not $resolved) {
        return $null
    }

    if ($resolved.AdbSerial) {
        $match = @($ReadyDevices | Where-Object { $_.Serial -eq $resolved.AdbSerial })
        if ($match.Count -gt 0) {
            return $resolved.AdbSerial
        }
    }

    # Match by model from `adb devices -l`
    $raw = & $Adb devices -l
    foreach ($line in $raw) {
        if ($line -match '^(\S+)\s+device\b' -and $line -match $resolved.AdbModelRegex) {
            $candidate = $Matches[1]
            if ($ReadyDevices | Where-Object { $_.Serial -eq $candidate }) {
                return $candidate
            }
        }
    }

    throw @"
Phone '$($resolved.BluetoothName)' (-Phone $($resolved.Id)) is not among ready adb devices.
Connect it over USB (status 'device'), Accept USB debugging if asked, then retry.
Ready now: $(($ReadyDevices | ForEach-Object { $_.Serial }) -join ', ')
"@
}
