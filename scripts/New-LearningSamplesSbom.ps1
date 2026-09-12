[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v\d+\.\d+\.\d+$')]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [switch]$GeneratedAfterRelease,

    [string]$GenerationNote,

    [string]$Repository = 'AsiBackbone/Learning'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-SpdxIdPart {
    param([Parameter(Mandatory = $true)][string]$Value)

    $part = [regex]::Replace($Value, '[^A-Za-z0-9.-]+', '-').Trim('-')
    return $(if ([string]::IsNullOrWhiteSpace($part)) { 'unknown' } else { $part })
}

function Get-GitBlobBytes {
    param(
        [Parameter(Mandatory = $true)][string]$GitRoot,
        [Parameter(Mandatory = $true)][string]$Revision,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'git'
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('-C')
    $startInfo.ArgumentList.Add($GitRoot)
    $startInfo.ArgumentList.Add('cat-file')
    $startInfo.ArgumentList.Add('blob')
    $startInfo.ArgumentList.Add("${Revision}:$Path")

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $memory = [System.IO.MemoryStream]::new()
    try {
        $process.StandardOutput.BaseStream.CopyTo($memory)
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "Unable to read '$Path' from commit '$Revision': $errorText"
        }
        return $memory.ToArray()
    }
    finally {
        $memory.Dispose()
        $process.Dispose()
    }
}

if ($GeneratedAfterRelease -and [string]::IsNullOrWhiteSpace($GenerationNote)) {
    throw 'GenerationNote is required when GeneratedAfterRelease is set.'
}

if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository '$Repository' must use the owner/name form."
}

$version = $TagName.Substring(1)
$sourceCommit = $SourceCommit.ToLowerInvariant()
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path

$headOutput = @(& git -C $root rev-parse HEAD 2>&1)
if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
    throw "Unable to resolve repository HEAD: $($headOutput -join "`n")"
}
$headCommit = ([string]$headOutput[0]).Trim()
if ($headCommit -ne $sourceCommit) {
    throw "Repository HEAD '$headCommit' does not match source commit '$sourceCommit'."
}

$tagOutput = @(& git -C $root rev-list -n 1 $TagName 2>&1)
if ($LASTEXITCODE -ne 0 -or $tagOutput.Count -ne 1) {
    throw "Unable to resolve tag '$TagName': $($tagOutput -join "`n")"
}
$tagCommit = ([string]$tagOutput[0]).Trim()
if ($tagCommit -ne $sourceCommit) {
    throw "Tag '$TagName' does not resolve to source commit '$sourceCommit'."
}

$relativeFiles = @(& git -C $root ls-tree -r --name-only $sourceCommit -- samples 2>&1 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
if ($LASTEXITCODE -ne 0 -or $relativeFiles.Count -eq 0) {
    throw 'No tracked sample source files were found.'
}

$lockFiles = @($relativeFiles | Where-Object { $_ -like '*/packages.lock.json' })
if ($lockFiles.Count -eq 0) {
    throw 'No committed sample dependency lock files were found.'
}

$fileEntries = [System.Collections.Generic.List[object]]::new()
$relationships = [System.Collections.Generic.List[object]]::new()
$verificationHashes = [System.Collections.Generic.List[string]]::new()
$rootPackageId = 'SPDXRef-Package-Learning-Samples'

foreach ($relativeFile in $relativeFiles) {
    $fileBytes = Get-GitBlobBytes -GitRoot $root -Revision $sourceCommit -Path $relativeFile
    $sha1 = [System.BitConverter]::ToString([System.Security.Cryptography.SHA1]::HashData($fileBytes)).Replace('-', '').ToLowerInvariant()
    $sha256 = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::HashData($fileBytes)).Replace('-', '').ToLowerInvariant()
    $verificationHashes.Add($sha1)
    $fileId = 'SPDXRef-File-{0}' -f (ConvertTo-SpdxIdPart -Value $relativeFile)
    $fileEntries.Add([ordered]@{
        fileName = "./$relativeFile"
        SPDXID = $fileId
        checksums = @(
            [ordered]@{ algorithm = 'SHA1'; checksumValue = $sha1 },
            [ordered]@{ algorithm = 'SHA256'; checksumValue = $sha256 }
        )
        licenseConcluded = 'NOASSERTION'
        copyrightText = 'NOASSERTION'
    })
    $relationships.Add([ordered]@{
        spdxElementId = $rootPackageId
        relationshipType = 'CONTAINS'
        relatedSpdxElement = $fileId
    })
}

