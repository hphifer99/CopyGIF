[CmdletBinding()]
param(
    [switch]$RemoveUserData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$localApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)

$applicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::ApplicationData)

if ([string]::IsNullOrWhiteSpace($localApplicationData) -or
    [string]::IsNullOrWhiteSpace($applicationData)) {
    throw "Windows user profile directories could not be resolved."
}

$installDirectory = Join-Path $localApplicationData "Programs\CopyGIF"
$installedExecutable = Join-Path $installDirectory "CopyGIF.exe"
$shortcutPath = Join-Path `
    $applicationData `
    "Microsoft\Windows\Start Menu\Programs\CopyGIF.lnk"

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

foreach ($process in $runningInstalledProcesses) {
    Stop-Process -Id $process.Id -Force
}

$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

if (Test-Path -LiteralPath $runKeyPath) {
    Remove-ItemProperty `
        -LiteralPath $runKeyPath `
        -Name "CopyGIF" `
        -ErrorAction SilentlyContinue
}

$uninstallKeyPath =
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CopyGIF"

if (Test-Path -LiteralPath $uninstallKeyPath) {
    Remove-Item -LiteralPath $uninstallKeyPath -Recurse -Force
}

if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

$currentDirectory = [IO.Path]::GetFullPath((Get-Location).Path)
$installDirectoryFullPath = [IO.Path]::GetFullPath($installDirectory)

if ($currentDirectory.StartsWith(
        $installDirectoryFullPath,
        [StringComparison]::OrdinalIgnoreCase)) {
    Set-Location -LiteralPath ([IO.Path]::GetTempPath())
}

if (Test-Path -LiteralPath $installDirectory) {
    Remove-Item -LiteralPath $installDirectory -Recurse -Force
}

if ($RemoveUserData) {
    $userDataDirectory = Join-Path $localApplicationData "CopyGIF"
    $temporaryDataDirectory = Join-Path ([IO.Path]::GetTempPath()) "CopyGIF"

    foreach ($directory in @($userDataDirectory, $temporaryDataDirectory)) {
        if (Test-Path -LiteralPath $directory) {
            Remove-Item -LiteralPath $directory -Recurse -Force
        }
    }

    Write-Output "CopyGIF and its local user data were removed."
}
else {
    Write-Output "CopyGIF was removed. Local user data was preserved in $localApplicationData\CopyGIF"
}
