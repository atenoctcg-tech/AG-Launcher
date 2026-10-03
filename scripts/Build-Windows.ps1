$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\AGLauncher-win-x64"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$root\dist\updater" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $out | Out-Null

dotnet publish "$root\src\AGLauncher\AGLauncher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out
if ($LASTEXITCODE -ne 0) { throw "AGLauncher publish failed with exit code $LASTEXITCODE" }

dotnet publish "$root\src\AGLauncher.Updater\AGLauncher.Updater.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$root\dist\updater"
if ($LASTEXITCODE -ne 0) { throw "Updater publish failed with exit code $LASTEXITCODE" }

Copy-Item "$root\dist\updater\AGLauncher.Updater.exe" "$out\AGLauncher.Updater.exe" -Force
Copy-Item "$root\launcher-manifest.json" "$out\launcher-manifest.template.json" -Force

if (-not (Test-Path "$out\AGLauncher.exe")) { throw "AGLauncher.exe was not produced." }
if (-not (Test-Path "$out\AGLauncher.Updater.exe")) { throw "AGLauncher.Updater.exe was not produced." }

Compress-Archive -Path "$out\*" -DestinationPath "$root\dist\AGLauncher-win-x64.zip" -Force
Write-Host "Built: $root\dist\AGLauncher-win-x64.zip"
