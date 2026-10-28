<#
.SYNOPSIS
  Tells IndexNow search engines (Bing, Yahoo, DuckDuckGo, Yandex, Seznam, Naver) about every URL in the live sitemap.

.DESCRIPTION
  Run after publishing new or changed public pages. The key file (wwwroot/<key>.txt) must already be live,
  because the search engines fetch it to confirm the request comes from the site owner.

.EXAMPLE
  .\tools\submit-indexnow.ps1
#>
param([string]$Site = 'https://www.wageharbor.com')

$ErrorActionPreference = 'Stop'
$wwwroot = Join-Path $PSScriptRoot '..\src\Paycrest\wwwroot'
$keyFile = Get-ChildItem $wwwroot -Filter '*.txt' | Where-Object { $_.BaseName -match '^[0-9a-f]{32}$' } | Select-Object -First 1
if (-not $keyFile) { throw "No IndexNow key file (32 hex characters + .txt) found in $wwwroot." }
$key = $keyFile.BaseName

$live = (Invoke-WebRequest "$Site/$key.txt" -UseBasicParsing).Content.Trim()
if ($live -ne $key) { throw "The key file isn't live at $Site/$key.txt yet. Publish first." }

[xml]$sitemap = (Invoke-WebRequest "$Site/sitemap.xml" -UseBasicParsing).Content
$urls = @($sitemap.urlset.url | ForEach-Object { $_.loc })

$body = @{ host = ([uri]$Site).Host; key = $key; keyLocation = "$Site/$key.txt"; urlList = $urls } | ConvertTo-Json
$response = Invoke-WebRequest 'https://api.indexnow.org/indexnow' -Method Post -Body $body -ContentType 'application/json; charset=utf-8' -UseBasicParsing
Write-Host "IndexNow: HTTP $($response.StatusCode) for $($urls.Count) URLs." -ForegroundColor Green
