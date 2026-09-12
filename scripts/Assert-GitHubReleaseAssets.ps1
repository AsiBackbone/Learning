[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TagName,
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [string]$Repository = 'AsiBackbone/Learning'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$evidenceRoot = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$expectedNames = @(Get-ChildItem -LiteralPath $evidenceRoot -File | Sort-Object Name | Select-Object -ExpandProperty Name)
if ($expectedNames.Count -eq 0) {
    throw "No expected release assets were found in '$evidenceRoot'."
}

$releaseJson = & gh release view $TagName --repo $Repository --json assets 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect release '$TagName' in '$Repository': $releaseJson"
}
$release = $releaseJson | ConvertFrom-Json
$actualNames = @($release.assets | ForEach-Object { [string]$_.name })
$missing = @($expectedNames | Where-Object { $_ -notin $actualNames })
if ($missing.Count -ne 0) {
    throw "Release '$TagName' is missing required assets: $($missing -join ', ')"
}

foreach ($assetName in $expectedNames) {
    $encodedTag = [uri]::EscapeDataString($TagName)
    $encodedName = [uri]::EscapeDataString($assetName)
    $downloadUrl = "https://github.com/$Repository/releases/download/$encodedTag/$encodedName"
    try {
        $response = Invoke-WebRequest -Uri $downloadUrl -Method Head -MaximumRedirection 5 -TimeoutSec 30 -ErrorAction Stop
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
            throw "HTTP $($response.StatusCode)"
        }
    }
    catch {
        throw "Release asset '$assetName' is not anonymously retrievable from '$downloadUrl': $($_.Exception.Message)"
    }
}

Write-Host "Verified $($expectedNames.Count) durable, anonymously retrievable release assets on '$Repository' release '$TagName'."
