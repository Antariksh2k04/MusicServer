$ErrorActionPreference = 'Stop'
$helper = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../scripts/provision-owner.ps1'))
$names = @('ConnectionStrings__MusicServerMigration', 'MUSICSERVER_OWNER_ID_TOKEN', 'MUSICSERVER_OWNER_NONCE')
$original = @{}
foreach ($name in $names) { $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$global:ownerHelperCommands = [Collections.Generic.List[string]]::new()
$global:ownerHelperPrompts = 0
$global:ownerHelperFailConfiguration = $false
$global:ownerHelperFailProvision = $false
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if (-not $AsSecureString) { throw 'Secret prompt must be hidden.' }
    $global:ownerHelperPrompts++
    $value = if ($Prompt.StartsWith('Fresh Google')) { 'test-only-private-token' } else { 'test-only;quoted" password' }
    return ConvertTo-SecureString $value -AsPlainText -Force
}
function dotnet {
    if (($args -join ' ') -match 'test-only|Password=') { throw 'Secrets were included in command arguments.' }
    if ($args[-2] -ne 'owner') { throw 'Expected an operator-only command.' }
    $global:ownerHelperCommands.Add($args[-1])
    if ($args[-1] -eq 'provision') {
        $settings = [System.Data.Common.DbConnectionStringBuilder]::new()
        $settings.set_ConnectionString($env:ConnectionStrings__MusicServerMigration)
        if ($settings['Database'] -ne 'music_server_db') { throw 'Unexpected database target.' }
        if ($settings['Password'] -ne 'test-only;quoted" password') { throw 'Password delimiters were corrupted.' }
        if ($env:MUSICSERVER_OWNER_ID_TOKEN -ne 'test-only-private-token') { throw 'Proof was not passed privately.' }
        if ($env:MUSICSERVER_OWNER_NONCE -ne ('A' * 43)) { throw 'Wrong nonce binding.' }
    }
    $global:LASTEXITCODE = if ($global:ownerHelperFailConfiguration -or
        ($global:ownerHelperFailProvision -and $args[-1] -eq 'provision')) { 1 } else { 0 }
}
try {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, 'previous-test-value', 'Process') }
    & $helper -Nonce ('A' * 43)
    if (($global:ownerHelperCommands -join ',') -ne 'configuration,provision') { throw 'Unexpected command order.' }
    foreach ($name in $names) {
        if ([Environment]::GetEnvironmentVariable($name, 'Process') -ne 'previous-test-value') { throw 'Environment was not restored.' }
    }
    $global:ownerHelperCommands.Clear()
    $global:ownerHelperPrompts = 0
    $global:ownerHelperFailConfiguration = $true
    $caught = $false
    try { & $helper -Nonce ('A' * 43) } catch { $caught = $true }
    if (-not $caught -or $global:ownerHelperPrompts -ne 0 -or ($global:ownerHelperCommands -join ',') -ne 'configuration') {
        throw 'Invalid client configuration must fail before secret prompts or provisioning.'
    }
    foreach ($name in $names) {
        if ([Environment]::GetEnvironmentVariable($name, 'Process') -ne 'previous-test-value') { throw 'Failure did not restore environment.' }
    }
    $global:ownerHelperFailConfiguration = $false
    $global:ownerHelperFailProvision = $true
    $global:ownerHelperCommands.Clear()
    $caught = $false
    try { & $helper -Nonce ('A' * 43) } catch { $caught = $true }
    if (-not $caught -or ($global:ownerHelperCommands -join ',') -ne 'configuration,provision') { throw 'Failed provisioning was not reported.' }
    foreach ($name in $names) {
        if ([Environment]::GetEnvironmentVariable($name, 'Process') -ne 'previous-test-value') { throw 'Provisioning failure did not restore secrets.' }
    }
    Write-Output 'Owner helper contracts passed; no Google or database requests were made.'
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process') }
}
