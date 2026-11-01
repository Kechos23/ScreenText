param([Parameter(Mandatory=$true)][string]$ImagePath, [string]$Language = 'auto')
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Globalization, ContentType=WindowsRuntime]
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function Await($operation, [Type]$resultType) {
    $task = $asTask.MakeGenericMethod($resultType).Invoke($null, @($operation))
    $task.Wait()
    $task.Result
}
$stream = $null
$bitmap = $null
try {
    $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync([IO.Path]::GetFullPath($ImagePath))) ([Windows.Storage.StorageFile])
    $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
    if ($bitmap.PixelWidth -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -or $bitmap.PixelHeight -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension) { throw 'The image is too large. Select a smaller region.' }
    if ($Language -in @('auto','system','greek-english')) { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages() }
    else { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage((New-Object Windows.Globalization.Language($Language))) }
    if ($Language -eq 'greek-english' -and -not ([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages | Where-Object { $_.LanguageTag -match '^el' })) { throw 'Greek + English needs the Greek Windows OCR pack. Install it in Settings > Extraction & Translation > Advanced > Windows OCR, or choose an installed language.' }
    if ($null -eq $engine) { throw 'This OCR language is not installed. Choose an installed language or open Settings > Extraction & Translation > Advanced > Windows OCR.' }
    $result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
    $text = ($result.Lines | ForEach-Object { $_.Text }) -join "`r`n"
    # User-profile language is not language detection. Try the Greek recognizer
    # as well: it preserves Latin passages while correctly reading Greek script.
    if ($Language -in @('auto','greek-english') -and $engine.RecognizerLanguage.LanguageTag -notmatch '^el') {
        $greekLanguage = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages | Where-Object { $_.LanguageTag -match '^el' } | Select-Object -First 1
        if ($null -ne $greekLanguage) {
            $greekEngine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($greekLanguage)
            $greekResult = Await ($greekEngine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
            $greekText = ($greekResult.Lines | ForEach-Object { $_.Text }) -join "`r`n"
            $greekCount = [regex]::Matches($greekText, '[\p{IsGreek}\p{IsGreekExtended}]').Count
            $letterCount = [regex]::Matches($greekText, '\p{L}').Count
            $greekWords = [regex]::Matches($greekText, '[\p{IsGreek}\p{IsGreekExtended}]{2,}').Count
            if ($greekCount -ge 3 -and ($greekWords -ge 2 -or $greekCount / [Math]::Max(1,$letterCount) -ge 0.65)) { $text = $greekText }
        }
    }
    [Console]::Write($text)
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
finally { if ($null -ne $bitmap) { $bitmap.Dispose() }; if ($null -ne $stream) { $stream.Dispose() } }
