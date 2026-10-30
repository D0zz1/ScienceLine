param([Parameter(Mandatory=$true)][string]$PgBin,[string]$Python='python',[string]$Node='node',[string]$EvidenceDirectory='docs/evidence/quality')
$ErrorActionPreference='Stop'
$qualityRoot=Split-Path $PSScriptRoot -Parent
$qualityOriginal=Get-Content -LiteralPath (Join-Path $qualityRoot '.local/settings.json') -Raw | ConvertFrom-Json
$qualityDatabase='scienceline_quality_'+(Get-Date -Format 'yyyyMMddHHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,6)
$qualityLocal=Join-Path $qualityRoot ('.local/'+$qualityDatabase)
New-Item -ItemType Directory -Path $qualityLocal -Force | Out-Null
$qualityEnvNames=@('PGPASSWORD','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','ConnectionStrings__Portal','Demo__Enabled','Demo__Password','DataProtection__KeysPath','Logging__LogLevel__Default','Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command','QUALITY_EVIDENCE_DIR')
$qualityPrevious=@{};foreach($qualityName in $qualityEnvNames){$qualityPrevious[$qualityName]=[Environment]::GetEnvironmentVariable($qualityName,'Process')}
$qualityServer=$null
try {
 $env:PGPASSWORD=$qualityOriginal.DbPassword
 & (Join-Path $PgBin 'createdb.exe') -h 127.0.0.1 -p $qualityOriginal.DbPort -U scienceline -T scienceline $qualityDatabase
 if($LASTEXITCODE -ne 0){throw 'Clone failed. Stop your source backend first; no existing connections were terminated.'}
 $qualitySettings=@{Database=$qualityDatabase;DbPort=$qualityOriginal.DbPort;DbPassword=$qualityOriginal.DbPassword;DemoPassword=$qualityOriginal.DemoPassword;AppPort=5181}
 $qualitySettingsPath=Join-Path $qualityLocal 'settings.json'
 $qualitySettings | ConvertTo-Json | Set-Content -LiteralPath $qualitySettingsPath -Encoding utf8
 $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS='http://127.0.0.1:5181'
 $env:ConnectionStrings__Portal="Host=127.0.0.1;Port=$($qualityOriginal.DbPort);Database=$qualityDatabase;Username=scienceline;Password=$($qualityOriginal.DbPassword);Maximum Pool Size=50"
 $env:Demo__Enabled='true';$env:Demo__Password=$qualityOriginal.DemoPassword
 $env:DataProtection__KeysPath=Join-Path $qualityLocal 'keys';$env:Logging__LogLevel__Default='Warning'
 [Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command','Warning','Process')
 $qualityProject=Join-Path $qualityRoot 'src/ScienceLine.Web'
 Push-Location $qualityRoot
 try {
  & dotnet build $qualityProject -c Release
  if($LASTEXITCODE -ne 0){throw 'Quality backend build failed.'}
  $qualityDll=Join-Path $qualityProject 'bin/Release/net10.0/ScienceLine.Web.dll'
  $qualityServer=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$qualityDll+'"')) -WorkingDirectory $qualityProject -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $qualityLocal 'backend.out.log') -RedirectStandardError (Join-Path $qualityLocal 'backend.err.log')
  $qualityReady=$false;$qualityOldProxy=[System.Net.WebRequest]::DefaultWebProxy
  try {
   [System.Net.WebRequest]::DefaultWebProxy=$null
   for($qualityTry=0;$qualityTry -lt 40;$qualityTry++){
    Start-Sleep -Seconds 1
    if($qualityServer.HasExited){throw 'Backend exited; inspect private backend.err.log.'}
    try{if((Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:5181/health' -TimeoutSec 2).StatusCode -eq 200){$qualityReady=$true;break}}catch{}
   }
  }finally{[System.Net.WebRequest]::DefaultWebProxy=$qualityOldProxy}
  if(-not $qualityReady){throw 'Quality backend did not become ready.'}
  $env:QUALITY_EVIDENCE_DIR=$EvidenceDirectory
  New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
  & $Python -X utf8 'tests/quality_experiments.py' --settings $qualitySettingsPath --psql (Join-Path $PgBin 'psql.exe') --out $EvidenceDirectory
  if($LASTEXITCODE -ne 0){throw 'Volume/load checks failed; inspect evidence.'}
  & $Node 'tests/frontend-quality.cjs'
  if($LASTEXITCODE -ne 0){throw 'Frontend checks failed; inspect evidence.'}
  & $Python -X utf8 'tests/optimization_comparison.py' --settings $qualitySettingsPath --psql (Join-Path $PgBin 'psql.exe') --out $EvidenceDirectory
  if($LASTEXITCODE -ne 0){throw 'Index comparison failed; inspect evidence.'}
  & $Node 'tests/frontend-comparison.cjs'
  if($LASTEXITCODE -ne 0){throw 'CSS comparison failed; inspect evidence.'}
  Write-Output "Quality experiments completed. Isolated database retained: $qualityDatabase"
 }finally{Pop-Location}
}finally{
 if($null -ne $qualityServer -and -not $qualityServer.HasExited){Stop-Process -Id $qualityServer.Id -ErrorAction SilentlyContinue}
 foreach($qualityName in $qualityEnvNames){[Environment]::SetEnvironmentVariable($qualityName,$qualityPrevious[$qualityName],'Process')}
}
