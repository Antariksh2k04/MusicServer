[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('\A[A-Za-z0-9_-]{43}\z')]
    [string]$Nonce,
    [ValidateNotNullOrEmpty()]
    [string]$UserName = 'root'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repository 'MusicServerBackend/MusicServer.WebApi/MusicServer.WebApi.csproj'
$variableNames = @('ConnectionStrings__MusicServerMigration', 'MUSICSERVER_OWNER_ID_TOKEN', 'MUSICSERVER_OWNER_NONCE')
$previous = @{}
foreach ($name in $variableNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$password = $null
$token = $null
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
try {
    # Validate public client configuration before collecting any secrets or opening the database.
    & dotnet run --project $project --no-build --no-restore --no-launch-profile -- owner configuration
    if ($LASTEXITCODE -ne 0) { throw 'Google client configuration is incomplete. See docs/owner-provisioning.md.' }
    $password = Read-Host "Local MySQL password for $UserName (music_server_db only)" -AsSecureString
    $token = Read-Host 'Fresh Google ID token issued with the supplied nonce' -AsSecureString
    $connection['Server'] = 'localhost'
    $connection['Port'] = 3306
    $connection['Database'] = 'music_server_db'
    $connection['User ID'] = $UserName
    $connection['Password'] = ([PSCredential]::new('operator', $password)).GetNetworkCredential().Password
    $connection['Old Guids'] = $false
    [Environment]::SetEnvironmentVariable('ConnectionStrings__MusicServerMigration', $connection.get_ConnectionString(), 'Process')
    [Environment]::SetEnvironmentVariable('MUSICSERVER_OWNER_ID_TOKEN', ([PSCredential]::new('proof', $token)).GetNetworkCredential().Password, 'Process')
    [Environment]::SetEnvironmentVariable('MUSICSERVER_OWNER_NONCE', $Nonce, 'Process')
    & dotnet run --project $project --no-build --no-restore --no-launch-profile -- owner provision
    if ($LASTEXITCODE -ne 0) { throw 'Owner provisioning failed; no automatic owner replacement was attempted.' }
}
finally {
    foreach ($name in $variableNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $connection.Clear()
    if ($null -ne $password) { $password.Dispose() }
    if ($null -ne $token) { $token.Dispose() }
}
