[CmdletBinding()]
param([string]$JdkDirectory, [string]$SdkDirectory)

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pins = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'android-toolchain.json') -Raw | ConvertFrom-Json

function Resolve-LocalDirectory([string]$Path) {
    # Skip network paths: even metadata reads can cause implicit authentication.
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.StartsWith('\\') -or $Path.StartsWith('//')) { return $null }
    try {
        $full = [System.IO.Path]::GetFullPath($Path)
        $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($full))
        if ($drive.DriveType -eq [System.IO.DriveType]::Network) { return $null }
        if (Test-Path -LiteralPath $full -PathType Container) { return $full }
    }
    catch { }
    return $null
}

function Test-File([string]$Directory, [string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($Directory)) { return $false }
    return Test-Path -LiteralPath (Join-Path $Directory $RelativePath) -PathType Leaf
}

function Read-MetadataValue([string]$Directory, [string]$RelativePath, [string]$Key) {
    if (-not (Test-File $Directory $RelativePath)) { return $null }
    $pattern = '^' + [regex]::Escape($Key) + '\s*=\s*"?([0-9]+(?:\.[0-9]+)*(?:\+[0-9]+)?)"?\s*$'
    foreach ($line in Get-Content -LiteralPath (Join-Path $Directory $RelativePath)) {
        if ($line -match $pattern) { return $Matches[1] }
    }
    return $null
}

if ([string]::IsNullOrWhiteSpace($JdkDirectory)) {
    if (-not [string]::IsNullOrWhiteSpace($env:JAVA_HOME)) { $JdkDirectory = $env:JAVA_HOME }
    else { $JdkDirectory = Join-Path $env:ProgramFiles 'Android/Android Studio/jbr' }
}
if ([string]::IsNullOrWhiteSpace($SdkDirectory)) {
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) { $SdkDirectory = $env:ANDROID_HOME }
    elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) { $SdkDirectory = $env:ANDROID_SDK_ROOT }
    else { $SdkDirectory = Join-Path $env:LOCALAPPDATA 'Android/Sdk' }
}
$jdk = Resolve-LocalDirectory $JdkDirectory
$sdk = Resolve-LocalDirectory $SdkDirectory
$javaVersion = Read-MetadataValue $jdk 'release' 'JAVA_VERSION'
$javaMatches = $null -ne $javaVersion -and $javaVersion -match ('^' + $pins.javaMajor + '(\.|$)') -and
    (Test-File $jdk 'bin/java.exe') -and (Test-File $jdk 'bin/javac.exe')
$platformPath = 'platforms/android-' + $pins.compileSdk
$platformApi = Read-MetadataValue $sdk ($platformPath + '/source.properties') 'AndroidVersion.ApiLevel'
$platformMatches = $platformApi -eq [string]$pins.compileSdk -and (Test-File $sdk ($platformPath + '/android.jar'))
$buildPath = 'build-tools/' + $pins.buildTools
$buildRevision = Read-MetadataValue $sdk ($buildPath + '/source.properties') 'Pkg.Revision'
$buildMatches = $buildRevision -eq $pins.buildTools -and (Test-File $sdk ($buildPath + '/aapt2.exe')) -and
    (Test-File $sdk ($buildPath + '/d8.bat')) -and (Test-File $sdk ($buildPath + '/apksigner.bat'))
$adbRevision = Read-MetadataValue $sdk 'platform-tools/source.properties' 'Pkg.Revision'
$adbMatches = $adbRevision -eq $pins.platformTools -and (Test-File $sdk 'platform-tools/adb.exe')
$packages = [ordered]@{}
$missing = [System.Collections.Generic.List[string]]::new()
if (-not $javaMatches) { $missing.Add('jdk21Compiler') }
if (-not $platformMatches) { $missing.Add('android36Platform') }
if (-not $buildMatches) { $missing.Add('buildTools35') }
if (-not $adbMatches) { $missing.Add('platformTools37') }
foreach ($name in @('core', 'cli', 'android')) {
    $version = $null
    $manifest = Join-Path $repository ('MusicServerFrontend/node_modules/@capacitor/' + $name + '/package.json')
    if (Test-Path -LiteralPath $manifest -PathType Leaf) {
        $version = (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).version
    }
    $matchesPin = $version -eq $pins.capacitor
    $packages[$name] = [ordered]@{ installedVersion = $version; matchesPin = $matchesPin }
    if (-not $matchesPin) { $missing.Add('capacitor-' + $name) }
}

[ordered]@{
    task = '1.4'
    gateCompleted = $false
    status = $(if ($missing.Count -eq 0) { 'metadataMatches' } else { 'blocked' })
    pins = $pins
    java = [ordered]@{ releaseVersion = $javaVersion; compilerMetadataMatches = [bool]$javaMatches }
    sdk = [ordered]@{ platformMetadataMatches = [bool]$platformMatches; buildToolsRevision = $buildRevision;
        buildToolsMetadataMatches = [bool]$buildMatches; platformToolsRevision = $adbRevision; adbMetadataMatches = [bool]$adbMatches }
    capacitor = $packages
    missing = @($missing.ToArray())
    nativeBuildVerified = $false
    physicalDeviceVerified = $false
} | ConvertTo-Json -Depth 6
# Read-only inventory. No tool execution, downloads, licence acceptance, adb server,
# Gradle build, native project generation, or gate completion occurs here.
