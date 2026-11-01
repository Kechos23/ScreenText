$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
& (Join-Path $root 'build.ps1') -Quiet
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$app=Join-Path $root 'outputs\ScreenText';$releaseRoot=Join-Path $root 'outputs\releases';New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$stageRoot=Join-Path $root 'work\.package-runs';$stage=Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'));$bundle=Join-Path $stage 'ScreenText'
$archive=Join-Path $releaseRoot ('ScreenText-'+$version+'-win-x64.zip');$temp=$archive+'.'+[Guid]::NewGuid().ToString('N')+'.zip'
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
try{
 foreach($name in @('ScreenText.exe','OneOcrWorker.exe','ocr.ps1','ocr-languages.ps1','screen-text.ico','screen-text.svg','README.md','LICENSE','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath (Join-Path $app $name) -Destination (Join-Path $bundle $name)}
 $runtime=Join-Path $app 'copilot-runtime';$runtimeBundle=Join-Path $bundle 'copilot-runtime';New-Item -ItemType Directory -Path $runtimeBundle | Out-Null
 $v=Get-Content -LiteralPath (Join-Path $root 'runtime\copilot\versions.json') -Raw | ConvertFrom-Json
 foreach($entry in @(@('node.exe',$v.nodeSha256),@('copilot.exe',$v.cliSha256))){if((Get-FileHash -LiteralPath (Join-Path $runtime $entry[0])).Hash -ne $entry[1]){throw ('Runtime integrity mismatch: '+$entry[0])}}
 foreach($name in @('node.exe','copilot.exe','worker.mjs','NODE-LICENSE.txt','versions.json','package.json','package-lock.json')){Copy-Item -LiteralPath (Join-Path $runtime $name) -Destination (Join-Path $runtimeBundle $name)}
 $modules=Join-Path $runtime 'node_modules';if(Get-ChildItem -LiteralPath $modules -Recurse -Force -Attributes ReparsePoint){throw 'Linked dependency directory refused'}
 Copy-Item -LiteralPath $modules -Destination (Join-Path $runtimeBundle 'node_modules') -Recurse
 Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination (Join-Path $bundle 'licenses') -Recurse
 [IO.File]::WriteAllText((Join-Path $bundle 'theme.txt'),'dark')
 Compress-Archive -LiteralPath $bundle -DestinationPath $temp
 Move-Item -LiteralPath $temp -Destination $archive -Force
 $hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
 [IO.File]::WriteAllText((Join-Path $releaseRoot 'SHA256SUMS.txt'),$hash+'  '+[IO.Path]::GetFileName($archive)+[Environment]::NewLine)
 Write-Host ('Packaged: '+$archive)
}finally{
 if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}
 $full=[IO.Path]::GetFullPath($stage);$expected=[IO.Path]::GetFullPath($stageRoot).TrimEnd('\')+'\'
 if(-not $full.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -notmatch '^[a-f0-9]{32}$'){throw 'Unexpected package cleanup path'}
 if(Test-Path -LiteralPath $full){if(Get-ChildItem -LiteralPath $full -Recurse -Force -Attributes ReparsePoint){throw 'Linked package staging refused'};Remove-Item -LiteralPath $full -Recurse -Force}
}
