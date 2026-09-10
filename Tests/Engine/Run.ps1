param(
    [string]$GamePath = $env:RIMWORLD_DIR,
    [string]$HarmonyPath = $env:HARMONY_DIR,
    [string]$OutputPath = (Join-Path $PSScriptRoot ('../../artifacts/engine-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [switch]$Graphics,
    [switch]$Interactive
)
$ErrorActionPreference = 'Stop'
if (!$GamePath -or !(Test-Path -LiteralPath (Join-Path $GamePath 'RimWorldWin64.exe'))) { throw 'Supply -GamePath or RIMWORLD_DIR pointing to your RimWorld installation.' }
if (!$HarmonyPath -or !(Test-Path -LiteralPath $HarmonyPath)) { throw 'Supply -HarmonyPath or HARMONY_DIR pointing to the Harmony mod.' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw 'Choose a new OutputPath; existing test runtimes are never overwritten.' }
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtime = Join-Path $OutputPath 'runtime'
$config = Join-Path $OutputPath 'savedata\Config'
$probe = Join-Path $runtime 'Mods\RimCBMProbe'
foreach ($folder in @($runtime, $config, (Join-Path $probe 'About'), (Join-Path $probe 'Assemblies'))) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
}
foreach ($file in @('RimWorldWin64.exe', 'UnityPlayer.dll', 'steam_appid.txt')) {
    Copy-Item -LiteralPath (Join-Path $GamePath $file) -Destination (Join-Path $runtime $file)
}
foreach ($folder in @('Data', 'RimWorldWin64_Data', 'MonoBleedingEdge')) {
    New-Item -ItemType Junction -Path (Join-Path $runtime $folder) -Target (Join-Path $GamePath $folder) | Out-Null
}
New-Item -ItemType Junction -Path (Join-Path $runtime 'Mods\RimCBM') -Target $projectRoot | Out-Null
New-Item -ItemType Junction -Path (Join-Path $runtime 'Mods\Harmony') -Target $HarmonyPath | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\RimCBM.Probe.dll') -Destination (Join-Path $probe 'Assemblies')
Set-Content -LiteralPath (Join-Path $probe 'About\About.xml') -Encoding utf8 -Value '<ModMetaData><name>RimCBM engine probe</name><author>Local test</author><packageId>rimcbm.engineprobe</packageId><supportedVersions><li>1.6</li></supportedVersions><description>Local test only.</description></ModMetaData>'
Set-Content -LiteralPath (Join-Path $config 'ModsConfig.xml') -Encoding utf8 -Value '<ModsConfigData><version>1.6.0</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li><li>ch4acko3.rimcbm</li><li>rimcbm.engineprobe</li></activeMods><knownExpansions><li>ludeon.rimworld.royalty</li><li>ludeon.rimworld.ideology</li><li>ludeon.rimworld.biotech</li><li>ludeon.rimworld.anomaly</li><li>ludeon.rimworld.odyssey</li></knownExpansions></ModsConfigData>'
Set-Content -LiteralPath (Join-Path $config 'Prefs.xml') -Encoding utf8 -Value '<PrefsData><screenWidth>1280</screenWidth><screenHeight>900</screenHeight><fullscreen>false</fullscreen><uiScale>1</uiScale><volumeMaster>0</volumeMaster><runInBackground>true</runInBackground><langFolderName>ChineseSimplified</langFolderName><devMode>true</devMode></PrefsData>'
$env:RIMCBM_TEST_OUTPUT = $OutputPath
$env:RIMCBM_INTERACTIVE = if ($Interactive) { '1' } else { '0' }
$arguments = @('-quicktest', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '900', ('-savedatafolder=' + (Join-Path $OutputPath 'savedata')), '-logFile', (Join-Path $OutputPath 'Player.log'))
if (-not $Graphics -and -not $Interactive) { $arguments = @('-batchmode') + $arguments }
$windowStyle = if ($Interactive) { 'Normal' } else { 'Hidden' }
$process = Start-Process -FilePath (Join-Path $runtime 'RimWorldWin64.exe') -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle $windowStyle -PassThru
[pscustomobject]@{ ProcessId = $process.Id; OutputPath = $OutputPath } | ConvertTo-Json
