[CmdletBinding()]
param(
    [ValidateSet('inspect', 'apply')]
    [string]$Action = 'inspect',
    [ValidateNotNullOrEmpty()]
    [string]$UserName = 'root'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repository 'MusicServerBackend/MusicServer.WebApi/MusicServer.WebApi.csproj'
$variableName = 'ConnectionStrings__MusicServerMigration'
$previousConnection = [Environment]::GetEnvironmentVariable($variableName, 'Process')
$securePassword = $null
$passwordPointer = [IntPtr]::Zero
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
try {
    $securePassword = Read-Host "Local MySQL password for $UserName (music_server_db only)" -AsSecureString
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    # The builder quotes delimiter characters in passwords; no string concatenation or file writes.
    $connection['Server'] = 'localhost'
    $connection['Port'] = 3306
    $connection['Database'] = 'music_server_db'
    $connection['User ID'] = $UserName
    $connection['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $connection['Old Guids'] = $false
    [Environment]::SetEnvironmentVariable($variableName, $connection.get_ConnectionString(), 'Process')
    Write-Host 'Target: localhost:3306/music_server_db. Existing build required; the password is not saved.'
    & dotnet run --project $project --no-build --no-restore --no-launch-profile -- database inspect
    if ($LASTEXITCODE -ne 0) { throw 'Database inspection failed; no migration was requested.' }
    if ($Action -eq 'apply') {
        & dotnet run --project $project --no-build --no-restore --no-launch-profile -- database apply
        if ($LASTEXITCODE -ne 0) { throw 'Database migration failed; follow docs/database-migrations.md before retrying.' }
    }
}
finally {
    [Environment]::SetEnvironmentVariable($variableName, $previousConnection, 'Process')
    $connection.Clear()
    if ($passwordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
    if ($null -ne $securePassword) { $securePassword.Dispose() }
}
