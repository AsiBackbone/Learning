[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$SiteDirectory,
    [switch]$SelfTest
)

# Guards the entry pages that tell readers which Learning release is the published
# archive and which dated currentness review is selected. The pointers come from
# eng/published-learning-baseline.json. Historical pages may keep citing older
# releases; only the entry pages and the bridge's intentional teaching pins are checked.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)

function Get-ReleaseMarkerFailures {
    param(
        [string]$Homepage,
        [string]$GettingStarted,
        [string]$Readme,
        [string]$Bridge,
        [string]$Archive,
        [string]$Review,
        [bool]$IsRendered = $false
    )

    $releaseUrl = "https://github.com/AsiBackbone/Learning/releases/tag/v$Archive"
    $reviewPath = "implementation-currentness-review-$Review"
    # 'main' alone also matches <main> elements and words such as "domain", so
    # require the code-formatted branch name the entry pages use.
    $mainMarker = if ($IsRendered) { '<code>main</code>' } else { '`main`' }
    $failures = [Collections.Generic.List[string]]::new()

    $pages = [ordered]@{ 'Homepage' = $Homepage; 'Getting Started' = $GettingStarted }
    if (-not $IsRendered) {
        $pages['README'] = $Readme
    }

    foreach ($name in $pages.Keys) {
        $text = [string]$pages[$name]
        if (-not $text.Contains($releaseUrl, [StringComparison]::Ordinal)) {
            $failures.Add("$name does not link the published archive $releaseUrl.")
        }
        if (-not $text.Contains($reviewPath, [StringComparison]::Ordinal)) {
            $failures.Add("$name does not link the selected currentness review $reviewPath.")
        }
        if (-not $text.Contains($mainMarker, [StringComparison]::Ordinal)) {
            $failures.Add("$name does not distinguish the main-published site from the archive (expected $mainMarker).")
        }
    }

    if (-not $IsRendered) {
        foreach ($pin in @('v7.0.0', 'v2.11.2')) {
            if (-not $Bridge.Contains($pin, [StringComparison]::Ordinal)) {
                $failures.Add("The teaching bridge no longer carries its intentional $pin pin.")
            }
        }
    }

    return , $failures.ToArray()
}

if ($SelfTest) {
    $release = 'https://github.com/AsiBackbone/Learning/releases/tag/v1.6.0'
    $review = 'implementation-currentness-review-2026-11-14'
    $source = "$release $review follows ``main``"
    $rendered = "<a href=`"$release`">v1.6.0</a> <a href=`"getting-started/$review.html`"></a> follows <code>main</code>"
    $bridge = 'v7.0.0 v2.11.2'
    $selfTestFailures = [Collections.Generic.List[string]]::new()

    function Test-Case([string]$Name, [bool]$ShouldPass, [string[]]$Failures) {
        if ($ShouldPass -ne ($Failures.Length -eq 0)) {
            $selfTestFailures.Add("Self-test '$Name' expected $(if ($ShouldPass) { 'success' } else { 'failure' }) but got $($Failures.Length) failure(s).")
        }
    }

    Test-Case 'valid source' $true (Get-ReleaseMarkerFailures $source $source $source $bridge '1.6.0' '2026-11-14')
    Test-Case 'valid rendered' $true (Get-ReleaseMarkerFailures $rendered $rendered '' '' '1.6.0' '2026-11-14' $true)
    Test-Case 'stale homepage archive' $false (Get-ReleaseMarkerFailures $source.Replace('v1.6.0', 'v1.5.0') $source $source $bridge '1.6.0' '2026-11-14')
    Test-Case 'stale Getting Started review' $false (Get-ReleaseMarkerFailures $source $source.Replace($review, 'implementation-currentness-review-2026-11-07') $source $bridge '1.6.0' '2026-11-14')
    Test-Case 'stale README archive' $false (Get-ReleaseMarkerFailures $source $source $source.Replace('v1.6.0', 'v1.5.0') $bridge '1.6.0' '2026-11-14')
    Test-Case 'bridge pin removed' $false (Get-ReleaseMarkerFailures $source $source $source 'v7.5.4 v2.11.2' '1.6.0' '2026-11-14')
    Test-Case 'source without main distinction' $false (Get-ReleaseMarkerFailures $source.Replace('`main`', 'main') $source $source $bridge '1.6.0' '2026-11-14')
    Test-Case 'rendered <main> element is not a main distinction' $false (Get-ReleaseMarkerFailures $rendered.Replace('<code>main</code>', '<main></main>') $rendered '' '' '1.6.0' '2026-11-14' $true)
    Test-Case 'rendered stale review' $false (Get-ReleaseMarkerFailures $rendered $rendered.Replace('2026-11-14', '2026-11-07') '' '' '1.6.0' '2026-11-14' $true)

    if ($selfTestFailures.Count -gt 0) {
        $selfTestFailures | ForEach-Object { Write-Host "::error::$_" }
        exit 1
    }
    Write-Host 'Learning release-marker self-tests passed.'
    exit 0
}

$manifestPath = Join-Path $RepositoryRoot 'eng/published-learning-baseline.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$archive = [string]$manifest.publishedArchive
$review = [string]$manifest.currentnessReview
if ($archive -cnotmatch '^\d+\.\d+\.\d+$' -or $review -cnotmatch '^\d{4}-\d{2}-\d{2}$') {
    throw "Published archive/review manifest is malformed: $manifestPath"
}

$reviewSource = Join-Path $RepositoryRoot "docs/getting-started/implementation-currentness-review-$review.md"
if (-not (Test-Path -LiteralPath $reviewSource -PathType Leaf)) {
    throw "Selected currentness review does not exist: $reviewSource"
}
$releaseNotes = Join-Path $RepositoryRoot "RELEASE-NOTES-$archive.md"
if (-not (Test-Path -LiteralPath $releaseNotes -PathType Leaf)) {
    throw "Selected release notes do not exist: $releaseNotes"
}

if ($SiteDirectory) {
    $site = [IO.Path]::GetFullPath($SiteDirectory)
    $homepage = Get-Content -LiteralPath (Join-Path $site 'index.html') -Raw
    $start = Get-Content -LiteralPath (Join-Path $site 'getting-started/index.html') -Raw
    $failures = Get-ReleaseMarkerFailures $homepage $start '' '' $archive $review $true
    $scope = 'rendered site'
}
else {
    $homepage = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'docs/index.md') -Raw
    $start = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'docs/getting-started/index.md') -Raw
    $readme = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'README.md') -Raw
    $bridge = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'docs/getting-started/from-learning-samples-to-production-host.md') -Raw
    $failures = Get-ReleaseMarkerFailures $homepage $start $readme $bridge $archive $review
    $scope = 'source'
}

if ($failures.Length -gt 0) {
    $failures | ForEach-Object { Write-Host "::error::$_" }
    exit 1
}
Write-Host "Learning release markers validated ($scope): archive v$archive, currentness review $review."
