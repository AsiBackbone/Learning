[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TagName,
    [string]$AssetDirectory,
    [string]$SourceCommit,
    [string]$Repository = 'AsiBackbone/Learning',
    [switch]$RequireBundle,
    [switch]$IncludeNegativeTests
)

# Verifies the Sigstore-signed GitHub provenance attestations of a stable Learning release's durable evidence assets
# against a pinned signer identity: the source repository, the GitHub Actions OIDC issuer, the release-evidence
# workflow, the release tag, the tagged commit, and a GitHub-hosted runner. See RELEASE.md, "Signing decision" (#480).
#
# Without -AssetDirectory the assets are downloaded fresh from the public GitHub Release. When the release carries
# release-evidence.sigstore.json, every subject is verified against that durable bundle instead of the GitHub
# attestation API; -RequireBundle makes the bundle mandatory. -IncludeNegativeTests also proves that an altered subject
# and an untrusted signer identity are rejected. Every failure throws; nothing is skipped silently.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$bundleName = 'release-evidence.sigstore.json'
$oidcIssuer = 'https://token.actions.githubusercontent.com'
$signerWorkflowPath = '.github/workflows/publish-release-evidence.yml'

if ($TagName -notmatch '^v(?<version>\d+\.\d+\.\d+)$') {
    throw "Only stable vMAJOR.MINOR.PATCH releases are supported: $TagName"
}
$version = $Matches.version
$subjectNames = @(
    "learning-samples-$version.spdx.json",
    "learning-$version-release-notes.md",
    'release-evidence-manifest.json'
)

$workRoot = $null
try {
    if ([string]::IsNullOrWhiteSpace($AssetDirectory)) {
        $workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("learning-release-signatures-{0}" -f [guid]::NewGuid().ToString('N'))
        $AssetDirectory = Join-Path $workRoot 'download'
        New-Item -ItemType Directory -Path $AssetDirectory -Force | Out-Null
        & gh release download $TagName --repo $Repository --dir $AssetDirectory
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to download the assets of release '$TagName' from '$Repository'."
        }
    }

    $assetRoot = (Resolve-Path -LiteralPath $AssetDirectory).Path
    $presentNames = @(Get-ChildItem -LiteralPath $assetRoot -File | Select-Object -ExpandProperty Name)

    $missing = @($subjectNames | Where-Object { $_ -notin $presentNames })
    if ($missing.Count -ne 0) {
        throw "Release '$TagName' is missing signed evidence subjects: $($missing -join ', ')"
    }

    $unexpected = @($presentNames | Where-Object { $_ -notin $subjectNames -and $_ -ne $bundleName })
    if ($unexpected.Count -ne 0) {
        throw "Release '$TagName' has assets outside the signed evidence set: $($unexpected -join ', ')"
    }

    $bundlePath = Join-Path $assetRoot $bundleName
    $hasBundle = Test-Path -LiteralPath $bundlePath -PathType Leaf
    if ($RequireBundle -and -not $hasBundle) {
        throw "Release '$TagName' is missing its durable Sigstore bundle '$bundleName'."
    }

    if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
        $SourceCommit = (& gh api "repos/$Repository/commits/$TagName" --jq '.sha')
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($SourceCommit)) {
            throw "Unable to resolve release tag '$TagName' to a commit in '$Repository'."
        }
    }
    $SourceCommit = $SourceCommit.Trim().ToLowerInvariant()
    if ($SourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw "Source commit '$SourceCommit' is not a full 40-character SHA-1."
    }

    function Invoke-PinnedVerification([string]$SubjectPath, [string]$SourceRef) {
        $arguments = @(
            'attestation', 'verify', $SubjectPath,
            '--repo', $Repository,
            '--signer-workflow', "$Repository/$signerWorkflowPath",
            '--source-ref', $SourceRef,
            '--source-digest', $SourceCommit,
            '--cert-oidc-issuer', $oidcIssuer,
            '--predicate-type', 'https://slsa.dev/provenance/v1',
            '--deny-self-hosted-runners'
        )
        if ($hasBundle) {
            $arguments += @('--bundle', $bundlePath)
        }

        $output = & gh @arguments 2>&1
        return [pscustomobject]@{ Succeeded = ($LASTEXITCODE -eq 0); Output = ($output | Out-String) }
    }

    $sourceRef = "refs/tags/$TagName"
    foreach ($subjectName in $subjectNames) {
        $result = Invoke-PinnedVerification (Join-Path $assetRoot $subjectName) $sourceRef
        if (-not $result.Succeeded) {
            throw "Signature verification failed for '$subjectName' on release '$TagName':`n$($result.Output)"
        }
        Write-Host "Verified '$subjectName' against $Repository/$signerWorkflowPath@$sourceRef ($SourceCommit)."
    }

    if ($IncludeNegativeTests) {
        if ($null -eq $workRoot) {
            $workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("learning-release-signatures-{0}" -f [guid]::NewGuid().ToString('N'))
        }
        $tamperDirectory = Join-Path $workRoot 'tampered'
        New-Item -ItemType Directory -Path $tamperDirectory -Force | Out-Null

        $tamperedPath = Join-Path $tamperDirectory $subjectNames[1]
        Copy-Item -LiteralPath (Join-Path $assetRoot $subjectNames[1]) -Destination $tamperedPath
        Add-Content -LiteralPath $tamperedPath -Value 'tampered' -Encoding utf8
        if ((Invoke-PinnedVerification $tamperedPath $sourceRef).Succeeded) {
            throw "Negative test failed: an altered '$($subjectNames[1])' passed signature verification."
        }

        if ((Invoke-PinnedVerification (Join-Path $assetRoot $subjectNames[1]) 'refs/tags/v0.0.0').Succeeded) {
            throw "Negative test failed: '$($subjectNames[1])' verified against an untrusted signer identity."
        }

        Write-Host 'Negative tests passed: an altered subject and an untrusted signer identity were rejected.'
    }

    $mode = if ($hasBundle) { "the durable '$bundleName' bundle" } else { 'the GitHub attestation API' }
    Write-Host "Verified $($subjectNames.Count) signed evidence subjects of '$Repository' release '$TagName' using $mode."
}
finally {
    if ($null -ne $workRoot -and (Test-Path -LiteralPath $workRoot)) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
