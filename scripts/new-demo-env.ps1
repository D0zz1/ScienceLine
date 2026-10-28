$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$target=Join-Path $repo '.env'
if(Test-Path -LiteralPath $target){throw '.env already exists; preserve the existing database password'}
$database='Db9a'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(18))
$demo='Demo9a'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12))
[IO.File]::WriteAllText($target,"DB_PASSWORD=$database`nDEMO_PASSWORD=$demo`n")
Write-Output 'Created ignored .env. View DEMO_PASSWORD locally to sign in; do not publish this file.'
