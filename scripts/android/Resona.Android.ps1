#requires -Version 7
<#
.SYNOPSIS
    Build, deploy, provision and control the Resona Android kiosk app.

.EXAMPLE
    ./Resona.Android.ps1 -Action Build
.EXAMPLE
    ./Resona.Android.ps1 -Action Provision -Device emulator-5554
.EXAMPLE
    ./Resona.Android.ps1 -Action Deploy -Device 192.168.1.50:37123
.EXAMPLE
    ./Resona.Android.ps1 -Action Publish -PublishDir \\nas\web\resona
.EXAMPLE
    ./Resona.Android.ps1 -Action SetUpdateSource -Url http://192.168.1.10:8000/
.EXAMPLE
    ./Resona.Android.ps1 -Action CheckUpdate
.EXAMPLE
    ./Resona.Android.ps1 -Action KioskOff
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Build', 'Deploy', 'Provision', 'Status', 'KioskOff', 'KioskOn', 'Release', 'Logs', 'Publish', 'SetUpdateSource', 'CheckUpdate')]
    [string] $Action,

    # adb serial, or ip:port for wireless debugging (adb connect is run first). Optional if exactly one device is attached.
    [string] $Device,

    [string] $Configuration = 'Release',

    # Deploy/Provision: install the APK that was last built instead of building first.
    [switch] $NoBuild,

    # Release: skip the confirmation prompt.
    [switch] $Force,

    # Publish: folder served by your web server (local or UNC). Defaults to $env:RESONA_PUBLISH_DIR.
    [string] $PublishDir = $env:RESONA_PUBLISH_DIR,

    # SetUpdateSource: base URL holding resona-update.json, e.g. http://nas/resona/ (empty string disables updates).
    [string] $Url,

    # Build/Deploy/Provision/Publish: create a new signing keystore if none exists. Without it a missing keystore is an
    # error, because a new key can't update existing devices and changes the SHA1 registered with Spotify.
    [switch] $NewKeystore,

    [string] $KeystorePath = (Join-Path $HOME '.resona/resona-release.keystore'),
    [string] $KeyAlias = 'resona'
)

$ErrorActionPreference = 'Stop'

$PackageName = 'com.resona'
$AdminComponent = "$PackageName/com.resona.ResonaDeviceAdminReceiver"
$CommandComponent = "$PackageName/com.resona.KioskCommandReceiver"
$WebPort = 8080

# Broadcast actions handled by KioskCommandReceiver
$ActionKioskDisable = 'com.resona.action.KIOSK_DISABLE'
$ActionKioskEnable = 'com.resona.action.KIOSK_ENABLE'
$ActionReleaseOwner = 'com.resona.action.RELEASE_OWNER'
$ActionSetUpdateSource = 'com.resona.action.SET_UPDATE_SOURCE'
$ActionCheckUpdate = 'com.resona.action.CHECK_UPDATE'
$ActionUpdateStatus = 'com.resona.action.UPDATE_STATUS'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
$ProjectPath = Join-Path $RepoRoot 'src/Resona.Android/Resona.Android.csproj'
$PasswordPath = "$KeystorePath.password"

# ---------------------------------------------------------------------------------------------------------------------
# Tool discovery
# ---------------------------------------------------------------------------------------------------------------------

function Find-Adb {
    $roots = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, 'C:\Program Files (x86)\Android\android-sdk', (Join-Path $env:LOCALAPPDATA 'Android\Sdk')) |
        Where-Object { $_ }
    foreach ($root in $roots) {
        $candidate = Join-Path $root 'platform-tools/adb.exe'
        if (Test-Path $candidate) { return $candidate }
    }
    $onPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw 'adb not found. Set ANDROID_HOME or install the Android SDK platform-tools.'
}

