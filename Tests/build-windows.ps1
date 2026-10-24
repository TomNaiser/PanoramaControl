param(
    [string]$OutputDirectory = '',
    [ValidatePattern('^COM[0-9]+$')][string]$DefaultPort = 'COM4'
)

$ErrorActionPreference = 'Stop'
$repoDirectory = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoDirectory 'releases/27.4.4' }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

function Run-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}

Run-Checked 'dotnet' @('run', '--project', (Join-Path $PSScriptRoot 'ProtocolTests/ProtocolTests.csproj'))
Run-Checked 'dotnet' @('msbuild', (Join-Path $repoDirectory 'PanoramaControl/PanoramaControl Version 27.csproj'), '/t:Build', '/p:Configuration=Release', '/p:GenerateManifests=false', '/v:minimal', '/nologo')
Run-Checked 'dotnet' @('msbuild', (Join-Path $PSScriptRoot 'WindowsSmoke/WindowsSmoke.csproj'), '/t:Build', '/v:minimal', '/nologo')
Run-Checked (Join-Path $PSScriptRoot 'WindowsSmoke/bin/WindowsSmoke.exe') @((Join-Path $PSScriptRoot 'WindowsSmoke/artifacts'))
Run-Checked (Join-Path $PSScriptRoot 'WindowsSmoke/bin/WindowsSmoke.exe') @('preview-repro', (Join-Path $PSScriptRoot 'WindowsSmoke/preview-repro'))

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$buildDirectory = Join-Path $repoDirectory 'PanoramaControl/bin/Release'
foreach ($name in @('PanoramaControl.exe', 'PanoramaControl.exe.config', 'PanoramaControl.pdb')) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory $name) -Destination (Join-Path $OutputDirectory $name) -Force
}
$configPath = Join-Path $OutputDirectory 'PanoControlConfig.xml'
if (!(Test-Path -LiteralPath $configPath)) {
    [xml]$configuration = Get-Content -LiteralPath (Join-Path $repoDirectory 'PanoControlConfig.xml') -Raw
    $configuration.configuration.appSettings.COMPort = $DefaultPort
    $configuration.Save($configPath)
}
Copy-Item -LiteralPath (Join-Path $repoDirectory 'WINDOWS-27.4.4-ROBUSTHEIT.md') -Destination (Join-Path $OutputDirectory 'ABNAHME.md') -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'WINDOWS-27.4.2-BEDIENUNG.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'WINDOWS-27.4.1-ABGLEICH.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'WINDOWS-27.4.0-ABNAHME.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WindowsSmoke/RELEASE-README.txt') -Destination (Join-Path $OutputDirectory 'README.txt') -Force
$zipPath = Join-Path (Split-Path -Parent $OutputDirectory) 'PanoramaControl-27.4.4.zip'
Compress-Archive -Path (Join-Path $OutputDirectory '*') -DestinationPath $zipPath -Force
Write-Output "Windows-Paket: $OutputDirectory"
Write-Output "ZIP: $zipPath"
