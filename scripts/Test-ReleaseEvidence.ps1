[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("learning-release-evidence-{0}" -f [guid]::NewGuid().ToString('N'))
$repositoryRoot = Join-Path $testRoot 'repository'
$sbomDirectory = Join-Path $testRoot 'sbom'
$evidenceDirectory = Join-Path $testRoot 'evidence'
$tamperedEvidenceDirectory = Join-Path $testRoot 'tampered-evidence'
$releaseNotesPath = Join-Path $testRoot 'release-notes.md'

try {
    New-Item -ItemType Directory -Path (Join-Path $repositoryRoot 'samples/Test') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $repositoryRoot 'samples/Samples.slnx') -Value '<Solution />' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $repositoryRoot 'samples/Test/Test.cs') -Value 'public sealed class Test;' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $repositoryRoot 'samples/Test/packages.lock.json') -Encoding utf8 -NoNewline -Value @'
{
  "version": 2,
  "dependencies": {
    "net10.0": {
      "Example.Direct": { "type": "Direct", "resolved": "1.2.3", "contentHash": "fixture" },
      "Example.Transitive": { "type": "Transitive", "resolved": "4.5.6", "contentHash": "fixture" },
      "SampleProject": { "type": "Project" }
    }
  }
}
'@

    & git -C $repositoryRoot init -q
    & git -C $repositoryRoot config user.email 'release-evidence-test@example.invalid'
    & git -C $repositoryRoot config user.name 'Release Evidence Test'
    & git -C $repositoryRoot add samples
    & git -C $repositoryRoot commit -q -m fixture
    $sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    & git -C $repositoryRoot tag v9.8.7

    & (Join-Path $PSScriptRoot 'New-LearningSamplesSbom.ps1') `
        -RepositoryRoot $repositoryRoot `
        -OutputDirectory $sbomDirectory `
        -TagName v9.8.7 `
        -SourceCommit $sourceCommit

    $sbomPath = Join-Path $sbomDirectory 'learning-samples-9.8.7.spdx.json'
    $sbom = Get-Content -LiteralPath $sbomPath -Raw | ConvertFrom-Json
    if ($sbom.spdxVersion -ne 'SPDX-2.3' -or $sbom.files.Count -ne 3 -or $sbom.packages.Count -ne 3) {
        throw 'Generated SBOM did not contain the expected source and resolved dependency inventory.'
    }

    Set-Content -LiteralPath $releaseNotesPath -Value '# Release 9.8.7' -Encoding utf8 -NoNewline
    & (Join-Path $PSScriptRoot 'New-ReleaseEvidence.ps1') `
        -SbomDirectory $sbomDirectory `
        -ReleaseNotesPath $releaseNotesPath `
        -OutputDirectory $evidenceDirectory `
        -TagName v9.8.7 `
        -SourceCommit $sourceCommit

    $expectedFiles = @(
        'learning-samples-9.8.7.spdx.json',
        'learning-9.8.7-release-notes.md',
        'release-evidence-manifest.json'
    )
    foreach ($expectedFile in $expectedFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $evidenceDirectory $expectedFile) -PathType Leaf)) {
            throw "Expected evidence file was not generated: $expectedFile"
        }
    }

    $manifest = Get-Content -LiteralPath (Join-Path $evidenceDirectory 'release-evidence-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.releaseTag -ne 'v9.8.7' -or $manifest.assets.Count -ne 2 -or $manifest.scope.resolvedNuGetPackageCount -ne 2) {
        throw 'Release evidence manifest did not preserve the expected release identity and inventory.'
    }

    Set-Content -LiteralPath $sbomPath -Value '{"spdxVersion":"tampered"}' -Encoding utf8 -NoNewline
    $tamperRejected = $false
    try {
        & (Join-Path $PSScriptRoot 'New-ReleaseEvidence.ps1') `
            -SbomDirectory $sbomDirectory `
            -ReleaseNotesPath $releaseNotesPath `
            -OutputDirectory $tamperedEvidenceDirectory `
            -TagName v9.8.7 `
            -SourceCommit $sourceCommit
    }
    catch {
        $tamperRejected = $_.Exception.Message -eq 'SBOM hash does not match samples-sbom-metadata.json.'
    }
    if (-not $tamperRejected) {
        throw 'Tampered SBOM input was not rejected.'
    }

    $verifyProvenance = [string]$manifest.commands.verifyProvenance
    foreach ($pin in @(
            '--bundle release-evidence.sigstore.json',
            '--signer-workflow AsiBackbone/Learning/.github/workflows/publish-release-evidence.yml',
            '--source-ref refs/tags/v9.8.7',
            "--source-digest $sourceCommit",
            '--cert-oidc-issuer https://token.actions.githubusercontent.com',
            '--deny-self-hosted-runners')) {
        if (-not $verifyProvenance.Contains($pin, [System.StringComparison]::Ordinal)) {
            throw "Manifest provenance verification command does not pin '$pin'."
        }
    }

    # Signature verification must fail closed before any network call when the release shape is wrong.
    $signatureScript = Join-Path $PSScriptRoot 'Test-ReleaseSignatures.ps1'
    $signatureAssets = Join-Path $testRoot 'signature-assets'
    New-Item -ItemType Directory -Path $signatureAssets -Force | Out-Null
    foreach ($expectedFile in $expectedFiles) {
        Copy-Item -LiteralPath (Join-Path $evidenceDirectory $expectedFile) -Destination $signatureAssets
    }

    function Assert-SignatureCheckRejects([string]$Name, [string]$ExpectedMessage, [hashtable]$Arguments) {
        try {
            & $signatureScript @Arguments | Out-Null
        }
        catch {
            if (-not $_.Exception.Message.Contains($ExpectedMessage, [System.StringComparison]::Ordinal)) {
                throw "Signature check '$Name' failed with an unexpected message: $($_.Exception.Message)"
            }
            return
        }
        throw "Signature check '$Name' was expected to fail closed but passed."
    }

    $signatureArguments = @{ TagName = 'v9.8.7'; AssetDirectory = $signatureAssets; SourceCommit = $sourceCommit }
    Assert-SignatureCheckRejects 'prerelease tag' 'Only stable vMAJOR.MINOR.PATCH releases are supported' @{ TagName = 'v9.8.7-rc.1'; AssetDirectory = $signatureAssets; SourceCommit = $sourceCommit }
    Assert-SignatureCheckRejects 'missing bundle' "missing its durable Sigstore bundle 'release-evidence.sigstore.json'" ($signatureArguments + @{ RequireBundle = $true })
    Assert-SignatureCheckRejects 'malformed source commit' 'is not a full 40-character SHA-1' @{ TagName = 'v9.8.7'; AssetDirectory = $signatureAssets; SourceCommit = 'abc123' }

    Set-Content -LiteralPath (Join-Path $signatureAssets 'learning-9.8.7.zip') -Value 'unsigned' -Encoding utf8 -NoNewline
    Assert-SignatureCheckRejects 'unexpected asset' 'has assets outside the signed evidence set: learning-9.8.7.zip' $signatureArguments
    Remove-Item -LiteralPath (Join-Path $signatureAssets 'learning-9.8.7.zip')

    Remove-Item -LiteralPath (Join-Path $signatureAssets 'learning-9.8.7-release-notes.md')
    Assert-SignatureCheckRejects 'missing subject' 'is missing signed evidence subjects: learning-9.8.7-release-notes.md' $signatureArguments

    Write-Host 'Release evidence tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
