[CmdletBinding()]param([switch]$Force)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$manifestRoot=Join-Path $root 'runtime\copilot'
$versions=Get-Content -LiteralPath (Join-Path $manifestRoot 'versions.json') -Raw | ConvertFrom-Json
$dest=Join-Path $root 'outputs\ScreenText\copilot-runtime'
function Matches([string]$Path,[string]$Hash){return (Test-Path -LiteralPath $Path) -and (Get-FileHash -LiteralPath $Path).Hash -eq $Hash}
$sdkManifest=Join-Path $dest 'node_modules\@github\copilot-sdk\package.json'
$ready=(Matches (Join-Path $dest 'node.exe') $versions.nodeSha256) -and (Matches (Join-Path $dest 'copilot.exe') $versions.cliSha256) -and (Test-Path -LiteralPath $sdkManifest)
if($ready){$ready=(Get-Content -LiteralPath $sdkManifest -Raw | ConvertFrom-Json).version -eq $versions.sdk}
$zodManifest=Join-Path $dest 'node_modules\zod\package.json'
if($ready){$ready=(Test-Path -LiteralPath $zodManifest) -and (Get-Content -LiteralPath $zodManifest -Raw | ConvertFrom-Json).version -eq $versions.zod}
if($ready -and -not $Force){foreach($name in @('package.json','package-lock.json','versions.json')){Copy-Item -LiteralPath (Join-Path $manifestRoot $name) -Destination (Join-Path $dest $name) -Force};return}
$stageRoot=Join-Path $root 'work\.runtime-runs';$stage=Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try{
 $node=Join-Path $stage 'node.exe'
 Invoke-WebRequest -UseBasicParsing -Uri ('https://nodejs.org/dist/v'+$versions.node+'/win-x64/node.exe') -OutFile $node
 if(-not(Matches $node $versions.nodeSha256)){throw 'Node release hash mismatch'}
 $zip=Join-Path $stage 'copilot.zip'
 Invoke-WebRequest -UseBasicParsing -Uri ('https://github.com/github/copilot-cli/releases/download/v'+$versions.cli+'/copilot-win32-x64.zip') -OutFile $zip
 if(-not(Matches $zip $versions.cliZipSha256)){throw 'Copilot release archive hash mismatch'}
 Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $stage 'cli')
 if(-not(Matches (Join-Path $stage 'cli\copilot.exe') $versions.cliSha256)){throw 'Copilot executable hash mismatch'}
 foreach($name in @('package.json','package-lock.json','versions.json')){Copy-Item -LiteralPath (Join-Path $manifestRoot $name) -Destination (Join-Path $stage $name)}
 $npmJs=& (Join-Path $PSScriptRoot 'bootstrap-npm.ps1') -Root $root
 $config=Join-Path $stage '.npmrc';$globalConfig=Join-Path $stage '.npmrc-global';[IO.File]::WriteAllText($config,'');[IO.File]::WriteAllText($globalConfig,'')
 Push-Location $stage
 try{& $node $npmJs ci --ignore-scripts --omit=dev --no-audit --no-fund --registry=https://registry.npmjs.org ('--userconfig='+$config) ('--globalconfig='+$globalConfig) ('--cache='+ (Join-Path $root 'work\.npm-cache'));if($LASTEXITCODE -ne 0){throw 'Copilot dependency restore failed'}}finally{Pop-Location}
 Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/nodejs/node/v'+$versions.node+'/LICENSE') -OutFile (Join-Path $stage 'NODE-LICENSE.txt')
 New-Item -ItemType Directory -Path $dest -Force | Out-Null
 foreach($name in @('node.exe','package.json','package-lock.json','versions.json','NODE-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $dest $name) -Force}
 Copy-Item -LiteralPath (Join-Path $stage 'cli\copilot.exe') -Destination (Join-Path $dest 'copilot.exe') -Force
 $modules=Join-Path $dest 'node_modules'
 if(Test-Path -LiteralPath $modules){$full=[IO.Path]::GetFullPath($modules);if($full -ne [IO.Path]::GetFullPath((Join-Path $root 'outputs\ScreenText\copilot-runtime\node_modules'))){throw 'Unexpected runtime directory'};if(Get-ChildItem -LiteralPath $modules -Recurse -Force -Attributes ReparsePoint){throw 'Linked runtime dependencies refused'};Remove-Item -LiteralPath $modules -Recurse -Force}
 Copy-Item -LiteralPath (Join-Path $stage 'node_modules') -Destination $modules -Recurse
}finally{
 $full=[IO.Path]::GetFullPath($stage);$expected=[IO.Path]::GetFullPath($stageRoot).TrimEnd('\')+'\'
 if(-not $full.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -notmatch '^[a-f0-9]{32}$'){throw 'Unexpected restore cleanup path'}
 if(Test-Path -LiteralPath $full){if(Get-ChildItem -LiteralPath $full -Recurse -Force -Attributes ReparsePoint){throw 'Linked restore staging refused'};Remove-Item -LiteralPath $full -Recurse -Force}
}
