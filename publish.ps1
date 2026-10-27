# Builds a self-contained LyricFlow.exe (no .NET install needed on the target PC) and zips it into dist/.
# Usage:  powershell -ExecutionPolicy Bypass -File .\publish.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src\LyricFlow\LyricFlow.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$out = Join-Path $PSScriptRoot 'dist\LyricFlow'
$zip = Join-Path $PSScriptRoot "dist\LyricFlow-$version-win-x64.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -nodeReuse:false -p:UseSharedCompilation=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Copy-Item (Join-Path $PSScriptRoot 'dist-readme.txt') (Join-Path $out 'README.txt')

if (Test-Path $zip) { Remove-Item $zip -Force }

# Antivirus often scans a freshly written exe and briefly locks it, so retry the zip.
for ($attempt = 1; ; $attempt++) {
    try {
        Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -ErrorAction Stop
        break
    } catch {
        if ($attempt -ge 6) { throw }
        if (Test-Path $zip) { Remove-Item $zip -Force }
        Start-Sleep -Seconds 5
    }
}
dotnet build-server shutdown | Out-Null

Get-Item $zip | Select-Object FullName, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
