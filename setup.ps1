$ErrorActionPreference = 'Stop'
$toolRoot = Join-Path $PSScriptRoot 'tools'
New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $toolRoot 'yt-dlp.exe'))) {
    Invoke-WebRequest 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile (Join-Path $toolRoot 'yt-dlp.exe')
}
if (!(Test-Path -LiteralPath (Join-Path $toolRoot 'ffmpeg.exe')) -or !(Test-Path -LiteralPath (Join-Path $toolRoot 'ffprobe.exe'))) {
    $downloadRoot = Join-Path $toolRoot 'ffmpeg-download'
    New-Item -ItemType Directory -Force -Path $downloadRoot | Out-Null
    $archive = Join-Path $downloadRoot 'ffmpeg.zip'
    Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $downloadRoot -Force
    $package = Get-ChildItem -LiteralPath $downloadRoot -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/ffmpeg.exe') } | Select-Object -First 1
    if (!$package) { throw 'FFmpeg archive does not contain the expected binaries.' }
    Copy-Item -LiteralPath (Join-Path $package.FullName 'bin/ffmpeg.exe') -Destination $toolRoot
    Copy-Item -LiteralPath (Join-Path $package.FullName 'bin/ffprobe.exe') -Destination $toolRoot
    Copy-Item -LiteralPath (Join-Path $package.FullName 'LICENSE') -Destination (Join-Path $toolRoot 'ffmpeg-LICENSE.txt')
}
if (!(Test-Path -LiteralPath (Join-Path $toolRoot 'ffmpeg-LICENSE.txt'))) { throw 'FFmpeg license is missing; restore it from the FFmpeg distribution.' }
