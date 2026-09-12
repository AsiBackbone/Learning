[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SbomDirectory,
    [Parameter(Mandatory = $true)][string]$ReleaseNotesPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][ValidatePattern('^v\d+\.\d+\.\d+$')][string]$TagName,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit,
    [switch]$GeneratedAfterRelease,
    [string]$GenerationNote,
    [string]$Repository = 'AsiBackbone/Learning'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if ($GeneratedAfterRelease -and [string]::IsNullOrWhiteSpace($GenerationNote)) {
    throw 'GenerationNote is required when GeneratedAfterRelease is set.'
}

$version = $TagName.Substring(1)
$sourceCommit = $SourceCommit.ToLowerInvariant()
$sbomRoot = (Resolve-Path -LiteralPath $SbomDirectory).Path
$notesPath = (Resolve-Path -LiteralPath $ReleaseNotesPath).Path
$metadataPath = Join-Path $sbomRoot 'samples-sbom-metadata.json'

if ([string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $notesPath -Raw))) {
    throw 'Release notes must not be empty.'
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw 'samples-sbom-metadata.json was not found.'
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.releaseTag -ne $TagName -or $metadata.sourceCommit -ne $sourceCommit) {
    throw 'SBOM metadata does not match the requested release identity.'
}
$sbomPath = Join-Path $sbomRoot ([string]$metadata.sbomFile)
if ((Get-Sha256Hex -Path $sbomPath) -ne ([string]$metadata.sbomSha256).ToLowerInvariant()) {
    throw 'SBOM hash does not match samples-sbom-metadata.json.'
}
$sbom = Get-Content -LiteralPath $sbomPath -Raw | ConvertFrom-Json
if ($sbom.spdxVersion -ne 'SPDX-2.3' -or $sbom.packages[0].versionInfo -ne $version) {
    throw 'SBOM identity does not match the release.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
if (@(Get-ChildItem -LiteralPath $outputRoot -Force).Count -ne 0) {
    throw "Release evidence output directory must be empty: $outputRoot"
}

$notesName = "learning-$version-release-notes.md"
$assets = [System.Collections.Generic.List[object]]::new()
foreach ($item in @(
    @{ Source = $sbomPath; Name = [string]$metadata.sbomFile; MediaType = 'application/spdx+json'; Purpose = 'samples-and-resolved-dependencies-sbom' },
    @{ Source = $notesPath; Name = $notesName; MediaType = 'text/markdown'; Purpose = 'exact-github-release-notes' }
)) {
    $destination = Join-Path $outputRoot $item.Name
    Copy-Item -LiteralPath $item.Source -Destination $destination
    $assets.Add([ordered]@{
        name = $item.Name
        sha256 = Get-Sha256Hex -Path $destination
        mediaType = $item.MediaType
        purpose = $item.Purpose
    })
}

$createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$manifest = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    releaseTag = $TagName
    releaseVersion = $version
    sourceCommit = $sourceCommit
    createdUtc = $createdUtc
    generatedAfterRelease = [bool]$GeneratedAfterRelease
    generationNote = $(if ([string]::IsNullOrWhiteSpace($GenerationNote)) { $null } else { $GenerationNote })
    scope = $metadata.scope
    assets = @($assets.ToArray())
    tools = [ordered]@{
        sbomGenerator = [ordered]@{ name = 'New-LearningSamplesSbom.ps1'; powershellVersion = [string]$metadata.generator.powershellVersion }
        evidenceGenerator = [ordered]@{ name = 'New-LearningReleaseEvidence.ps1'; powershellVersion = [string]$PSVersionTable.PSVersion }
    }
    commands = [ordered]@{
        restore = 'dotnet restore samples/Samples.slnx --locked-mode'
        build = 'dotnet build samples/Samples.slnx --no-restore'
        format = 'dotnet format samples/Samples.slnx --verify-no-changes --no-restore --verbosity minimal'
        test = 'dotnet test samples/Samples.slnx --no-build'
        docs = 'dotnet tool run docfx docs/docfx.json --warningsAsErrors'
        verifyAssetHashPowerShell = '(Get-FileHash -Algorithm SHA256 <downloaded-file>).Hash.ToLowerInvariant()'
        verifyProvenance = $(if ($GeneratedAfterRelease) { $null } else { "gh attestation verify <downloaded-file> --repo $Repository" })
    }
    trustBoundaries = [ordered]@{
        actionsArtifact = 'Temporary diagnostic workflow hand-off; not durable release evidence.'
        sbom = 'Inventory of tracked samples/ source and locked NuGet dependencies; not a vulnerability report or security guarantee.'
        signing = 'No package, binary, archive, or container is released by Learning; signing is outside the current release scope.'
        securityReports = 'Code scanning and dependency findings remain on GitHub security surfaces and are not release assets.'
    }
}

$manifest | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $outputRoot 'release-evidence-manifest.json') -Encoding utf8 -NoNewline
Write-Host "Prepared $($assets.Count + 1) durable release evidence files for '$TagName'."
