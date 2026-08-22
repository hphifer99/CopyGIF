[CmdletBinding()]
param(
    [string]$Repository = "hphifer99/CopyGIF",
    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[Net.ServicePointManager]::SecurityProtocol =
    [Net.ServicePointManager]::SecurityProtocol -bor
    [Net.SecurityProtocolType]::Tls12

$releaseApiUrl = "https://api.github.com/repos/$Repository/releases/latest"
$headers = @{
    Accept = "application/vnd.github+json"
    "User-Agent" = "CopyGIF-Installer"
}

$localApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)

$applicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::ApplicationData)

if ([string]::IsNullOrWhiteSpace($localApplicationData) -or
    [string]::IsNullOrWhiteSpace($applicationData)) {
    throw "Windows user profile directories could not be resolved."
}

$installDirectory = Join-Path $localApplicationData "Programs\CopyGIF"
$installParent = Split-Path $installDirectory -Parent
$startMenuDirectory = Join-Path $applicationData "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDirectory "CopyGIF.lnk"
$temporaryDirectory = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ("CopyGIF-Install-" + [Guid]::NewGuid().ToString("N"))

$zipPath = Join-Path $temporaryDirectory "CopyGIF-win-x64.zip"
$checksumPath = Join-Path $temporaryDirectory "CopyGIF-win-x64.sha256"
$payloadDirectory = Join-Path $temporaryDirectory "payload"
$stagingDirectory = Join-Path `
    $installParent `
    ("CopyGIF.install." + [Guid]::NewGuid().ToString("N"))

$backupDirectory = $null
$installationCompleted = $false

