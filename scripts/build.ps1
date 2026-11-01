param([string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Destination){$Destination=Join-Path $root 'outputs\ScreenText'}
$source=Join-Path $root 'src';$compiler=Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'VERSION must contain major.minor.patch'}
$generated=Join-Path $root 'work\.generated';New-Item -ItemType Directory -Path $generated -Force | Out-Null
$versionSource=Join-Path $generated 'AssemblyVersion.cs'
[IO.File]::WriteAllText($versionSource,('[assembly:System.Reflection.AssemblyVersion("'+$version+'.0")][assembly:System.Reflection.AssemblyFileVersion("'+$version+'.0")][assembly:System.Reflection.AssemblyInformationalVersion("'+$version+'")]'))
$sources=@(Get-Content -LiteralPath (Join-Path $source 'application-sources.txt') | Where-Object {$_.Trim()} | ForEach-Object {Join-Path $source $_.Trim()})+@($versionSource)
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32icon:$Destination\screen-text.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Web.dll /reference:System.Net.Http.dll /reference:System.Security.dll "/out:$Destination\ScreenText.exe" @sources
if($LASTEXITCODE -ne 0){throw 'ScreenText build failed'}
& $compiler /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Web.dll /reference:System.Net.Http.dll /reference:System.Security.dll "/out:$Destination\OneOcrWorker.exe" (Join-Path $source 'OneOcrWorker.cs')
if($LASTEXITCODE -ne 0){throw 'OneOCR build failed'}
