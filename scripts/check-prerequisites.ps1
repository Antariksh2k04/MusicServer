[CmdletBinding()]
param([ValidateSet('Cloud', 'Device', 'All')][string]$Scope = 'All')

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$report = [ordered]@{ checkedAtUtc = [DateTime]::UtcNow.ToString('o'); gateCompleted = $false }

function Test-Tool([string]$Name) {
    return $null -ne (Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue)
}

function Test-LocalPath([string]$Path, [string]$Type) {
    # UNC/mapped network paths can trigger network authentication even on Test-Path.
    # Only inventory local filesystem metadata, never credential contents.
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.StartsWith('\\') -or $Path.StartsWith('//')) { return $false }
    try {
        $full = [System.IO.Path]::GetFullPath($Path)
        $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($full))
        if ($drive.DriveType -eq [System.IO.DriveType]::Network) { return $false }
        return Test-Path -LiteralPath $full -PathType $Type
    }
    catch { return $false }
}

if ($Scope -in @('Cloud', 'All')) {
    $customConfig = $env:OCI_CLI_CONFIG_FILE
    $report.cloud = [ordered]@{
        task = '1.1'
        status = 'unverified'
        ociOnPath = Test-Tool 'oci'
        workspaceConfigPresent = Test-LocalPath (Join-Path $repository '.oci/config') 'Leaf'
        profileConfigPresent = Test-LocalPath (Join-Path $env:USERPROFILE '.oci/config') 'Leaf'
        customConfigConfigured = -not [string]::IsNullOrWhiteSpace($customConfig)
        customConfigPresent = $false
        accountEntitlementVerified = $false
    }
    if (-not [string]::IsNullOrWhiteSpace($customConfig)) {
        $report.cloud.customConfigPresent = Test-LocalPath $customConfig 'Leaf'
    }
    # Never read config/key content, invoke OCI, authenticate, or provision resources.
}

if ($Scope -in @('Device', 'All')) {
    $browsers = [ordered]@{}
    $browserPaths = [ordered]@{
        Chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe'
        Edge = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
    }
    foreach ($name in $browserPaths.Keys) {
        $browsers[$name] = $null
        if (Test-Path -LiteralPath $browserPaths[$name] -PathType Leaf) {
            $browsers[$name] = (Get-Item -LiteralPath $browserPaths[$name]).VersionInfo.ProductVersion
        }
    }
    $sdkLocations = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, (Join-Path $env:LOCALAPPDATA 'Android/Sdk')) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $report.device = [ordered]@{
        task = '1.3'
        status = 'unverified'
        adbOnPath = Test-Tool 'adb'
        javaOnPath = Test-Tool 'java'
        sdkDirectoryPresent = @($sdkLocations | Where-Object { Test-LocalPath $_ 'Container' }).Count -gt 0
        installedBrowsers = $browsers
        physicalPhoneBuildVerified = $false
        batteryAndAccessoryChecksVerified = $false
    }
    # Browser versions are observations; they do not establish stable channels.
    # Do not start adb, expose serials, change battery settings, or infer phone evidence.
}

$report | ConvertTo-Json -Depth 5
