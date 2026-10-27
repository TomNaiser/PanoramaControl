param(
    [string]$OutputDirectory = '',
    [ValidatePattern('^COM[0-9]+$')][string]$DefaultPort = 'COM4',
    [string]$MotionProfile = ''
)

$ErrorActionPreference = 'Stop'
$repoDirectory = Split-Path -Parent $PSScriptRoot
$profilePath = $null
$profileRevision = 39
$packageName = '27.5.4'
if ($MotionProfile) {
    $profilePath = (Resolve-Path -LiteralPath $MotionProfile).Path
    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    if ($profile.schemaVersion -ne 1 -or $profile.firmware -ne '2.4.75' -or $profile.baseRevision -notin @(39, 40) -or
        @($profile.parameters.PSObject.Properties).Count -ne 129 -or
        $profile.parameters.'control.travelStartBrakeLeadSeconds'.value -ne 0.110 -or
        $profile.parameters.'tilt.fineApproachEnabled'.value -ne 0) {
        throw 'Das beigelegte Referenzprofil muss der geprüften Firmware 2.4.75 / Revision 39 oder 40 entsprechen.'
    }
    $profileRevision = [int]$profile.baseRevision
    $panEnter = if ($profileRevision -eq 40) { 0.15 } else { 0.30 }
    $panExit = if ($profileRevision -eq 40) { 0.25 } else { 0.40 }
    if ($profile.parameters.'pan.positionToleranceDeg'.value -ne $panEnter -or
        $profile.parameters.'pan.holdExitToleranceDeg'.value -ne $panExit) {
        throw 'Pan-Fenster stimmt nicht mit der angegebenen Referenzrevision überein.'
    }
    if ($profileRevision -eq 40) { $packageName = '27.5.4-pan-r40' }
}
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoDirectory ('releases/' + $packageName) }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

function Run-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}

Run-Checked 'dotnet' @('run', '--project', (Join-Path $PSScriptRoot 'ProtocolTests/ProtocolTests.csproj'))
Run-Checked 'dotnet' @('msbuild', (Join-Path $repoDirectory 'PanoramaControl/PanoramaControl Version 27.csproj'), '/t:Build', '/p:Configuration=Release', '/p:GenerateManifests=false', '/v:minimal', '/nologo')
Run-Checked 'dotnet' @('msbuild', (Join-Path $PSScriptRoot 'WindowsSmoke/WindowsSmoke.csproj'), '/t:Build', '/v:minimal', '/nologo')
Run-Checked (Join-Path $PSScriptRoot 'WindowsSmoke/bin/WindowsSmoke.exe') @((Join-Path $PSScriptRoot 'WindowsSmoke/artifacts'))
Run-Checked (Join-Path $PSScriptRoot 'WindowsSmoke/bin/WindowsSmoke.exe') @('preview-repro', (Join-Path $PSScriptRoot 'WindowsSmoke/artifacts/preview-repro'))
Run-Checked (Join-Path $PSScriptRoot 'WindowsSmoke/bin/WindowsSmoke.exe') @('navigation-repro', (Join-Path $PSScriptRoot 'WindowsSmoke/artifacts/navigation'))
Run-Checked (Join-Path $PSScriptRoot 'WindowsSmoke/bin/WindowsSmoke.exe') @('touch-repro', (Join-Path $PSScriptRoot 'WindowsSmoke/artifacts/touch'))

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
Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/WINDOWS-27.5.4-PAN-VERGLEICH.md') -Destination (Join-Path $OutputDirectory 'ABNAHME.md') -Force
if ($profileRevision -eq 40) {
    Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/WINDOWS-27.5.4-PAN-KORREKTUR.md') -Destination (Join-Path $OutputDirectory 'PAN-KORREKTUR.md') -Force
}
Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/history/WINDOWS-27.5.2-SURFACE-TOUCH.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/history/WINDOWS-27.5.0-ANDROID-PARITAET.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/history/WINDOWS-27.4.2-BEDIENUNG.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/history/WINDOWS-27.4.1-ABGLEICH.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repoDirectory 'docs/history/WINDOWS-27.4.0-ABNAHME.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WindowsSmoke/RELEASE-README.txt') -Destination (Join-Path $OutputDirectory 'README.txt') -Force
$reference = [ordered]@{
    windowsVersion = '27.5.4'
    androidVersion = '0.6.27'
    androidVersionCode = 40
    baselineCommit = '665f0dc'
    firmwareVersion = '2.4.75'
    firmwareSourceCommit = '55ff4bb9e74085bacb71bb94783856b67a685f55'
    referenceConfigRevision = $profileRevision
    startBrakeLeadSeconds = 0.110
    profileAppliedAutomatically = $false
    motionProfile = $null
}
if ($profilePath) {
    $profileName = [System.IO.Path]::GetFileName($profilePath)
    $destination = Join-Path $OutputDirectory $profileName
    Copy-Item -LiteralPath $profilePath -Destination $destination -Force
    $reference.motionProfile = [ordered]@{ file = $profileName; sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash }
}
$reference | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'REFERENCE.json') -Encoding utf8
$zipPath = Join-Path (Split-Path -Parent $OutputDirectory) ('PanoramaControl-' + $packageName + '.zip')
Compress-Archive -Path (Join-Path $OutputDirectory '*') -DestinationPath $zipPath -Force
Write-Output "Windows-Paket: $OutputDirectory"
Write-Output "ZIP: $zipPath"
