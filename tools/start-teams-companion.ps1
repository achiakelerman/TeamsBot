$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$worker = Join-Path $root 'tools\teams-browser'
$url = Read-Host 'Paste the Teams meeting URL'
if ([string]::IsNullOrWhiteSpace($url)) { throw 'A Teams meeting URL is required.' }
$name = Read-Host 'Participant name (Enter for Meeting Companion)'
if ([string]::IsNullOrWhiteSpace($name)) { $name = 'Meeting Companion' }
Push-Location $worker
try {
  if (-not (Test-Path (Join-Path $worker 'node_modules\playwright'))) { npm install }
  npm run join -- $url $name
} finally { Pop-Location }