$dependencyMap = @{}
foreach ($lockRelativePath in $lockFiles) {
    $lockBytes = Get-GitBlobBytes -GitRoot $root -Revision $sourceCommit -Path $lockRelativePath
    $lock = [System.Text.Encoding]::UTF8.GetString($lockBytes) | ConvertFrom-Json
    if ($lock.version -ne 2) {
        throw "Unsupported NuGet lock-file version in '$lockRelativePath'."
    }

    foreach ($frameworkProperty in $lock.dependencies.PSObject.Properties) {
        foreach ($packageProperty in $frameworkProperty.Value.PSObject.Properties) {
            $packageName = [string]$packageProperty.Name
            $package = $packageProperty.Value
            $packageType = [string]$package.type
            if ($packageType -eq 'Project') {
                continue
            }
            $resolvedVersion = [string]$package.resolved
            if ([string]::IsNullOrWhiteSpace($resolvedVersion)) {
                throw "Package '$packageName' in '$lockRelativePath' has no resolved version."
            }

            $key = "$($packageName.ToLowerInvariant())|$($resolvedVersion.ToLowerInvariant())"
            if (-not $dependencyMap.ContainsKey($key)) {
                $dependencyMap[$key] = [ordered]@{
                    name = $packageName
                    version = $resolvedVersion
                    usages = [System.Collections.Generic.List[string]]::new()
                }
            }

            $dependencyMap[$key].usages.Add("$lockRelativePath [$($frameworkProperty.Name); $packageType]")
        }
    }
}

$packages = [System.Collections.Generic.List[object]]::new()
$verificationInput = ($verificationHashes.ToArray() | Sort-Object) -join ''
$verificationBytes = [System.Text.Encoding]::ASCII.GetBytes($verificationInput)
$verificationCode = [System.BitConverter]::ToString([System.Security.Cryptography.SHA1]::HashData($verificationBytes)).Replace('-', '').ToLowerInvariant()

$packages.Add([ordered]@{
    name = 'AsiBackbone Learning executable samples'
    SPDXID = $rootPackageId
    versionInfo = $version
    downloadLocation = "git+https://github.com/$Repository.git@$sourceCommit"
    filesAnalyzed = $true
    packageVerificationCode = [ordered]@{ packageVerificationCodeValue = $verificationCode }
    licenseConcluded = 'NOASSERTION'
    licenseDeclared = 'NOASSERTION'
    copyrightText = 'NOASSERTION'
    comment = 'Scope is the tracked samples/ source tree. Licensing is component-specific: executable sample projects and source are MIT; surrounding educational text is CC BY 4.0. See LICENSING.md.'
    externalRefs = @([ordered]@{
        referenceCategory = 'OTHER'
        referenceType = 'vcs'
        referenceLocator = "git+https://github.com/$Repository.git@$sourceCommit"
    })
})

$relationships.Add([ordered]@{
    spdxElementId = 'SPDXRef-DOCUMENT'
    relationshipType = 'DESCRIBES'
    relatedSpdxElement = $rootPackageId
})

