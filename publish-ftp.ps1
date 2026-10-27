<#
.SYNOPSIS
  Builds WageHarbor and uploads it to SmarterASP.NET (wageharbor.com) over FTP (FTPS by default).
  Use when Web Deploy (publish.ps1) is unavailable.

.EXAMPLE
  .\publish-ftp.ps1            # encrypted FTP (explicit TLS)
  .\publish-ftp.ps1 -NoTls     # plain FTP, only if the server refuses TLS
  .\publish-ftp.ps1 -SelfContained
#>
param([switch]$NoTls, [switch]$SelfContained)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$FtpRoot = 'ftp://win1209.site4now.net/WageHarbor'
$FtpUser = 'kkabir84-001'

$prod = 'src/Paycrest/appsettings.Production.json'
if (-not (Test-Path $prod)) { throw "Missing $prod - it holds the production database connection string." }
if ((Get-Content $prod -Raw) -match 'YOUR_DB_PASSWORD') { throw "Put your real database password in $prod first." }

dotnet test -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Tests failed - publish cancelled.' }

$out = Join-Path $PSScriptRoot 'publish'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$pubArgs = @('publish', 'src/Paycrest', '-c', 'Release', '-o', $out, '--nologo', '-v', 'q')
if ($SelfContained) { $pubArgs += @('--self-contained', 'true', '-r', 'win-x86') }
dotnet @pubArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$cred = Get-Credential -UserName $FtpUser -Message 'SmarterASP FTP password (usually the same as Web Deploy)'
if (-not $cred) { throw 'Password dialog was cancelled - nothing was uploaded.' }
$netCred = $cred.GetNetworkCredential()

function New-FtpRequest([string]$uri, [string]$method) {
    $r = [System.Net.FtpWebRequest]::Create($uri)
    $r.Method = $method
    $r.Credentials = $netCred
    $r.EnableSsl = -not $NoTls
    $r.UsePassive = $true
    $r.UseBinary = $true
    $r.KeepAlive = $true
    $r.Timeout = 120000
    return $r
}

function Send-FtpFile([string]$local, [string]$remote) {
    $req = New-FtpRequest $remote ([System.Net.WebRequestMethods+Ftp]::UploadFile)
    $bytes = [System.IO.File]::ReadAllBytes($local)
    $req.ContentLength = $bytes.Length
    $s = $req.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close()
    $req.GetResponse().Close()
}

function New-FtpDirectory([string]$remote) {
    try { (New-FtpRequest $remote ([System.Net.WebRequestMethods+Ftp]::MakeDirectory)).GetResponse().Close() }
    catch { } # already exists
}

# 1. Take the site offline so IIS releases the DLLs.
$offline = Join-Path $env:TEMP 'app_offline.htm'
Set-Content $offline '<html><body style="font-family:sans-serif;text-align:center;padding:4rem"><h2>WageHarbor is updating</h2><p>Back in a minute.</p></body></html>' -Encoding UTF8
Write-Host 'Taking site offline...'
Send-FtpFile $offline "$FtpRoot/app_offline.htm"
Start-Sleep -Seconds 3

# 2. Upload everything.
$files = Get-ChildItem $out -Recurse -File
$dirs = $files | ForEach-Object { $_.DirectoryName } | Sort-Object -Unique | Where-Object { $_ -ne $out }
foreach ($d in $dirs) {
    $rel = $d.Substring($out.Length).TrimStart('\').Replace('\', '/')
    $parts = $rel.Split('/'); $acc = ''
    foreach ($p in $parts) { $acc = if ($acc) { "$acc/$p" } else { $p }; New-FtpDirectory "$FtpRoot/$acc" }
}
$i = 0
foreach ($f in $files) {
    $i++
    $rel = $f.FullName.Substring($out.Length).TrimStart('\').Replace('\', '/')
    Write-Progress -Activity 'Uploading WageHarbor' -Status $rel -PercentComplete ($i * 100 / $files.Count)
    $tries = 0
    while ($true) {
        try { Send-FtpFile $f.FullName "$FtpRoot/$rel"; break }
        catch { $tries++; if ($tries -ge 3) { throw "Upload failed for $rel : $($_.Exception.Message)" }; Start-Sleep -Seconds 2 }
    }
}
Write-Progress -Activity 'Uploading WageHarbor' -Completed

# 3. Bring the site back online.
(New-FtpRequest "$FtpRoot/app_offline.htm" ([System.Net.WebRequestMethods+Ftp]::DeleteFile)).GetResponse().Close()

Write-Host "`nUploaded $($files.Count) files. Open https://wageharbor.com (first load takes 10-30 s while the database is created)." -ForegroundColor Green
