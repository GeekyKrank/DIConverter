param([string]$CompilerPath = '')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if (!$CompilerPath) {
    $compilerCandidates = @(
        (Join-Path $PSScriptRoot 'tools/inno/ISCC.exe'),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
    )
    $CompilerPath = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$CompilerPath) { throw 'Install Inno Setup 6 or 7, or pass -CompilerPath with the path to ISCC.exe.' }
& $CompilerPath (Join-Path $PSScriptRoot 'installer/DIConverter.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Write-Output 'Installer ready in dist.'
