param([string]$Dotnet='dotnet',[switch]$MigrateOnly)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $env:ConnectionStrings__Portal){throw 'Set ConnectionStrings__Portal; see README.md'}
Push-Location $repo
try{
  & $Dotnet tool restore
  if($LASTEXITCODE -ne 0){throw 'Tool restore failed'}
  & $Dotnet restore src/ScienceLine.Web
  if($LASTEXITCODE -ne 0){throw 'Restore failed'}
  & $Dotnet run --project src/ScienceLine.Web -- --migrate
  if($LASTEXITCODE -ne 0){throw 'Migration failed'}
  if(-not $MigrateOnly){& $Dotnet run --project src/ScienceLine.Web --no-build}
}finally{Pop-Location}
