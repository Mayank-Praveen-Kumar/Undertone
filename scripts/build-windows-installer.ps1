#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$PublishDirectory,
    [string]$CompilerPath,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$PublishDirectory) { $PublishDirectory = Join-Path $repoRoot 'publish/win-x64' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'installer-output' }
$PublishDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

# The installer must contain a complete self-contained player, including native audio.
foreach ($relativePath in @('Undertone.exe', 'Undertone.dll', 'Undertone.deps.json',
        'Undertone.runtimeconfig.json', 'coreclr.dll', 'libvlc/win-x64/libvlc.dll',
        'libvlc/win-x64/libvlccore.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $PublishDirectory $relativePath) -PathType Leaf)) {
        throw "Incomplete Windows publish output: missing $relativePath"
    }
}
if (!(Get-ChildItem -LiteralPath (Join-Path $PublishDirectory 'libvlc/win-x64/plugins') -Filter '*.dll' -Recurse -File)) {
    throw 'Incomplete Windows publish output: VLC audio plugins are missing.'
}
[xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Undertone/Undertone.csproj') -Raw
$version = [string]($project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'The project needs a numeric installer version.' }
$exeVersion = (Get-Item -LiteralPath (Join-Path $PublishDirectory 'Undertone.exe')).VersionInfo
if ($exeVersion.FileMajorPart -ne ([version]$version).Major -or
    $exeVersion.FileMinorPart -ne ([version]$version).Minor -or
    $exeVersion.FileBuildPart -ne ([version]$version).Build) {
    throw "The published player does not match project version $version. Publish it again first."
}

if (!$CompilerPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $CompilerPath = $command.Source }
    else {
        $candidates = foreach ($base in @(${env:ProgramFiles}, ${env:ProgramFiles(x86)}, "$env:LOCALAPPDATA/Programs")) {
            foreach ($edition in @('Inno Setup 7', 'Inno Setup 6')) { Join-Path $base "$edition/ISCC.exe" }
        }
        $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}
if (!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Install Inno Setup 6.5 or newer, or pass -CompilerPath pointing to ISCC.exe.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $CompilerPath "/DMyAppVersion=$version" "/DPublishDir=$PublishDirectory" "/O$OutputDirectory" (Join-Path $repoRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed with exit code $LASTEXITCODE." }
$installer = Join-Path $OutputDirectory "Undertone-v$version-Setup.exe"
if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'The compiler did not create the expected installer.' }
Get-FileHash -LiteralPath $installer -Algorithm SHA256 | Format-List
Write-Output "Installer ready: $installer"
