param([string]$Root=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$v=Get-Content -LiteralPath (Join-Path $Root 'runtime\copilot\versions.json') -Raw | ConvertFrom-Json
$tool=Join-Path $Root ('work\.tools\npm-'+$v.npm);$cli=Join-Path $tool 'package\bin\npm-cli.js'
if(Test-Path -LiteralPath $cli){$installed=Get-Content -LiteralPath (Join-Path $tool 'package\package.json') -Raw | ConvertFrom-Json;if($installed.version -eq $v.npm){Write-Output $cli;return}}
New-Item -ItemType Directory -Path $tool -Force | Out-Null
$archive=Join-Path $tool 'npm.tgz'
Invoke-WebRequest -UseBasicParsing -Uri ('https://registry.npmjs.org/npm/-/npm-'+$v.npm+'.tgz') -OutFile $archive
$sha=[Security.Cryptography.SHA512]::Create();$stream=[IO.File]::OpenRead($archive);try{$actual='sha512-'+[Convert]::ToBase64String($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
if($actual -ne $v.npmIntegrity){throw 'Private npm archive integrity mismatch'}
$tar=Join-Path $env:SystemRoot 'System32\tar.exe';if(-not(Test-Path -LiteralPath $tar)){throw 'Windows tar.exe is required to restore the private build tool.'}
$names=& $tar -tzf $archive
if($LASTEXITCODE -ne 0 -or @($names | Where-Object {$_ -notmatch '^package/' -or $_ -match '(^|/)\.\.(/|$)'}).Count){throw 'Unexpected npm archive entries'}
& $tar -xzf $archive -C $tool
if($LASTEXITCODE -ne 0 -or -not(Test-Path -LiteralPath $cli)){throw 'Private npm extraction failed'}
Write-Output $cli
