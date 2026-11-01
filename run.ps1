$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot 'outputs\ScreenText\ScreenText.exe'
if (-not (Test-Path -LiteralPath $app)) { & (Join-Path $PSScriptRoot 'build.ps1') }
Start-Process -FilePath $app -WorkingDirectory (Split-Path -Parent $app) -WindowStyle Normal
