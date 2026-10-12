$ErrorActionPreference = 'Stop'
$helper = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../scripts/local-database.ps1'))
$variableName = 'ConnectionStrings__MusicServerMigration'
$originalConnection = [Environment]::GetEnvironmentVariable($variableName, 'Process')
$global:localDatabaseTestCommands = [Collections.Generic.List[string]]::new()
$global:localDatabaseTestFailInspect = $false
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if (-not $AsSecureString) { throw 'Password prompt must be hidden.' }
    return ConvertTo-SecureString 'test-only;quoted" password' -AsPlainText -Force
}
function dotnet {
    $settings = [System.Data.Common.DbConnectionStringBuilder]::new()
    $settings.set_ConnectionString([Environment]::GetEnvironmentVariable('ConnectionStrings__MusicServerMigration', 'Process'))
    if ($settings['Database'] -ne 'music_server_db') { throw 'Helper selected another database.' }
    if ($settings['Password'] -ne 'test-only;quoted" password') { throw 'Password delimiters were corrupted.' }
    if (($args -join ' ') -match 'test-only|Password=') { throw 'Credential entered command arguments.' }
    if ($args[-2] -ne 'database') { throw 'Helper did not invoke an operator command.' }
    $global:localDatabaseTestCommands.Add($args[-1])
    $global:LASTEXITCODE = if ($global:localDatabaseTestFailInspect) { 1 } else { 0 }
}
try {
    [Environment]::SetEnvironmentVariable($variableName, 'previous-test-configuration', 'Process')
    & $helper -Action apply 6>$null
    if (($global:localDatabaseTestCommands -join ',') -ne 'inspect,apply') { throw 'Apply must follow a successful inspection.' }
    if ([Environment]::GetEnvironmentVariable($variableName, 'Process') -ne 'previous-test-configuration') { throw 'Previous environment was not restored.' }
    $global:localDatabaseTestCommands.Clear()
    $global:localDatabaseTestFailInspect = $true
    $caught = $false
    try { & $helper -Action apply 6>$null } catch { $caught = $true }
    if (-not $caught -or ($global:localDatabaseTestCommands -join ',') -ne 'inspect') { throw 'A failed inspection must prevent migration.' }
    if ([Environment]::GetEnvironmentVariable($variableName, 'Process') -ne 'previous-test-configuration') { throw 'Failure did not restore environment.' }
    Write-Output 'Local database helper contracts passed; no database was contacted.'
}
finally {
    [Environment]::SetEnvironmentVariable($variableName, $originalConnection, 'Process')
}
