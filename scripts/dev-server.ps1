[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$variableNames = @('ASPNETCORE_ENVIRONMENT', 'Diagnostics__Enabled', 'Diagnostics__OperatorKey',
    'Diagnostics__BrowserOrigin', 'Diagnostics__StorageDirectory', 'DOTNET_GENERATE_ASPNET_CERTIFICATE')
$previous = @{}
foreach ($name in $variableNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$secureKey = $null
$keyPointer = [IntPtr]::Zero
try {
    # An existing process environment value permits noninteractive private test runs.
    if ([string]::IsNullOrWhiteSpace($env:Diagnostics__OperatorKey)) {
        $secureKey = Read-Host 'Choose a local operator key (32-256 characters; enter the same key in the browser)' -AsSecureString
        $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        $env:Diagnostics__OperatorKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    }
    if ($env:Diagnostics__OperatorKey.Length -lt 32 -or $env:Diagnostics__OperatorKey.Length -gt 256) {
        throw 'The local operator key must be 32-256 characters.'
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Diagnostics__Enabled = 'true'
    $env:Diagnostics__BrowserOrigin = 'http://127.0.0.1:5173'
    $env:Diagnostics__StorageDirectory = Join-Path $repository '.local/media'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    Write-Host 'Starting loopback diagnostics at http://127.0.0.1:5080. Stop with Ctrl+C.'
    & dotnet run --project (Join-Path $repository 'MusicServerBackend/MusicServer.WebApi/MusicServer.WebApi.csproj') --no-restore --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw "Local server exited with code $LASTEXITCODE." }
}
finally {
    foreach ($name in $variableNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    if ($null -ne $secureKey) { $secureKey.Dispose() }
}
