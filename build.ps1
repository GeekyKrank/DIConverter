param([string]$OutputDirectory = 'DIConverterApp')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$localSdk = Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localSdk) { $sdk = $localSdk }
else { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'tools\dotnet-home'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
& (Join-Path $projectRoot 'setup.ps1')
$project = Join-Path $projectRoot 'LiquidApp\SoundCloud.csproj'
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $projectRoot $OutputDirectory }
& $sdk publish $project -c Release --self-contained true --nologo -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Ready: $output\DIConverter.exe"
