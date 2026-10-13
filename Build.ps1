param([string]$Version = '0.5.1')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version muss MAJOR.MINOR.PATCH sein.' }
$root = $PSScriptRoot
$buildRoot = Join-Path $root 'build'
$env:NUGET_PACKAGES = Join-Path $buildRoot 'nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $buildRoot,(Join-Path $root 'Assets'),(Join-Path $root 'Lizenzen') | Out-Null
$mediaAsset = Join-Path $root 'Assets/Media.zip'
if (-not (Test-Path -LiteralPath $mediaAsset)) {
    $archive = Join-Path $buildRoot 'ffmpeg.7z'
    $metadata = Invoke-RestMethod 'https://api.github.com/repos/GyanD/codexffmpeg/releases/tags/9.0.2'
    $asset = $metadata.assets | Where-Object name -eq 'ffmpeg-9.0.2-essentials_build.7z'
    if (-not $asset) { throw 'Gepinnter FFmpeg-Build fehlt.' }
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '4705843CCAAF54257C16AD90F3E952ECE33C17DF964ECF7BFDBB0F49C7171077') { throw 'FFmpeg-Prüfsumme stimmt nicht überein.' }
    $expanded = Join-Path $buildRoot 'ffmpeg'
    New-Item -ItemType Directory -Force -Path $expanded | Out-Null
    tar -xf $archive -C $expanded
    if ($LASTEXITCODE -ne 0) { throw 'FFmpeg konnte nicht entpackt werden.' }
    $mediaRoot = Join-Path $expanded 'ffmpeg-9.0.2-essentials_build'
    Compress-Archive -LiteralPath (Join-Path $mediaRoot 'bin/ffmpeg.exe'),(Join-Path $mediaRoot 'bin/ffprobe.exe') -DestinationPath $mediaAsset
    Copy-Item -LiteralPath (Join-Path $mediaRoot 'LICENSE') -Destination (Join-Path $root 'Lizenzen/FFmpeg-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $mediaRoot 'README.txt') -Destination (Join-Path $root 'Lizenzen/FFmpeg-README.txt')
}
# Web-uploaded source packages may store license texts at the repository root.
Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Name -match '(-LICENSE|-NOTICES|FFmpeg-README|KOMPONENTEN)' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $root 'Lizenzen') -Force }
$appDir = Join-Path $buildRoot 'app'
dotnet publish (Join-Path $root 'Multitool.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:Version=$Version" -o $appDir
if ($LASTEXITCODE -ne 0) { throw 'Programm-Build fehlgeschlagen.' }
dotnet restore (Join-Path $root 'InnoCompiler.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Installer-Compiler fehlt.' }
$compiler = Join-Path $env:NUGET_PACKAGES 'tools.innosetup/7.1.0/tools/ISCC.exe'
$releaseDir = Join-Path $buildRoot 'release'
& $compiler "/DAppVersion=$Version" "/DPayloadDir=$appDir" "/DDocsDir=$root" "/DOutDir=$releaseDir" (Join-Path $root 'setup.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer-Build fehlgeschlagen.' }
$setup = Join-Path $releaseDir 'DateiWerk-Setup.exe'
$manifest = [ordered]@{version=$Version;termsVersion='2026-10-13.1';sha256=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseDir 'update.json') -Encoding utf8NoBOM
Write-Output "Release-Dateien: $releaseDir"

