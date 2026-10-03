$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\AGLauncher-win-x64"
$assets = Join-Path $root "src\AGLauncher\Assets"
$encoded = Join-Path $root "src\AGLauncher\AssetsEncoded"

function Restore-EncodedAsset([string]$prefix, [string]$destination) {
    if (Test-Path $destination) { return }
    $parts = Get-ChildItem -Path $encoded -Filter "$prefix.*.b64" -ErrorAction SilentlyContinue | Sort-Object Name
    if (-not $parts -or $parts.Count -eq 0) {
        throw "Brand asset '$destination' is missing and no encoded source was found."
    }
    $base64 = ($parts | ForEach-Object { Get-Content $_.FullName -Raw }) -join ""
    [IO.File]::WriteAllBytes($destination, [Convert]::FromBase64String($base64))
}

New-Item -ItemType Directory -Path $assets -Force | Out-Null
Restore-EncodedAsset "icon" (Join-Path $assets "AGLauncher.ico")
Restore-EncodedAsset "brand" (Join-Path $assets "BrandLogo.png")

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$root\dist\updater" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $out -Force | Out-Null

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
