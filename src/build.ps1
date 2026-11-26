$ErrorActionPreference = "Stop"

$srcDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $srcDir
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$output = Join-Path $root "m3u8DL-GUI.new.exe"
$icon = Join-Path $root "assets\m3u8dl-gui.ico"
if (-not (Test-Path -LiteralPath $icon)) {
    throw "Missing application icon: $icon"
}

$sources = Get-ChildItem -LiteralPath $srcDir -Filter "*.cs" |
    Sort-Object Name |
    ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) {
    throw "No C# sources found in $srcDir."
}

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /out:$output `
    /win32icon:$icon `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Xml.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built: $output"
Write-Host ("Sources: " + ($sources.Count) + " file(s)")
