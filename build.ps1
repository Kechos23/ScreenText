param([switch]$Force,[switch]$Quiet,[switch]$SkipRuntime)
$ErrorActionPreference='Stop'
$app=Join-Path $PSScriptRoot 'outputs\ScreenText';New-Item -ItemType Directory -Path $app -Force | Out-Null
function Copy-Changed([string]$From,[string]$To){if(-not(Test-Path -LiteralPath $To) -or (Get-FileHash -LiteralPath $From).Hash -ne (Get-FileHash -LiteralPath $To).Hash){Copy-Item -LiteralPath $From -Destination $To -Force}}
foreach($name in @('ocr.ps1','ocr-languages.ps1','screen-text.ico','screen-text.svg')){Copy-Changed (Join-Path $PSScriptRoot ('assets\'+$name)) (Join-Path $app $name)}
foreach($name in @('README.md','LICENSE','THIRD-PARTY-NOTICES.md')){if(Test-Path -LiteralPath (Join-Path $PSScriptRoot $name)){Copy-Changed (Join-Path $PSScriptRoot $name) (Join-Path $app $name)}}
$names=@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\application-sources.txt') | Where-Object {$_.Trim()})
$inputs=@($names | ForEach-Object {'src\'+$_})+@('src\application-sources.txt','src\OneOcrWorker.cs','scripts\build.ps1','VERSION','assets\screen-text.ico')
$signature=($inputs | ForEach-Object {$_+':'+(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_)).Hash}) -join '|'
$cache=Join-Path $PSScriptRoot 'work\.build-cache.json';New-Item -ItemType Directory -Path (Split-Path -Parent $cache) -Force | Out-Null
$state=if(Test-Path -LiteralPath $cache){try{Get-Content -LiteralPath $cache -Raw | ConvertFrom-Json}catch{$null}}else{$null}
$outputs=@('ScreenText.exe','OneOcrWorker.exe');$present=@($outputs | Where-Object {-not(Test-Path -LiteralPath (Join-Path $app $_))}).Count -eq 0
$hash=if($present){($outputs | ForEach-Object {$_+':'+(Get-FileHash -LiteralPath (Join-Path $app $_)).Hash}) -join '|'}else{''}
if($Force -or -not($state -and $present -and $state.Inputs -eq $signature -and $state.Outputs -eq $hash)){
 if(Get-Process ScreenText -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $app 'ScreenText.exe')}){throw 'Close ScreenText using tray Exit before rebuilding.'}
 & (Join-Path $PSScriptRoot 'scripts\build.ps1') -Destination $app
 $hash=($outputs | ForEach-Object {$_+':'+(Get-FileHash -LiteralPath (Join-Path $app $_)).Hash}) -join '|'
 @{Inputs=$signature;Outputs=$hash} | ConvertTo-Json | Set-Content -LiteralPath $cache -Encoding UTF8
 if(-not $Quiet){Write-Host 'Built ScreenText and OneOCR helper.'}
}elseif(-not $Quiet){Write-Host 'Build unchanged; reused verified binaries.'}
if(-not $SkipRuntime){& (Join-Path $PSScriptRoot 'scripts\restore-runtime.ps1');Copy-Changed (Join-Path $PSScriptRoot 'src\CopilotWorker.mjs') (Join-Path $app 'copilot-runtime\worker.mjs')}
