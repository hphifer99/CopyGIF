[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string]$Version,

    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot ".."))

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts"
}

$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$projectPath = Join-Path $repositoryRoot "CopyGIF\CopyGIF.csproj"
$packagesConfigPath = Join-Path $repositoryRoot "CopyGIF\packages.config"
$packagesDirectory = Join-Path $repositoryRoot "packages"
$assemblyInfoPath = Join-Path $repositoryRoot "CopyGIF\Properties\AssemblyInfo.cs"
$buildOutputDirectory = Join-Path $repositoryRoot "CopyGIF\bin\$Configuration"
$packageDirectory = Join-Path $OutputDirectory "CopyGIF-win-x64"
$zipPath = Join-Path $OutputDirectory "CopyGIF-win-x64.zip"
$checksumPath = Join-Path $OutputDirectory "CopyGIF-win-x64.sha256"

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "CopyGIF.csproj was not found at $projectPath"
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $expectedAttribute =
        '[assembly: AssemblyInformationalVersion("{0}")]' -f $Version

    $versionMatch = Select-String `
        -LiteralPath $assemblyInfoPath `
        -SimpleMatch `
        -Pattern $expectedAttribute

    if ($null -eq $versionMatch) {
        throw "The requested version $Version does not match AssemblyInformationalVersion."
    }
}

if ($null -eq (Get-Command nuget.exe -ErrorAction SilentlyContinue)) {
    throw "nuget.exe is required and was not found on PATH."
}

if ($null -eq (Get-Command msbuild.exe -ErrorAction SilentlyContinue)) {
    throw "msbuild.exe is required and was not found on PATH."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Write-Output "Restoring NuGet packages..."
& nuget.exe restore $packagesConfigPath `
    -PackagesDirectory $packagesDirectory `
    -NonInteractive

if ($LASTEXITCODE -ne 0) {
    throw "NuGet restore failed with exit code $LASTEXITCODE."
}

Write-Output "Building CopyGIF $Configuration..."
& msbuild.exe $projectPath `
    /m `
    /t:Rebuild `
    "/p:Configuration=$Configuration" `
    /p:Platform=AnyCPU `
    /p:ContinuousIntegrationBuild=true `
    /p:DeterministicSourcePaths=false `
    /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

$requiredBuildFiles = @(
    "CopyGIF.exe",
    "CopyGIF.exe.config",
    "XamlAnimatedGif.dll"
)

foreach ($fileName in $requiredBuildFiles) {
    $sourcePath = Join-Path $buildOutputDirectory $fileName

    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "The build did not produce the required file $sourcePath"
    }
}

if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}

foreach ($filePath in @($zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $filePath) {
        Remove-Item -LiteralPath $filePath -Force
    }
}

New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

foreach ($fileName in $requiredBuildFiles) {
    Copy-Item `
        -LiteralPath (Join-Path $buildOutputDirectory $fileName) `
        -Destination $packageDirectory
}

$requiredDistributionFiles = @(
    "LICENSE.txt",
    "README.md",
    "PRIVACY.md",
    "THIRD-PARTY-NOTICES.md",
    "uninstall.ps1"
)

foreach ($relativePath in $requiredDistributionFiles) {
    $sourcePath = Join-Path $repositoryRoot $relativePath

    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "The required distribution file $relativePath was not found."
    }

    Copy-Item -LiteralPath $sourcePath -Destination $packageDirectory
}

$licensesSource = Join-Path $repositoryRoot "licenses"
$licensesDestination = Join-Path $packageDirectory "licenses"

if (-not (Test-Path -LiteralPath $licensesSource -PathType Container)) {
    throw "The licenses directory was not found."
}

Copy-Item `
    -LiteralPath $licensesSource `
    -Destination $licensesDestination `
    -Recurse

Write-Output "Creating release archive..."
Compress-Archive `
    -Path (Join-Path $packageDirectory "*") `
    -DestinationPath $zipPath `
    -CompressionLevel Optimal

$releaseHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$checksumLine = "{0}  {1}`r`n" -f $releaseHash.Hash, (Split-Path $zipPath -Leaf)
Set-Content -LiteralPath $checksumPath -Value $checksumLine -Encoding ASCII -NoNewline

Write-Output "Release package created:"
Write-Output "  $zipPath"
Write-Output "  $checksumPath"