function Find-Keytool {
    $jdks = @($env:JAVA_HOME, 'C:\Program Files\Android\openjdk\jdk-17.0.14-8', 'C:\Program Files\Android\openjdk') |
        Where-Object { $_ -and (Test-Path $_) }
    foreach ($jdk in $jdks) {
        $found = Get-ChildItem -Path $jdk -Filter keytool.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    foreach ($pattern in 'C:\Program Files\Microsoft\jdk-*', 'C:\Program Files\Android\openjdk\*', 'C:\Program Files\Eclipse Adoptium\*', 'C:\Program Files\Java\*') {
        $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'bin/keytool.exe' } |
            Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($found) { return $found }
    }
    $onPath = Get-Command keytool -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw 'keytool not found. Set JAVA_HOME to a JDK (e.g. the one used by the Android workload).'
}

$script:Adb = $null   # resolved lazily by Resolve-Device so Build/Publish don't need adb
$script:Serial = $null

# ---------------------------------------------------------------------------------------------------------------------
# adb helpers
# ---------------------------------------------------------------------------------------------------------------------

function Resolve-Device {
    if (-not $script:Adb) { $script:Adb = Find-Adb }

    if ($Device -match ':\d+$') {
        Write-Host "adb connect $Device"
        & $script:Adb connect $Device | Out-Host
    }

    if ($Device) {
        $script:Serial = $Device
    }
    else {
        $attached = @(& $script:Adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\sdevice$' } | ForEach-Object { ($_ -split '\s+')[0] })
        if ($attached.Count -eq 0) { throw 'No adb device connected.' }
        if ($attached.Count -gt 1) { throw "Multiple devices attached ($($attached -join ', ')); pass -Device <serial>." }
        $script:Serial = $attached[0]
    }

    $state = (& $script:Adb -s $script:Serial get-state 2>&1) -join ''
    if ($state -ne 'device') { throw "Device '$($script:Serial)' is not ready (state: $state). Check USB debugging authorization." }
    Write-Host "Using device $($script:Serial)" -ForegroundColor Cyan
}

# Runs `adb -s <serial> <args>` and returns the output as one string (stderr merged). Does not throw on failure.
function Invoke-Adb {
    param([Parameter(ValueFromRemainingArguments)] [string[]] $AdbArgs)
    $output = & $script:Adb -s $script:Serial @AdbArgs 2>&1
    return (($output | ForEach-Object { "$_" }) -join "`n").Trim()
}

function Invoke-Shell {
    param([Parameter(Mandatory)] [string] $Command)
    return Invoke-Adb shell $Command
}

# ---------------------------------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------------------------------

function Ensure-Keystore {
    if ((Test-Path $KeystorePath) -and (Test-Path $PasswordPath)) { return }
    if (Test-Path $KeystorePath) { throw "Keystore $KeystorePath exists but $PasswordPath is missing." }
    if (-not $NewKeystore) {
        throw (@(
            "Signing keystore not found: $KeystorePath (and $PasswordPath).",
            'If you are building on another machine, copy both files there from the machine that built the installed app.',
            'A new key cannot update devices already running Resona, and changes the Android SHA1 registered in the Spotify',
            'developer dashboard. Only for a brand new setup, rerun with -NewKeystore to create one.'
        ) -join [Environment]::NewLine)
    }

    $keytool = Find-Keytool
    New-Item -ItemType Directory -Force (Split-Path $KeystorePath) | Out-Null

    $bytes = [byte[]]::new(24)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $password = [Convert]::ToBase64String($bytes).Replace('+', 'A').Replace('/', 'b').Replace('=', 'c')

    $env:RESONA_KS_PASS = $password
    try {
        & $keytool -genkeypair -keystore $KeystorePath -alias $KeyAlias -keyalg RSA -keysize 2048 -validity 36500 `
            -dname 'CN=Resona, O=Resona' '-storepass:env' RESONA_KS_PASS '-keypass:env' RESONA_KS_PASS
        if ($LASTEXITCODE -ne 0) { throw 'keytool failed to create the keystore.' }
    }
    finally {
        Remove-Item Env:RESONA_KS_PASS -ErrorAction SilentlyContinue
    }
    Set-Content -Path $PasswordPath -Value $password -NoNewline

    Write-Host ''
    Write-Host '=====================================================================================' -ForegroundColor Red
    Write-Host ' BACK THESE TWO FILES UP (outside this repo, e.g. a password manager / private cloud):' -ForegroundColor Red
    Write-Host "   $KeystorePath" -ForegroundColor Red
    Write-Host "   $PasswordPath" -ForegroundColor Red
    Write-Host ' Losing them means updates can no longer be installed over the kiosk device-owner app' -ForegroundColor Red
    Write-Host ' without releasing/resetting the device first.' -ForegroundColor Red
    Write-Host '=====================================================================================' -ForegroundColor Red
    Write-Host ''
}

$ManifestName = 'resona-update.json'
$BuildInfoName = 'resona-build.json'

# versionCode = minutes since 2024-01-01 UTC: strictly increasing, fits int32 for decades
function New-VersionInfo {
    $minutes = [int][math]::Floor(([DateTime]::UtcNow - [DateTime]::new(2024, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)).TotalMinutes)
    return [pscustomobject]@{ VersionCode = $minutes; VersionName = [DateTime]::UtcNow.ToString('yyyy.M.d.HHmm') }  # numeric parts <= 65535 (assembly version rules)
}

function Invoke-Build {
    Ensure-Keystore
    $version = New-VersionInfo
    $env:RESONA_KS_PASS = (Get-Content $PasswordPath -Raw).Trim()
    try {
        Write-Host "Building signed APK ($Configuration)..." -ForegroundColor Cyan
        & dotnet publish $ProjectPath -f net11.0-android -c $Configuration `
            "-p:ApplicationVersion=$($version.VersionCode)" `
            "-p:ApplicationDisplayVersion=$($version.VersionName)" `
            -p:AndroidKeyStore=true `
            "-p:AndroidSigningKeyStore=$KeystorePath" `
            "-p:AndroidSigningKeyAlias=$KeyAlias" `
            '-p:AndroidSigningStorePass=env:RESONA_KS_PASS' `
            '-p:AndroidSigningKeyPass=env:RESONA_KS_PASS'
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
    }
    finally {
        Remove-Item Env:RESONA_KS_PASS -ErrorAction SilentlyContinue
    }

    $apk = Get-ApkPath
    # Remembered so Publish -NoBuild can describe the APK that was last built
    $version | ConvertTo-Json | Set-Content -Path (Join-Path (Split-Path $apk) $BuildInfoName)
    Write-Host "Signed APK: $apk (versionCode $($version.VersionCode))" -ForegroundColor Green
}

function Invoke-Publish {
    if (-not $PublishDir) { throw 'Pass -PublishDir <folder> (or set RESONA_PUBLISH_DIR).' }
    if (-not (Test-Path $PublishDir)) { New-Item -ItemType Directory -Force $PublishDir | Out-Null }

    if (-not $NoBuild) { Invoke-Build }
    $apk = Get-ApkPath
    $infoPath = Join-Path (Split-Path $apk) $BuildInfoName
    if (-not (Test-Path $infoPath)) { throw "$infoPath not found; run without -NoBuild." }
    $info = Get-Content $infoPath -Raw | ConvertFrom-Json

    # Version codes come from the clock: a clock that went backwards (or a stale -NoBuild APK) would publish a build
    # the phones silently ignore as older, and replace the manifest. Refuse instead.
    $manifestPath = Join-Path $PublishDir $ManifestName
    $published = $null
    if (Test-Path $manifestPath) {
        try { $published = Get-Content $manifestPath -Raw | ConvertFrom-Json } catch { Write-Warning 'Existing manifest unreadable; ignoring.' }
    }
    if ($published -and [long]$info.VersionCode -le [long]$published.versionCode) {
        throw "versionCode $($info.VersionCode) is not newer than the published $($published.versionCode). Check the PC clock (UTC now: $([DateTime]::UtcNow.ToString('u'))) and rebuild."
    }

    $apkName = "resona-$($info.VersionCode).apk"
    $target = Join-Path $PublishDir $apkName
    Copy-Item $apk $target -Force
    $hash = (Get-FileHash $target -Algorithm SHA256).Hash.ToLowerInvariant()

    # Remember the previous release before replacing the manifest, so one rollback APK is kept
    $previousApk = if ($published) { $published.apk } else { $null }

    $manifest = [ordered]@{
        versionCode  = [int]$info.VersionCode
        versionName  = $info.VersionName
        apk          = $apkName
        sha256       = $hash
        publishedUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    # Atomic: write a temp file in the same folder, then rename over the manifest
    $temp = Join-Path $PublishDir "$ManifestName.tmp"
    $manifest | ConvertTo-Json | Set-Content -Path $temp -Encoding utf8NoBOM
    Move-Item -Path $temp -Destination $manifestPath -Force

    Get-ChildItem -Path $PublishDir -Filter 'resona-*.apk' |
        Where-Object { $_.Name -ne $apkName -and $_.Name -ne $previousApk } |
        ForEach-Object { Write-Host "Removing old $($_.Name)"; Remove-Item $_.FullName -Force }

    Write-Host "Published $apkName (versionCode $($info.VersionCode), sha256 $hash) to $PublishDir" -ForegroundColor Green
}

function Get-ApkPath {
    $path = Join-Path $RepoRoot "src/Resona.Android/bin/$Configuration/net11.0-android/publish/com.resona-Signed.apk"
    if (-not (Test-Path $path)) { throw "APK not found at $path. Run -Action Build first." }
    return $path
}

# ---------------------------------------------------------------------------------------------------------------------
# Device operations
# ---------------------------------------------------------------------------------------------------------------------

# True when this app is the device owner AND lock task (kiosk) is currently engaged.
function Test-KioskLocked {
    if (-not (Test-Provisioned)) { return $false }
    $activities = Invoke-Shell 'dumpsys activity activities'
    return ($activities -match 'mLockTaskModeState=(LOCKED|PINNED)')
}

function Install-App {
    if (-not $NoBuild) { Invoke-Build }
    $apk = Get-ApkPath

    # While lock task is active, MIUI's USB-install confirmation activity cannot come to the front, so the install is
    # auto-cancelled (INSTALL_FAILED_USER_RESTRICTED). Pause the kiosk for the install and always restore it.
    $pauseKiosk = Test-KioskLocked
    if ($pauseKiosk) {
        Write-Host 'Kiosk is locked: pausing it for the install (KioskOff -> install -> KioskOn).' -ForegroundColor Yellow
        Send-KioskCommand $ActionKioskDisable
        Start-Sleep -Seconds 2
    }
    try {
        Install-Apk $apk
    }
    finally {
        if ($pauseKiosk) {
            Write-Host 'Re-enabling kiosk.' -ForegroundColor Yellow
            Send-KioskCommand $ActionKioskEnable
        }
    }
}

function Install-Apk {
    param([Parameter(Mandatory)] [string] $apk)
    Write-Host "Installing $apk" -ForegroundColor Cyan
    $result = Invoke-Adb install -r $apk
    Write-Host $result
    if ($result -notmatch 'Success') {
        if ($result -match 'INSTALL_FAILED_USER_RESTRICTED') {
            Write-Warning 'Xiaomi/MIUI blocked the install. Enable Developer options -> "Install via USB" and "USB debugging (Security settings)", then retry.'
        }
        elseif ($result -match 'INSTALL_FAILED_UPDATE_INCOMPATIBLE|signatures do not match') {
            Write-Warning 'The installed app is signed with a different key. Uninstall it (or -Action Release if it is the device owner) and retry.'
        }
        throw 'adb install failed.'
    }
}

function Start-App {
    # Explicit launch of the activity (works whether or not the kiosk HOME alias is enabled)
    Invoke-Shell "am start -n $PackageName/$PackageName.MainActivity" | Out-Host
}

function Get-DeviceOwner {
    # On-device: everything from the first "Device Owner:" line on, then the first admin= line
    $dump = Invoke-Shell "dumpsys device_policy | sed -n '/Device Owner:/,`$p' | grep -m1 'admin=ComponentInfo'"
    if ($dump -match 'admin=ComponentInfo\{([^}]+)\}') { return $Matches[1] }
    return $null
}

function Test-Provisioned {
    return ((Get-DeviceOwner) -eq $AdminComponent)
}

# Broadcasts a command to KioskCommandReceiver. Extras are raw `am broadcast` args (quote values yourself), e.g.
# -Extras @('--es', 'url', "'$Url'"). Prints the output and warns on missing data / not-device-owner; with -Quiet nothing
# is printed and the raw output is returned instead (caller interprets it).
function Send-KioskCommand {
    param([string] $KioskAction, [string[]] $Extras = @(), [switch] $Quiet)
    $out = Invoke-Shell ((@('am broadcast', '-a', $KioskAction, '-n', $CommandComponent) + $Extras) -join ' ')
    if ($Quiet) { return $out }
    Write-Host $out
    if ($out -notmatch 'data="[^"]*"') { Write-Warning 'No result data returned from the receiver (is the app installed/updated?).' }
    elseif ($out -match 'result=2') { Write-Warning 'The app is not the device owner on this device.' }
}

# Polls (~1s, up to $TimeoutSeconds) until the kiosk is locked or the app is in the foreground, then returns.
function Wait-AppReady {
    param([int] $TimeoutSeconds = 20)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        $activities = Invoke-Shell 'dumpsys activity activities'
        if ($activities -match 'mLockTaskModeState=LOCKED' -or (Get-ForegroundActivity $activities) -like "$PackageName/*") { return }
        if ([DateTime]::UtcNow -ge $deadline) { Write-Warning 'Timed out waiting for the app to start/lock.'; return }
        Start-Sleep -Seconds 1
    }
}

function Invoke-Preflight {
    Write-Host 'Preflight checks...' -ForegroundColor Cyan
    $problems = @()

    $owner = Get-DeviceOwner
    if ($owner -and $owner -ne $AdminComponent) {
        $problems += "The device is already owned by another admin app ($owner). Factory reset or remove it first."
    }

    if ($owner -ne $AdminComponent) {
        # Accounts and extra users block set-device-owner (only relevant for a first-time provision)
        $accounts = Invoke-Shell 'dumpsys account'
        if ($accounts -match 'Accounts:\s*(\d+)' -and [int]$Matches[1] -gt 0) {
            $problems += ("The device has $($Matches[1]) account(s). Remove ALL accounts first: Settings -> Accounts (Google), " +
                'and on Xiaomi also Settings -> Mi Account -> sign out / remove. set-device-owner refuses to run with accounts present.')
        }

        $users = @((Invoke-Shell 'pm list users') -split "`n" | Where-Object { $_ -match 'UserInfo\{' })
        if ($users.Count -gt 1) {
            $problems += "The device has $($users.Count) users; remove the secondary users/work profiles/'Second space' first (device owner needs only user 0)."
        }
    }

    if ($problems.Count -gt 0) {
        $problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        throw 'Preflight failed; fix the issues above and re-run.'
    }
    Write-Host '  OK' -ForegroundColor Green
}

function Invoke-Provision {
    Invoke-Preflight

    Install-App

    if (Test-Provisioned) {
        Write-Host 'Already the device owner; skipping set-device-owner.' -ForegroundColor Yellow
    }
    else {
        $out = Invoke-Shell "dpm set-device-owner $AdminComponent"
        Write-Host $out
        if ($out -notmatch 'Success') {
            if ($out -match 'already|accounts') { Write-Warning 'Remove all accounts (including Mi Account on Xiaomi) and extra users, then retry.' }
            throw 'dpm set-device-owner failed.'
        }
    }

    # Restart so the (new) device-owner state is applied from a clean process
    Invoke-Shell "am force-stop $PackageName" | Out-Null
    Start-App
    Wait-AppReady
    Show-Status
}

function Invoke-Deploy {
    Install-App
    Start-App
    Wait-AppReady
    Show-Status
}

# Parses the foreground activity out of `dumpsys activity activities`. Formats seen:
#   API 35:        topResumedActivity=ActivityRecord{abc u0 com.resona/.MainActivity t12}
#   API 33 (MIUI): "  ResumedActivity: ActivityRecord{abc u0 com.resona/.MainActivity} t35}" (also mResumedActivity)
# and falls back to mFocusedApp when nothing is resumed in the dump.
function Get-ForegroundActivity {
    param([string] $Dump)
    $component = '\bu\d+\s+([\w.]+/[\w.$]+)'
    foreach ($key in '(?:^|\s)topResumedActivity[:=]', '(?:^|\s)m?ResumedActivity[:=]', '(?:^|\s)mFocusedApp[:=]') {
        if ($Dump -match "(?m)$key[^\n]*?$component") { return $Matches[1] }
    }
    return 'unknown'
}

function Show-Status {
    $rows = [System.Collections.Generic.List[object]]::new()
    function Add-Row($Label, $Value, $Pass) { $rows.Add([pscustomobject]@{ Label = $Label; Value = $Value; Pass = $Pass }) }

    $props = Invoke-Shell "getprop | grep -E '^\[ro\.(product\.model|build\.version\.(release|sdk))\]'"
    function Get-Prop($Name) { if ($props -match "(?m)^\[$([regex]::Escape($Name))\]: \[([^\]]*)\]") { $Matches[1] } else { '' } }
    $model = Get-Prop 'ro.product.model'
    $androidVersion = Get-Prop 'ro.build.version.release'
    $sdk = Get-Prop 'ro.build.version.sdk'
    Write-Host "Device: $model, Android $androidVersion (API $sdk), serial $($script:Serial)" -ForegroundColor Cyan

    $pkg = Invoke-Shell "dumpsys package $PackageName | grep -E 'versionName=|versionCode='"
    if ($pkg -match 'versionName=(\S+)') {
        $versionName = $Matches[1]
        $versionCode = if ($pkg -match 'versionCode=(\d+)') { $Matches[1] } else { '?' }
        Add-Row 'App installed' "$PackageName $versionName (versionCode $versionCode)" $true
    }
    else {
        Add-Row 'App installed' 'not installed' $false
    }

    # Spotify app (driven through the Spotify App Remote SDK). The kiosk's HideOtherApps leaves it visible.
    $spotifyPkg = 'com.spotify.music'
    if ((Invoke-Shell "pm list packages $spotifyPkg") -match "package:$([regex]::Escape($spotifyPkg))") {
        $spotifyHidden = (Invoke-Shell "dumpsys package $spotifyPkg | grep -E 'hidden=|suspended='") -match 'hidden=true'
        Add-Row 'Spotify app' ($spotifyHidden ? 'installed but hidden' : 'installed') (-not $spotifyHidden)
    }
    else {
        Add-Row 'Spotify app' 'not installed (only needed for Spotify playback)' $false
    }

    $owner = Get-DeviceOwner
    Add-Row 'Device owner' ($owner ?? 'none') ($owner -eq $AdminComponent)

    $updateOut = Send-KioskCommand $ActionUpdateStatus -Quiet
    if ($updateOut -match 'data="url=([^;]*);lastCheck=([^;]*);lastResult=([^;"]*)(?:;hidden=(\d+))?"') {
        $updateUrl = $Matches[1]; $lastCheck = $Matches[2]; $lastResult = $Matches[3]; $hiddenApps = $Matches[4]
        Add-Row 'Update source' $updateUrl ($updateUrl -ne '-')
        Add-Row 'Last check' "$lastCheck ($lastResult)" ($lastResult -notmatch '^(error|hash-mismatch|bad-manifest|install-failed)')
        if ($hiddenApps) { Add-Row 'Hidden apps' $hiddenApps $true }
    }
    else {
        Add-Row 'Update source' 'unknown (app does not support updates or is not device owner)' $false
    }

    $activities = Invoke-Shell 'dumpsys activity activities'
    $lockState = if ($activities -match 'mLockTaskModeState=(\w+)') { $Matches[1] } else { 'unknown' }
    Add-Row 'Lock task' $lockState ($lockState -eq 'LOCKED')

    $homeOut = (Invoke-Shell 'cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.HOME') -split "`n" | Select-Object -Last 1
    Add-Row 'Default HOME' $homeOut ($homeOut -like "$PackageName/*")

    $focus = Get-ForegroundActivity $activities
    Add-Row 'Foreground' $focus ($focus -like "$PackageName/*")

    $ipOut = Invoke-Shell 'ip -f inet addr show wlan0'
    if ($ipOut -notmatch 'inet (\d+\.\d+\.\d+\.\d+)') { $ipOut = Invoke-Shell 'ip -f inet addr' }
    $ips = [regex]::Matches($ipOut, 'inet (\d+\.\d+\.\d+\.\d+)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notlike '127.*' }
    $url = if ($ips) { "http://$($ips | Select-Object -First 1):$WebPort/" } else { 'no IP address' }
    Add-Row 'Web client' $url ([bool]$ips)

    Write-Host ''
    foreach ($row in $rows) {
        $tag = if ($row.Pass) { 'PASS' } else { 'WARN' }
        $color = if ($row.Pass) { 'Green' } else { 'Yellow' }
        Write-Host ('[{0}] {1,-14} {2}' -f $tag, $row.Label, $row.Value) -ForegroundColor $color
    }
    Write-Host ''
}

# ---------------------------------------------------------------------------------------------------------------------
# Dispatch
# ---------------------------------------------------------------------------------------------------------------------

if ($Action -eq 'Build') {
    Invoke-Build
    return
}

if ($Action -eq 'Publish') {
    Invoke-Publish
    return
}

Resolve-Device

switch ($Action) {
    'Deploy' { Invoke-Deploy }
    'Provision' { Invoke-Provision }
    'Status' { Show-Status }
    'SetUpdateSource' {
        if ($null -eq $Url) { throw 'Pass -Url <http://host/path/> (use -Url "" to disable updates).' }
        Send-KioskCommand $ActionSetUpdateSource -Extras @('--es', 'url', "'$Url'")
    }
    'CheckUpdate' { Send-KioskCommand $ActionCheckUpdate }
    'KioskOff' { Send-KioskCommand $ActionKioskDisable; Start-Sleep -Seconds 2; Show-Status }
    'KioskOn' { Send-KioskCommand $ActionKioskEnable; Wait-AppReady; Show-Status }
    'Release' {
        if (-not $Force) {
            $answer = Read-Host "This removes kiosk mode AND device-owner status from $($script:Serial). Continue? (y/N)"
            if ($answer -notmatch '^[yY]') { Write-Host 'Cancelled.'; return }
        }
        Send-KioskCommand $ActionReleaseOwner
        Start-Sleep -Seconds 2
        Show-Status
    }
    'Logs' {
        & $script:Adb -s $script:Serial logcat -s 'Resona:*' 'AndroidRuntime:E'
    }
}