foreach ($dependency in @($dependencyMap.Values | Sort-Object name, version)) {
    $dependencyId = 'SPDXRef-Package-NuGet-{0}' -f (ConvertTo-SpdxIdPart -Value "$($dependency.name)-$($dependency.version)")
    $packages.Add([ordered]@{
        name = $dependency.name
        SPDXID = $dependencyId
        versionInfo = $dependency.version
        downloadLocation = "https://www.nuget.org/packages/$($dependency.name)/$($dependency.version)"
        filesAnalyzed = $false
        licenseConcluded = 'NOASSERTION'
        licenseDeclared = 'NOASSERTION'
        copyrightText = 'NOASSERTION'
        comment = "Resolved from committed NuGet lock files: $((@($dependency.usages) | Sort-Object -Unique) -join '; ')"
        externalRefs = @([ordered]@{
            referenceCategory = 'PACKAGE-MANAGER'
            referenceType = 'purl'
            referenceLocator = "pkg:nuget/$([uri]::EscapeDataString($dependency.name))@$([uri]::EscapeDataString($dependency.version))"
        })
    })
    $relationships.Add([ordered]@{
        spdxElementId = $rootPackageId
        relationshipType = 'DEPENDS_ON'
        relatedSpdxElement = $dependencyId
    })
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
if (@(Get-ChildItem -LiteralPath $outputRoot -Force).Count -ne 0) {
    throw "SBOM output directory must be empty: $outputRoot"
}

$createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$document = [ordered]@{
    spdxVersion = 'SPDX-2.3'
    dataLicense = 'CC0-1.0'
    SPDXID = 'SPDXRef-DOCUMENT'
    name = "AsiBackbone Learning samples $version"
    documentNamespace = "https://github.com/$Repository/sbom/samples/$version/$sourceCommit"
    creationInfo = [ordered]@{
        created = $createdUtc
        creators = @('Tool: New-LearningSamplesSbom.ps1', "Organization: $Repository")
        comment = "Generated with PowerShell $($PSVersionTable.PSVersion)."
    }
    documentComment = 'This inventory covers the tracked samples/ source tree and the exact NuGet dependencies resolved in committed packages.lock.json files. It is not a vulnerability report and does not establish that dependencies or samples are vulnerability-free.'
    packages = @($packages.ToArray())
    files = @($fileEntries.ToArray())
    relationships = @($relationships.ToArray())
    annotations = @([ordered]@{
        annotationDate = $createdUtc
        annotationType = 'OTHER'
        annotator = "Tool: New-LearningSamplesSbom.ps1"
        comment = $(if ($GeneratedAfterRelease) { $GenerationNote } else { 'Generated by the stable release evidence workflow.' })
    })
}

$sbomFileName = "learning-samples-$version.spdx.json"
$sbomPath = Join-Path $outputRoot $sbomFileName
$document | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $sbomPath -Encoding utf8 -NoNewline

$metadata = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    releaseTag = $TagName
    releaseVersion = $version
    sourceCommit = $sourceCommit
    createdUtc = $createdUtc
    generatedAfterRelease = [bool]$GeneratedAfterRelease
    generationNote = $(if ([string]::IsNullOrWhiteSpace($GenerationNote)) { $null } else { $GenerationNote })
    generator = [ordered]@{ name = 'New-LearningSamplesSbom.ps1'; powershellVersion = [string]$PSVersionTable.PSVersion }
    scope = [ordered]@{
        sourceRoot = 'samples/'
        sourceFileCount = $fileEntries.Count
        lockFileCount = $lockFiles.Count
        resolvedNuGetPackageCount = $dependencyMap.Count
        sourceFiles = @($relativeFiles)
        lockFiles = @($lockFiles)
    }
    sbomFile = $sbomFileName
    sbomSha256 = (Get-FileHash -LiteralPath $sbomPath -Algorithm SHA256).Hash.ToLowerInvariant()
    generationCommand = "./scripts/New-LearningSamplesSbom.ps1 -RepositoryRoot . -OutputDirectory <directory> -TagName $TagName -SourceCommit $sourceCommit"
}

$metadata | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputRoot 'samples-sbom-metadata.json') -Encoding utf8 -NoNewline
Write-Host "Generated '$sbomFileName' for $($fileEntries.Count) tracked source files and $($dependencyMap.Count) resolved NuGet packages."
