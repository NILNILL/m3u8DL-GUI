$ErrorActionPreference = "Stop"

$supportDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $supportDir
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$output = Join-Path $root "N_m3u8DL-CLI.new.exe"
$icon = Join-Path $supportDir "logo_3Iv_icon.ico"
if (-not (Test-Path -LiteralPath $icon)) {
    throw "Missing application icon: $icon"
}
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /out:$output `
    /win32icon:$icon `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Xml.dll `
    (Join-Path $supportDir "src\N_m3u8DL-CLI.Gui.cs")

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built: $output"
