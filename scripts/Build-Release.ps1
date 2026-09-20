[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$OutputDirectory,
    [string]$SigningThumbprint,
    # CI dry run: publish and build the MSI without signing. The output is named
    # so it cannot be mistaken for a release asset and no update manifest is written.
    [switch]$Unsigned,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repositoryRoot 'artifacts' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
# A unique work directory avoids removing unrelated artifacts or reusing stale publish output.
$workDirectory = Join-Path $OutputDirectory ('build-' + [guid]::NewGuid().ToString('N'))
$publishDirectory = Join-Path $workDirectory 'publish'
$toolsDirectory = Join-Path $workDirectory 'tools'
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
$project = Join-Path $repositoryRoot 'src\CopyGIF.App\CopyGIF.App.csproj'
if ($Unsigned -and -not [string]::IsNullOrWhiteSpace($SigningThumbprint)) { throw 'Use either -Unsigned or -SigningThumbprint, not both.' }
$assetName = if ($Unsigned) { "CopyGIF-$Version-win-x64-UNSIGNED-DRYRUN.msi" } else { "CopyGIF-$Version-win-x64.msi" }
$msiPath = Join-Path $OutputDirectory $assetName
if (Test-Path -LiteralPath $msiPath) { throw "An artifact already exists at $msiPath. Choose a new output directory." }
if (-not $Unsigned) {
    if ([string]::IsNullOrWhiteSpace($SigningThumbprint)) {
        throw 'A code-signing certificate in Cert:\CurrentUser\My is required. Supply -SigningThumbprint. An unsigned build cannot participate in trusted updates. Use -Unsigned only for a CI dry run.'
    }
    $signTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
    if ($null -eq $signTool) { throw 'Install the Windows SDK signing tools in Visual Studio Installer.' }
}
function Invoke-Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "An external build command failed with exit code $LASTEXITCODE." }
}
Push-Location $repositoryRoot
try {
    Invoke-Checked { dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false `
        -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false `
        "-p:Version=$Version" "-p:AssemblyVersion=$Version.0" "-p:FileVersion=$Version.0" `
        -o $publishDirectory }
    foreach ($name in @('LICENSE.txt','PRIVACY.md','THIRD-PARTY-NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination $publishDirectory
    }
    $exe = Join-Path $publishDirectory 'CopyGIF.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw 'The WinUI publish did not produce CopyGIF.exe.' }
    if (-not $Unsigned) {
        Invoke-Checked { & $signTool.FullName sign /sha1 $SigningThumbprint /fd SHA256 /tr $TimestampServer /td SHA256 $exe }
        Invoke-Checked { & $signTool.FullName verify /pa $exe }
    }
    Invoke-Checked { dotnet tool install wix --version 6.0.0 --allow-roll-forward --tool-path $toolsDirectory }
    $wix = Join-Path $toolsDirectory 'wix.exe'
    Invoke-Checked { & $wix build (Join-Path $repositoryRoot 'Installer\CopyGIF.wxs') -arch x64 `
        -sice ICE38 -sice ICE64 -sice ICE91 `
        -d "Version=$Version" -d "PublishDir=$publishDirectory" -o $msiPath }
    if ($Unsigned) {
        if (-not (Test-Path -LiteralPath $msiPath)) { throw 'The dry run did not produce an MSI.' }
        Write-Output "Dry run OK: built unsigned $msiPath. Not a release asset; no manifest was written."
        return
    }
    Invoke-Checked { & $signTool.FullName sign /sha1 $SigningThumbprint /fd SHA256 /tr $TimestampServer /td SHA256 $msiPath }
    Invoke-Checked { & $signTool.FullName verify /pa $msiPath }
    $exeSignature = Get-AuthenticodeSignature -LiteralPath $exe
    $msiSignature = Get-AuthenticodeSignature -LiteralPath $msiPath
    if ($exeSignature.Status -ne 'Valid' -or $msiSignature.Status -ne 'Valid' -or
        $exeSignature.SignerCertificate.Thumbprint -ne $msiSignature.SignerCertificate.Thumbprint) {
        throw 'The executable and MSI must have valid signatures from the same certificate.'
    }
    $hash = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $releaseBase = "https://github.com/hphifer99/CopyGIF/releases"
    $manifest = [ordered]@{
        schemaVersion = 1; version = $Version; channel = 'stable'; assetName = $assetName
        assetUri = "$releaseBase/download/v$Version/$assetName"
        sizeBytes = (Get-Item -LiteralPath $msiPath).Length; sha256 = $hash
        minimumSupportedVersion = '2.0.0'; releaseNotesUri = "$releaseBase/tag/v$Version"
        publishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'CopyGIF-update.json') -Encoding utf8
    "$hash  $assetName" | Set-Content -LiteralPath (Join-Path $OutputDirectory 'CopyGIF-win-x64.sha256') -Encoding ascii
    Write-Output "Created $msiPath and CopyGIF-update.json. Install and test the MSI before publishing."
}
finally { Pop-Location }
