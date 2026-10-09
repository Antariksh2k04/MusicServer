$ErrorActionPreference = 'Stop'
Set-Clipboard -Value (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'review-since-last.md'))
