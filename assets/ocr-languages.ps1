param(
    [ValidateSet('List','Install')][string]$Action='List',
    [ValidateSet('ar-SA','bg-BG','ca-ES','cs-CZ','da-DK','de-DE','el-GR','en-US','en-GB','es-ES','fi-FI','fr-FR','he-IL','hr-HR','hu-HU','it-IT','ja-JP','ko-KR','nb-NO','nl-NL','pl-PL','pt-BR','pt-PT','ro-RO','ru-RU','sk-SK','sl-SI','sr-Cyrl-RS','sr-Latn-RS','sv-SE','tr-TR','uk-UA','zh-CN','zh-TW')][string]$LanguageTag
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
if($Action -eq 'List') {
    try {
        $null=[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
        $languages=@([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages | ForEach-Object {
            $tag=$_.LanguageTag
            try { $name=[Globalization.CultureInfo]::GetCultureInfo($tag).EnglishName } catch { $name=$tag }
            [pscustomobject]@{Tag=$tag;Name=$name}
        })
        [Console]::Write((ConvertTo-Json -InputObject $languages -Compress))
        exit 0
    } catch { [Console]::Error.WriteLine('Windows OCR languages could not be read.');exit 1 }
}
# Only this explicit action services Windows. No arbitrary command, capability or output path is accepted.
if([string]::IsNullOrWhiteSpace($LanguageTag)){exit 10}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=New-Object Security.Principal.WindowsPrincipal($identity)
if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){exit 30}
try {
    $capabilities=Get-WindowsCapability -Online
    $ocr=@($capabilities | Where-Object { $_.Name -like "Language.OCR~~~$LanguageTag~*" })
    $basic=@($capabilities | Where-Object { $_.Name -like "Language.Basic~~~$LanguageTag~*" })
    if($ocr.Count -ne 1 -or $basic.Count -ne 1){exit 10}
    $restart=$false
    foreach($capability in @($basic[0],$ocr[0])) {
        if($capability.State -ne 'Installed') {
            $result=Add-WindowsCapability -Online -Name $capability.Name
            if($result.RestartNeeded){$restart=$true}
        }
    }
    if($restart){exit 3010}
    exit 0
} catch { exit 20 }
