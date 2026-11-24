<#
.SYNOPSIS
Build and stage the personal fork's Windows x64 native distribution.
.DESCRIPTION
Run on the build machine with .NET 10 SDK and Visual Studio C++ Build Tools.
Only the bridge, updater, default configuration and checksummed provenance are
copied into dist/win-x64. Consumer machines need none of the build tools.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') {
    throw 'The win-x64 Native AOT distribution must be built on Windows.'
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$publishDirectory = Join-Path $repoRoot 'publish/dist-win-x64'
$distributionDirectory = Join-Path $repoRoot 'dist/win-x64'
$installerDirectory = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer'
$vswhere = Join-Path $installerDirectory 'vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    throw 'Install Visual Studio Build Tools with Desktop development with C++ before updating dist.'
}
$vsInstall = & $vswhere -latest -products '*' `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $vsInstall) {
    throw 'No Visual Studio installation with x64 C++ tools was found.'
}
$vsDevCmd = Join-Path $vsInstall 'Common7/Tools/VsDevCmd.bat'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

Push-Location $repoRoot
try {
    $sourceCommit = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }
    $uncommittedSource = @(git status --porcelain -- src global.json Directory.Build.props Directory.Packages.props)
    if ($uncommittedSource.Count -gt 0) {
        throw 'Commit runtime source and dependency changes before packaging, so build-info identifies their source.'
    }
    $sdkVersion = dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Install the .NET SDK selected by global.json.' }
    New-Item -ItemType Directory -Path $publishDirectory, $distributionDirectory -Force | Out-Null

    $buildCommand = 'call "' + $vsDevCmd + '" -arch=x64 -host_arch=x64 && ' +
        'dotnet publish "' + (Join-Path $repoRoot 'src/CopilotBridge.Cli') +
        '" -c Release -r win-x64 -o "' + $publishDirectory + '" && ' +
        'dotnet publish "' + (Join-Path $repoRoot 'src/CopilotBridge.Updater') +
        '" -c Release -r win-x64 -o "' + $publishDirectory + '"'
    $previousPath = $env:Path
    try {
        # VsDevCmd does not add the Installer directory; ILC invokes vswhere by name.
        $env:Path += ';' + $installerDirectory
        & $env:ComSpec /d /c $buildCommand
        if ($LASTEXITCODE -ne 0) { throw 'Native publishing failed; dist payload was not replaced.' }
    }
    finally {
        $env:Path = $previousPath
    }

    $payload = @('copilot-bridge.exe', 'copilot-updater.exe', 'appsettings.json')
    foreach ($name in $payload) {
        $source = Join-Path $publishDirectory $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Native publishing did not produce $name."
        }
    }
    foreach ($name in $payload) {
        Copy-Item -LiteralPath (Join-Path $publishDirectory $name) `
            -Destination (Join-Path $distributionDirectory $name) -Force
    }
    $version = & (Join-Path $distributionDirectory 'copilot-bridge.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'The staged native bridge did not start.' }

    $files = @($payload | ForEach-Object {
        $path = Join-Path $distributionDirectory $_
        [ordered]@{
            name = $_
            bytes = (Get-Item -LiteralPath $path).Length
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    $buildInfo = [ordered]@{
        schema_version = 1
        runtime_identifier = 'win-x64'
        source_commit = $sourceCommit
        sdk_version = $sdkVersion
        product_version = "$version".Trim()
        packaged_at_utc = [DateTime]::UtcNow.ToString('o')
        files = $files
    }
    $buildInfoPath = Join-Path $distributionDirectory 'build-info.json'
    [IO.File]::WriteAllText($buildInfoPath, ($buildInfo | ConvertTo-Json -Depth 6) + "`n", $utf8NoBom)
    $checksums = @(@($payload) + @('build-info.json') | ForEach-Object {
        $digest = (Get-FileHash -LiteralPath (Join-Path $distributionDirectory $_) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$digest  $_"
    })
    [IO.File]::WriteAllText((Join-Path $distributionDirectory 'SHA256SUMS'),
        ($checksums -join "`n") + "`n", $utf8NoBom)
    Write-Host 'Native distribution staged. Run real-client verification against dist/win-x64 before committing it.'
    git status --short -- dist
}
finally {
    Pop-Location
}
