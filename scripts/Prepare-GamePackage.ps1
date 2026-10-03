param([Parameter(Mandatory=$true)][string]$GameBuildFolder,[Parameter(Mandatory=$true)][string]$OutputZip)
$ErrorActionPreference="Stop"
if(Test-Path $OutputZip){Remove-Item $OutputZip -Force}
Compress-Archive -Path "$GameBuildFolder\*" -DestinationPath $OutputZip -CompressionLevel Optimal
$hash=(Get-FileHash $OutputZip -Algorithm SHA256).Hash.ToLower()
Write-Host "ZIP: $OutputZip"
Write-Host "SHA256: $hash"
Write-Host "If a package exceeds GitHub's release asset size limit, split the game into multiple independent ZIP packages and list each in the game manifest."
