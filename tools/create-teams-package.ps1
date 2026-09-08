param([Parameter(Mandatory=$true)][string]$PublicHost)
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'appPackage'
$temp = Join-Path $env:TEMP ('teams-companion-package-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
$manifest = (Get-Content (Join-Path $source 'manifest.json') -Raw).Replace('YOUR_PUBLIC_HOST', $PublicHost.TrimEnd('/').Replace('https://','').Replace('http://',''))
Set-Content (Join-Path $temp 'manifest.json') $manifest
Copy-Item (Join-Path $source 'color.png'),(Join-Path $source 'outline.png') $temp
$out = Join-Path $root 'TeamsMeetingCompanion.zip'
Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $out -Force
Remove-Item $temp -Recurse -Force
Write-Host "Created $out"
