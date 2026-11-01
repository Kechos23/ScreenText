param(
 [ValidateSet('Quick','Core','UI','Lifecycle','Storage','Translation','Languages','Reading','Runtime','Connections','CodexCLI','All')][string]$Suite='Quick',
 [switch]$Previews,[switch]$KeepArtifacts,[switch]$ForceBuild,[switch]$List,
 [ValidateRange(5,600)][int]$TimeoutSeconds=90
)
$ErrorActionPreference='Stop'
$suites=[ordered]@{Quick=@('CoreSmoke','ReadingUiSmoke');Core=@('CoreSmoke');UI=@('AppearanceSmoke','WorkspaceUiSmoke','ReadingUiSmoke');Lifecycle=@('StartupSmoke','InteractionSmoke');Storage=@('TempAndMenuSmoke','EditorSmoke');Translation=@('CodexSmoke','TranslationRetentionSmoke');Languages=@('LanguageSmoke','OneOcrSmoke');Reading=@('ReadingUiSmoke');Runtime=@('RuntimeRepairsSmoke');Connections=@('ChatGptSmoke','CopilotSmoke','CodexCliSmoke');CodexCLI=@('CodexCliSmoke');All=@('ChatGptSmoke','CopilotSmoke','CodexCliSmoke','RuntimeRepairsSmoke','CoreSmoke','TempAndMenuSmoke','AppearanceSmoke','StartupSmoke','InteractionSmoke','EditorSmoke','CodexSmoke','LanguageSmoke','OneOcrSmoke','WorkspaceUiSmoke','ReadingUiSmoke','TranslationRetentionSmoke')}
if($List){$suites.GetEnumerator() | ForEach-Object {Write-Output ($_.Key+': '+($_.Value -join ', '))};return}
$overall=[Diagnostics.Stopwatch]::StartNew()
& (Join-Path $PSScriptRoot 'build.ps1') -Force:$ForceBuild -Quiet
$app=Join-Path $PSScriptRoot 'outputs\ScreenText'
if($Suite -in @('Lifecycle','All')){if(Get-Process -Name ScreenText -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $app 'ScreenText.exe')}){throw 'Exit ScreenText from the tray before Lifecycle/All tests. Quick/UI/Reading/Translation tests can run while it is open.'}}
$compiler=Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$cacheRoot=Join-Path $PSScriptRoot 'work\.test-cache';$runsRoot=Join-Path $PSScriptRoot 'work\.test-runs';$runId=[Guid]::NewGuid().ToString('N');$runRoot=Join-Path $runsRoot $runId
$reportRoot=Join-Path $PSScriptRoot ('outputs\validation\'+$Suite.ToLowerInvariant());$previewRoot=Join-Path $PSScriptRoot ('outputs\previews\'+$Suite.ToLowerInvariant())
foreach($folder in @($cacheRoot,$runRoot,$reportRoot)){New-Item -ItemType Directory -Path $folder -Force | Out-Null}
[IO.File]::WriteAllText((Join-Path $runRoot '.screentext-tests'),'ScreenText isolated tests v2')
$runtimeHash=(Get-FileHash -LiteralPath (Join-Path $app 'ScreenText.exe')).Hash
$compilerVersion=(Get-Item -LiteralPath $compiler).VersionInfo.FileVersion
function Compile-Test([string]$Name){
 $source=Join-Path $PSScriptRoot ('tests\'+$Name+'.cs');$hash=(Get-FileHash -LiteralPath $source).Hash
 $sharedHash=if($Name -in @('TranslationRetentionSmoke','LanguageSmoke')){(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'tests\ChatGptSmoke.cs')).Hash}else{''}
 $key=$Name+'|'+$hash+'|'+$sharedHash+'|'+$runtimeHash+'|'+$compilerVersion+'|v2';$sha=[Security.Cryptography.SHA256]::Create();try{$digest=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key))).Replace('-','')}finally{$sha.Dispose()}
 $target=Join-Path $cacheRoot ($Name+'-'+$digest+'.exe')
 if(-not(Test-Path -LiteralPath $target)){[string[]]$extraSource=if($Name -in @('TranslationRetentionSmoke','LanguageSmoke')){@((Join-Path $PSScriptRoot 'tests\ChatGptSmoke.cs'))}else{@()};$kind=if($Name -eq 'EditorProbe'){'winexe'}else{'exe'};$compileOutput=& $compiler /nologo "/target:$kind" /platform:x64 /codepage:65001 "/main:$Name" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll "/reference:$app\ScreenText.exe" "/out:$target" $source @extraSource;if($LASTEXITCODE -ne 0){throw ('Test compilation failed: '+$Name+[Environment]::NewLine+($compileOutput -join [Environment]::NewLine))}}
 return $target
}
function New-Fixture([string]$Path){
 Add-Type -AssemblyName System.Drawing
 $bitmap=New-Object Drawing.Bitmap(1000,140);$graphics=[Drawing.Graphics]::FromImage($bitmap);$font=New-Object Drawing.Font('Segoe UI',18)
 try{$graphics.Clear([Drawing.Color]::White);$text=[regex]::Unescape('\u0397 \u03b5\u03be\u03b1\u03b3\u03c9\u03b3\u03ae \u03ba\u03b1\u03b9 \u03b7 \u03bc\u03b5\u03c4\u03ac\u03c6\u03c1\u03b1\u03c3\u03b7 \u03bb\u03b5\u03b9\u03c4\u03bf\u03c5\u03c1\u03b3\u03bf\u03cd\u03bd \u03c3\u03c9\u03c3\u03c4\u03ac.');$graphics.DrawString($text,$font,[Drawing.Brushes]::Black,16,35);$bitmap.Save($Path)}finally{$font.Dispose();$graphics.Dispose();$bitmap.Dispose()}
}
$results=New-Object 'System.Collections.Generic.List[object]';$succeeded=$false
try{
 foreach($name in $suites[$Suite]){
  $watch=[Diagnostics.Stopwatch]::StartNew();$stage=Join-Path $runRoot $name;New-Item -ItemType Directory -Path $stage | Out-Null
  foreach($file in @('ScreenText.exe','OneOcrWorker.exe','ocr.ps1','ocr-languages.ps1')){Copy-Item -LiteralPath (Join-Path $app $file) -Destination $stage}
  # Every harness gets fresh preferences, an isolated ChatGPT store and only fake Codex. Never copy user settings/authentication.
  [IO.File]::WriteAllText((Join-Path $stage '.screentext-isolated-test'),'ScreenText isolated tests v2')
  foreach($probe in @('CodexProbe','EditorProbe','OneOcrProbe')){Copy-Item -LiteralPath (Compile-Test $probe) -Destination (Join-Path $stage ($probe+'.exe'))}
  [IO.File]::WriteAllText((Join-Path $stage 'codex-path.txt'),(Join-Path $stage 'CodexProbe.exe'))
  [IO.File]::WriteAllText((Join-Path $stage 'probe-mode.txt'),'success')
  $fixture=Join-Path $stage 'fixture.png';New-Fixture $fixture
  $testExe=Join-Path $stage ($name+'.exe');Copy-Item -LiteralPath (Compile-Test $name) -Destination $testExe
  $report=Join-Path $stage 'report.txt'
  $arguments=switch($name){TempAndMenuSmoke{'"'+$fixture+'" "'+$report+'"'}EditorSmoke{'"'+$fixture+'" "'+$report+'" "'+(Join-Path $stage 'EditorProbe.exe')+'"'}CodexSmoke{'"'+(Join-Path $stage 'CodexProbe.exe')+'" "'+$report+'"'}LanguageSmoke{'"'+$report+'" "'+(Join-Path $stage 'CodexProbe.exe')+'"'}OneOcrSmoke{'"'+$report+'" "'+(Join-Path $stage 'OneOcrProbe.exe')+'"'}TranslationRetentionSmoke{'"'+$report+'" "'+(Join-Path $stage 'CodexProbe.exe')+'"'}default{'"'+$report+'"'}}
  $process=Start-Process -FilePath $testExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
  try{if(-not $process.WaitForExit($TimeoutSeconds*1000)){& (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $process.Id /T /F | Out-Null;throw ('Test timed out: '+$name)};if(-not(Test-Path -LiteralPath $report)){throw ('Test produced no report: '+$name)};Copy-Item -LiteralPath $report -Destination (Join-Path $reportRoot ($name+'.txt'));if($process.ExitCode -ne 0){throw ($name+': '+[IO.File]::ReadAllText($report))}}finally{$process.Dispose()}
  if($Previews){$destination=Join-Path $previewRoot $name;New-Item -ItemType Directory -Path $destination -Force | Out-Null;Get-ChildItem -LiteralPath $stage -File -Filter '*.png' | Where-Object {$_.Name -ne 'fixture.png' -and $_.Name -notlike 'oneocr-*'} | Copy-Item -Destination $destination}
  $watch.Stop();$results.Add([pscustomobject]@{Test=$name;Seconds=[Math]::Round($watch.Elapsed.TotalSeconds,3);Passed=$true});Write-Host ('PASS {0}: {1:N2}s' -f $name,$watch.Elapsed.TotalSeconds)
 }
 $succeeded=$true
}finally{
 $overall.Stop();[pscustomobject]@{Suite=$Suite;Passed=$succeeded;Seconds=[Math]::Round($overall.Elapsed.TotalSeconds,3);ApplicationSHA256=$runtimeHash;FinishedUtc=[DateTime]::UtcNow.ToString('o');Tests=@($results.ToArray());Artifacts=if($KeepArtifacts -or -not $succeeded){$runRoot}else{$null}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportRoot 'summary.json') -Encoding UTF8
 if($succeeded -and -not $KeepArtifacts){$resolved=[IO.Path]::GetFullPath($runRoot);$expected=[IO.Path]::GetFullPath($runsRoot).TrimEnd('\')+'\';if(-not $resolved.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^[a-f0-9]{32}$' -or [IO.File]::ReadAllText((Join-Path $resolved '.screentext-tests')) -ne 'ScreenText isolated tests v2'){throw 'Refusing unexpected test cleanup path'};if(Get-ChildItem -LiteralPath $resolved -Recurse -Force -Attributes ReparsePoint){throw 'Refusing linked test cleanup path'};Remove-Item -LiteralPath $resolved -Recurse -Force}
 if(-not $succeeded){Write-Host ('Failed artifacts retained: '+$runRoot)}
}
Write-Host ('{0}: {1} tests passed in {2:N2}s. Reports: {3}' -f $Suite,$results.Count,$overall.Elapsed.TotalSeconds,$reportRoot)