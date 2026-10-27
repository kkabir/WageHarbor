<#
.SYNOPSIS
  Builds WageHarbor in Release and deploys it to SmarterASP.NET (wageharbor.com) with Web Deploy.

.EXAMPLE
  .\publish.ps1                 # framework-dependent (.NET 10 selected in the control panel)
  .\publish.ps1 -SelfContained  # bundles the .NET runtime (use if the site shows HTTP 500.31)
#>
param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$prod = 'src/Paycrest/appsettings.Production.json'
if (-not (Test-Path $prod)) { throw "Missing $prod - it holds the production database connection string." }
if ((Get-Content $prod -Raw) -match 'YOUR_DB_PASSWORD') {
    throw "Put your real database password in $prod (replace YOUR_DB_PASSWORD) before publishing."
}

# Run the tests first: never deploy a broken payroll engine.
dotnet test --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Tests failed - publish cancelled.' }

$cred = Get-Credential -UserName 'kkabir84-001' -Message 'SmarterASP Web Deploy password (control panel > Web Deploy)'
$password = $cred.GetNetworkCredential().Password

$publishArgs = @('publish', 'src/Paycrest', '-c', 'Release', '/p:PublishProfile=WageHarbor-WebDeploy', "/p:Password=$password")
# SmarterASP app pools run 32-bit by default; switch to win-x64 if you disabled "32-bit applications".
if ($SelfContained) { $publishArgs += @('/p:SelfContained=true', '/p:RuntimeIdentifier=win-x86') }

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Write-Host "`nPublished. Open https://wageharbor.com (first load takes 10-30 s while the database is created)." -ForegroundColor Green