try {
    Write-Output "Finding the latest CopyGIF release..."
    $release = Invoke-RestMethod `
        -Uri $releaseApiUrl `
        -Headers $headers `
        -Method Get

    $zipAssets = @(
        $release.assets |
            Where-Object { $_.name -eq "CopyGIF-win-x64.zip" }
    )

    $checksumAssets = @(
        $release.assets |
            Where-Object { $_.name -eq "CopyGIF-win-x64.sha256" }
    )

    if ($zipAssets.Count -ne 1 -or $checksumAssets.Count -ne 1) {
        throw "The latest release does not contain exactly one ZIP and checksum asset."
    }

    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $payloadDirectory -Force | Out-Null

    Write-Output "Downloading $($release.tag_name)..."
    Invoke-WebRequest `
        -Uri $zipAssets[0].browser_download_url `
        -Headers $headers `
        -OutFile $zipPath `
        -UseBasicParsing

    Invoke-WebRequest `
        -Uri $checksumAssets[0].browser_download_url `
        -Headers $headers `
        -OutFile $checksumPath `
        -UseBasicParsing

    $checksumText = [IO.File]::ReadAllText($checksumPath)
    $checksumMatch = [Text.RegularExpressions.Regex]::Match(
        $checksumText,
        "(?i)\b[0-9a-f]{64}\b")

    if (-not $checksumMatch.Success) {
        throw "The release checksum file is invalid."
    }

    $expectedHash = $checksumMatch.Value.ToUpperInvariant()
    $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

    if (-not [string]::Equals(
            $expectedHash,
            $actualHash,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "The downloaded ZIP failed SHA-256 verification."
    }

    Write-Output "Checksum verified."
    Expand-Archive -LiteralPath $zipPath -DestinationPath $payloadDirectory -Force

    $requiredFiles = @(
        "CopyGIF.exe",
        "CopyGIF.exe.config",
        "XamlAnimatedGif.dll",
        "LICENSE.txt",
        "THIRD-PARTY-NOTICES.md"
    )

    foreach ($fileName in $requiredFiles) {
        $payloadPath = Join-Path $payloadDirectory $fileName

        if (-not (Test-Path -LiteralPath $payloadPath -PathType Leaf)) {
            throw "The release payload is missing $fileName."
        }
    }

    $installedExecutable = Join-Path $installDirectory "CopyGIF.exe"
    $runningInstalledProcesses = @(
        Get-Process -Name "CopyGIF" -ErrorAction SilentlyContinue |
            Where-Object {
                try {
                    [string]::Equals(
                        [IO.Path]::GetFullPath($_.Path),
                        [IO.Path]::GetFullPath($installedExecutable),
                        [StringComparison]::OrdinalIgnoreCase)
                }
                catch {
                    $false
                }
            }
    )

    if ($runningInstalledProcesses.Count -gt 0) {
        throw "CopyGIF is running from the install directory. Exit it from the notification area, then run the installer again."
    }

    New-Item -ItemType Directory -Path $installParent -Force | Out-Null

    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
    Copy-Item `
        -Path (Join-Path $payloadDirectory "*") `
        -Destination $stagingDirectory `
        -Recurse `
        -Force

    if (Test-Path -LiteralPath $installDirectory) {
        $backupDirectory = Join-Path `
            $installParent `
            ("CopyGIF.backup." + [Guid]::NewGuid().ToString("N"))

        Move-Item -LiteralPath $installDirectory -Destination $backupDirectory
    }

    try {
        Move-Item -LiteralPath $stagingDirectory -Destination $installDirectory
    }
    catch {
        if ($null -ne $backupDirectory -and
            (Test-Path -LiteralPath $backupDirectory) -and
            -not (Test-Path -LiteralPath $installDirectory)) {
            Move-Item -LiteralPath $backupDirectory -Destination $installDirectory
            $backupDirectory = $null
        }

        throw
    }

    $installationCompleted = $true

    if ($null -ne $backupDirectory -and
        (Test-Path -LiteralPath $backupDirectory)) {
        Remove-Item `
            -LiteralPath $backupDirectory `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }

    $uninstallKeyPath =
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CopyGIF"

    $releaseVersion = if ($release.tag_name -match '^v(.+)$') {
        $Matches[1]
    }
    else {
        [string]$release.tag_name
    }

    $uninstallScript = Join-Path $installDirectory "uninstall.ps1"
    $uninstallCommand =
        'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f
        $uninstallScript

    New-Item -Path $uninstallKeyPath -Force | Out-Null

    $uninstallStringValues = @{
        DisplayName = "CopyGIF"
        DisplayVersion = $releaseVersion
        Publisher = "hphifer99"
        DisplayIcon = (Join-Path $installDirectory "CopyGIF.exe")
        InstallLocation = $installDirectory
        UninstallString = $uninstallCommand
        QuietUninstallString = $uninstallCommand
        URLInfoAbout = "https://github.com/$Repository"
    }

    foreach ($valueName in $uninstallStringValues.Keys) {
        New-ItemProperty `
            -Path $uninstallKeyPath `
            -Name $valueName `
            -Value $uninstallStringValues[$valueName] `
            -PropertyType String `
            -Force | Out-Null
    }

    foreach ($valueName in @("NoModify", "NoRepair")) {
        New-ItemProperty `
            -Path $uninstallKeyPath `
            -Name $valueName `
            -Value 1 `
            -PropertyType DWord `
            -Force | Out-Null
    }

    New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $null

    try {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = Join-Path $installDirectory "CopyGIF.exe"
        $shortcut.WorkingDirectory = $installDirectory
        $shortcut.IconLocation = (Join-Path $installDirectory "CopyGIF.exe") + ",0"
        $shortcut.Description = "Open CopyGIF"
        $shortcut.Save()
    }
    finally {
        if ($null -ne $shortcut) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
        }

        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }

    Write-Output "CopyGIF installed to $installDirectory"

    if (-not $NoLaunch) {
        Start-Process -FilePath (Join-Path $installDirectory "CopyGIF.exe")
    }
}
catch {
    if (-not $installationCompleted -and
        $null -ne $backupDirectory -and
        (Test-Path -LiteralPath $backupDirectory) -and
        -not (Test-Path -LiteralPath $installDirectory)) {
        Move-Item -LiteralPath $backupDirectory -Destination $installDirectory
        $backupDirectory = $null
    }

    throw
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item `
            -LiteralPath $stagingDirectory `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item `
            -LiteralPath $temporaryDirectory `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
