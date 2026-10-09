[CmdletBinding()]
param([string]$Origin)

$ErrorActionPreference = 'Stop'
$report = [ordered]@{
    checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    task = '1.2'
    gateCompleted = $false
    status = 'blocked'
    code = 'originNotConfigured'
    trustedHttpsLiveness = $false
    googleOriginVerified = $false
    noPurchaseAndRenewalVerified = $false
    mobileDataVerified = $false
}

if (-not [string]::IsNullOrWhiteSpace($Origin)) {
    $parsed = $null
    $address = $null
    if (-not [Uri]::TryCreate($Origin, [UriKind]::Absolute, [ref]$parsed) -or
        $parsed.Scheme -ne 'https' -or $parsed.IsLoopback -or
        $parsed.DnsSafeHost.TrimEnd('.').ToLowerInvariant() -match '(^|\.)(localhost|local|internal|home\.arpa)$' -or
        [System.Net.IPAddress]::TryParse($parsed.DnsSafeHost, [ref]$address) -or
        $parsed.UserInfo -ne '' -or $parsed.Query -ne '' -or $parsed.Fragment -ne '' -or
        $parsed.AbsolutePath -ne '/') {
        $report.code = 'httpsHostnameOriginRequired'
    }
    else {
        try {
            # Normal OS certificate validation; no trust bypass, credentials, or redirects.
            $response = Invoke-WebRequest -UseBasicParsing -Uri ($parsed.GetLeftPart([UriPartial]::Authority) + '/api/v1/health/live') `
                -Method Get -MaximumRedirection 0 -TimeoutSec 10 -ErrorAction Stop
            $body = $response.Content | ConvertFrom-Json -ErrorAction Stop
            if ($response.StatusCode -eq 200 -and $body.status -eq 'live') {
                $report.trustedHttpsLiveness = $true
                $report.status = 'partial'
                $report.code = 'manualAcceptancePending'
            }
            else { $report.code = 'unexpectedLivenessResponse' }
        }
        catch { $report.code = 'httpsOrLivenessProbeFailed' }
    }
}

# A successful TLS request alone never proves billing, Google registration, renewal,
# or public/mobile reachability. Keep those acceptance fields false until evidence exists.
$report | ConvertTo-Json
