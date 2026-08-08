param(
    [string]$BaseHref = "/EpubReaderDX/",
    [string]$Output = "artifacts/web"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

Write-Host "Publishing EpubReader.Web -> $Output"
dotnet publish (Join-Path $root "EpubReader.Web\EpubReader.Web.csproj") -c Release -o $Output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$site = Join-Path $Output "wwwroot"
$index = Join-Path $site "index.html"
if (-not (Test-Path $index)) { throw "Missing $index" }

if (-not $BaseHref.StartsWith("/")) { $BaseHref = "/$BaseHref" }
if (-not $BaseHref.EndsWith("/")) { $BaseHref = "$BaseHref/" }

$html = Get-Content -Raw -Path $index
$html2 = [regex]::Replace($html, '(<base\s+href=["''])[^"'']*(["'']\s*/?>)', "`${1}$BaseHref`${2}", 1)
if ($html -eq $html2) { throw "Could not rewrite <base href> in index.html" }

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($index, $html2, $utf8)

New-Item -ItemType File -Path (Join-Path $site ".nojekyll") -Force | Out-Null
Copy-Item $index (Join-Path $site "404.html") -Force

Write-Host "Ready: $site"
Write-Host "Base href: $BaseHref"
