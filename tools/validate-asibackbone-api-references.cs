using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

return AsiBackboneApiReferenceValidator.Run();

static partial class AsiBackboneApiReferenceValidator
{
    private const string CurrentApiBoundaryRelativePath =
        "docs/getting-started/asibackbone-7-api-boundary.md";

    private const string CurrentImplementationRef = "v7.0.0";

    private const string ValidatorRelativePath =
        "tools/validate-asibackbone-api-references.cs";

    private static readonly HashSet<string> VersionTransitionReferencePaths = new(StringComparer.Ordinal)
    {
        CurrentApiBoundaryRelativePath,
        "docs/getting-started/asibackbone-6-api-boundary.md",
        "docs/getting-started/learning-1-asibackbone-6-compatibility.md"
    };

    private static readonly HashSet<string> ImmutableHistoricalReleaseRecordPaths = new(StringComparer.Ordinal)
    {
        "RELEASE-NOTES-1.0.0.md",
        "docs/getting-started/learning-1-release-readiness.md"
    };

    private static readonly HashSet<string> ForbiddenCurrentSymbols = new(StringComparer.Ordinal)
    {
        "AsiBackboneAcknowledgmentChallenge",
        "AsiBackboneAcknowledgmentChallengeOptions",
        "AsiBackboneAcknowledgmentChallengeRequest",
        "AsiBackboneAcknowledgmentChallengeResult",
        "AsiBackboneActorContext",
        "AsiBackboneActorType",
        "AsiBackboneAspNetCoreOptions",
        "AsiBackboneAuditLedgerMetadataEntity",
        "AsiBackboneAuditLedgerMetadataEntityConfiguration",
        "AsiBackboneAuditLedgerReasonCodeEntity",
        "AsiBackboneAuditLedgerReasonCodeEntityConfiguration",
        "AsiBackboneAuditLedgerRecordEntity",
        "AsiBackboneAuditLedgerRecordEntityConfiguration",
        "AsiBackboneAuditResidueLifecycleEventEntity",
        "AsiBackboneAuditResidueLifecycleEventEntityConfiguration",
        "AsiBackboneAuditSinkContract",
        "AsiBackboneConstraintContract",
        "AsiBackboneConstraintEvaluationContext",
        "AsiBackboneContractViolationException",
        "AsiBackboneDecisionContract",
        "AsiBackboneDecisionPolicyContract",
        "AsiBackboneEndpointCapabilityGrantValidatorContract",
        "AsiBackboneEndpointGovernanceApplicationBuilderExtensions",
        "AsiBackboneEndpointGovernanceDescriptor",
        "AsiBackboneEndpointGovernanceMetadataMode",
        "AsiBackboneEndpointGovernanceMiddleware",
        "AsiBackboneEndpointGovernanceOptions",
        "AsiBackboneEndpointGovernanceResult",
        "AsiBackboneEndpointGovernanceRouteBuilderExtensions",
        "AsiBackboneEntity",
        "AsiBackboneGovernanceOutboxDrain",
        "AsiBackboneGovernanceOutboxDrainHostedService",
        "AsiBackboneGovernanceOutboxDrainWorkerOptions",
        "AsiBackboneGovernanceOutboxEntryEntity",
        "AsiBackboneGovernanceOutboxEntryEntityConfiguration",
        "AsiBackboneGovernanceOutboxOptions",
        "AsiBackboneHandshakeAcknowledgmentEntity",
        "AsiBackboneHandshakeAcknowledgmentEntityConfiguration",
        "AsiBackboneHandshakeAcknowledgmentMetadataEntity",
        "AsiBackboneHandshakeAcknowledgmentMetadataEntityConfiguration",
        "AsiBackboneHandshakeRequestEntity",
        "AsiBackboneHandshakeRequestEntityConfiguration",
        "AsiBackboneHandshakeRequestMetadataEntity",
        "AsiBackboneHandshakeRequestMetadataEntityConfiguration",
        "AsiBackboneHttpActorContextOptions",
        "AsiBackboneHttpRequestCorrelation",
        "AsiBackboneHttpRequestCorrelationAuditExtensions",
        "AsiBackboneHttpRequestMetadataKeys",
        "AsiBackboneHttpResultMappingExtensions",
        "AsiBackboneHttpResultMappingOptions",
        "AsiBackboneIdentifierLimits",
        "AsiBackbonePolicyEvaluatorBuilder",
        "AsiBackbonePolicyEvaluatorContract",
        "AsiBackbonePolicyEvaluatorOptions",
        "AsiBackboneSchemaVersions",
        "AsiBackboneTestAuditSink",
        "AsiBackboneTestHarnessEndpointCapabilityGrantValidator",
        "AsiBackboneTestHarnessOptions",
        "AsiBackboneTestHarnessPolicyEvaluator",
        "AsiBackboneTestHarnessServiceCollectionExtensions",
        "AsiBackboneTestSigningService",
        "AuditResidueLifecycleEventTests",
        "AuditResidue",
        "AuditResidueBuilder",
        "AuditResidueLifecycleEvent",
        "AuditResidueLifecycleStage",
        "BackboneResult",
        "CapabilityTokenGrant",
        "CapabilityTokens",
        "CreateExecutionBoundary",
        "DefaultAsiBackboneAcknowledgmentChallengeService",
        "DefaultAsiBackboneDlpFailurePolicyResolver",
        "DefaultAsiBackboneEndpointGovernanceService",
        "DefaultAsiBackbonePolicyEvaluator",
        "EfCoreAuditResidueLifecycleStore",
        "HttpContextAsiBackboneActorContextResolver",
        "HttpContextAsiBackboneRequestCorrelationResolver",
        "Handshakes",
        "IAsiBackboneAcknowledgmentChallengeService",
        "IAsiBackboneActorContext",
        "IAsiBackboneAuditLedgerStore",
        "IAsiBackboneAuditResidue",
        "IAsiBackboneAuditResidueLifecycleStore",
        "IAsiBackboneAuditSink",
        "IAsiBackboneConstraint",
        "IAsiBackboneConstraintEvaluationContext",
        "IAsiBackboneDecisionPolicy",
        "IAsiBackboneDlpFailurePolicyResolver",
        "IAsiBackboneEndpointAuditEmissionMetadata",
        "IAsiBackboneEndpointCapabilityGrantMetadata",
        "IAsiBackboneEndpointCapabilityGrantValidator",
        "IAsiBackboneEndpointGovernanceMetadata",
        "IAsiBackboneEndpointGovernancePolicyMetadata",
        "IAsiBackboneEndpointGovernanceService",
        "IAsiBackboneEndpointLiabilityHandshakeMetadata",
        "IAsiBackboneEndpointPolicyEvaluationOptionsMetadata",
        "IAsiBackboneEntity",
        "IAsiBackboneGovernanceEmitter",
        "IAsiBackboneGovernanceOutboxClaimOutcomeStore",
        "IAsiBackboneGovernanceOutboxClaimStore",
        "IAsiBackboneGovernanceOutboxStore",
        "IAsiBackboneHttpActorContextResolver",
        "IAsiBackboneHttpRequestCorrelationResolver",
        "IAsiBackbonePolicyEvaluator",
        "IAsiBackboneSignatureVerificationService",
        "IAsiBackboneSigningService",
        "InMemoryAuditResidueLifecycleStore",
        "LiabilityHandshakeAcknowledgment",
        "LiabilityHandshakeRequest",
        "RequireGovernancePolicy",
        "RequireGovernancePolicyAttribute"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".html",
        ".js",
        ".json",
        ".md",
        ".props",
        ".svg",
        ".targets",
        ".tmpl",
        ".xml",
        ".yaml",
        ".yml"
    };

    [GeneratedRegex(
        @"\b[A-Za-z_][A-Za-z0-9_]*\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(
        @"https://github\.com/AsiBackbone/AsiBackbone/(?:blob|tree)/(?!(?:v7\.0\.0|v6\.0\.0)(?:/|\b))[^\s)\]'>]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StaleImplementationLinkRegex();

    [GeneratedRegex(
        @"https://github\.com/AsiBackbone/AsiBackbone/(?:blob|tree)/(?<ref>[^/\s)\]'>]+)/[^\s)\]'>]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ImplementationLinkRegex();

    [GeneratedRegex(
        @"^asibackbone_(?<key>ref|status):\s*(?<value>\S+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex VersionMetadataRegex();

    public static int Run()
    {
        string repositoryRoot;

        try
        {
            repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        var errors = new List<string>();
        string[] textFiles = EnumerateTextFiles(repositoryRoot).ToArray();

        ValidateCurrentSymbolsAndLinks(repositoryRoot, textFiles, errors);
        ValidateVersionedCompatibilityPages(repositoryRoot, errors);
        int packageReferenceCount = ValidatePackageReferences(repositoryRoot, errors);
        ValidateScopeNotices(repositoryRoot, errors);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("AsiBackbone API-reference validation failed:");

            foreach (string error in errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(
                $"Use '{CurrentApiBoundaryRelativePath}' for current names and supported construction paths.");
            return 1;
        }

        string packageSummary = packageReferenceCount == 0
            ? "no AsiBackbone package references (framework-neutral sample policy)"
            : $"{packageReferenceCount} AsiBackbone 7.x package reference(s)";

        Console.WriteLine(
            $"Validated current AsiBackbone 7.0 references and immutable historical compatibility pages across {textFiles.Length} instructional file(s): {packageSummary}.");
        return 0;
    }

    private static void ValidateCurrentSymbolsAndLinks(
        string repositoryRoot,
        IEnumerable<string> files,
        List<string> errors)
    {
        foreach (string path in files)
        {
            string relativePath = NormalizeRelativePath(repositoryRoot, path);

            if (string.Equals(relativePath, ValidatorRelativePath, StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(path);

            if (!VersionTransitionReferencePaths.Contains(relativePath))
            {
                string[] lines = File.ReadAllLines(path);

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];

                    foreach (Match identifierMatch in IdentifierRegex().Matches(line))
                    {
                        if (ForbiddenCurrentSymbols.Contains(identifierMatch.Value))
                        {
                            errors.Add(
                                $"{relativePath}:{lineIndex + 1} uses a retired, removed, or renamed implementation symbol '{identifierMatch.Value}'.");
                        }
                    }
                }
            }

            if (ImmutableHistoricalReleaseRecordPaths.Contains(relativePath))
            {
                continue;
            }

            foreach (Match linkMatch in StaleImplementationLinkRegex().Matches(text))
            {
                int lineNumber = GetLineNumber(text, linkMatch.Index);
                errors.Add(
                    $"{relativePath}:{lineNumber} links implementation source outside the released v7.0.0 or historical v6.0.0 tags: {linkMatch.Value}");
            }
        }
    }

    private static void ValidateVersionedCompatibilityPages(
        string repositoryRoot,
        List<string> errors)
    {
        string gettingStarted = Path.Combine(repositoryRoot, "docs", "getting-started");

        foreach (string path in Directory.EnumerateFiles(gettingStarted, "*.md", SearchOption.TopDirectoryOnly))
        {
            string relativePath = NormalizeRelativePath(repositoryRoot, path);
            string fileName = Path.GetFileName(path);

            if (!fileName.Contains("asibackbone-", StringComparison.OrdinalIgnoreCase) ||
                (!fileName.Contains("api-boundary", StringComparison.OrdinalIgnoreCase) &&
                 !fileName.Contains("compatibility", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string text = File.ReadAllText(path);
            Dictionary<string, string> metadata = VersionMetadataRegex()
                .Matches(text)
                .ToDictionary(
                    match => match.Groups["key"].Value.ToLowerInvariant(),
                    match => match.Groups["value"].Value,
                    StringComparer.Ordinal);

            if (!metadata.TryGetValue("ref", out string? expectedRef) ||
                !metadata.TryGetValue("status", out string? status))
            {
                errors.Add($"{relativePath} must declare asibackbone_ref and asibackbone_status metadata.");
                continue;
            }

            if (status.Equals("historical", StringComparison.OrdinalIgnoreCase) &&
                expectedRef.Equals("main", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{relativePath} is historical and must use an immutable implementation ref, not main.");
            }

            bool isCurrentBoundary = relativePath.Equals(
                CurrentApiBoundaryRelativePath,
                StringComparison.Ordinal);

            if (status.Equals("current", StringComparison.OrdinalIgnoreCase))
            {
                if (!isCurrentBoundary)
                {
                    errors.Add(
                        $"{relativePath} cannot declare itself current; only '{CurrentApiBoundaryRelativePath}' may declare the current implementation boundary.");
                }

                if (!expectedRef.Equals(CurrentImplementationRef, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"{relativePath} is current and must declare asibackbone_ref: {CurrentImplementationRef}.");
                }
            }
            else if (!status.Equals("historical", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{relativePath} has unsupported asibackbone_status '{status}'. Use current or historical.");
            }

            if (!isCurrentBoundary && expectedRef.Equals("main", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{relativePath} is a versioned compatibility page and must pin an immutable implementation ref instead of main.");
            }

            foreach (Match linkMatch in ImplementationLinkRegex().Matches(text))
            {
                string actualRef = linkMatch.Groups["ref"].Value;

                if (!actualRef.Equals(expectedRef, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"{relativePath}:{GetLineNumber(text, linkMatch.Index)} uses implementation ref '{actualRef}' instead of declared ref '{expectedRef}'.");
                }
            }
        }
    }

    private static int ValidatePackageReferences(
        string repositoryRoot,
        List<string> errors)
    {
        var centralVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<PackageReference>();

        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "*.props", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories))
                     .Where(path => !IsExcludedPath(repositoryRoot, path)))
        {
            XDocument document;

            try
            {
                document = XDocument.Load(path, LoadOptions.SetLineInfo);
            }
            catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
            {
                errors.Add($"Could not parse {NormalizeRelativePath(repositoryRoot, path)}: {exception.Message}");
                continue;
            }

            foreach (XElement element in document.Descendants())
            {
                string localName = element.Name.LocalName;

                if (localName is not ("PackageReference" or "PackageVersion"))
                {
                    continue;
                }

                string? packageId = element.Attribute("Include")?.Value;

                if (!IsAsiBackbonePackage(packageId))
                {
                    continue;
                }

                string? version = element.Attribute("Version")?.Value;

                if (localName == "PackageVersion")
                {
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        centralVersions[packageId!] = version;
                    }

                    continue;
                }

                references.Add(new PackageReference(
                    packageId!,
                    version,
                    NormalizeRelativePath(repositoryRoot, path)));
            }
        }

        foreach (PackageReference reference in references)
        {
            string? version = reference.Version;

            if (string.IsNullOrWhiteSpace(version))
            {
                centralVersions.TryGetValue(reference.PackageId, out version);
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                errors.Add(
                    $"{reference.RelativePath} references {reference.PackageId} without a pinned package version.");
                continue;
            }

            string normalizedVersion = version.Trim().TrimStart('[', '(');

            if (!normalizedVersion.StartsWith("7.", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{reference.RelativePath} references {reference.PackageId} {version}; current Learning package-integration samples must use 7.x.");
            }
        }

        return references.Count;
    }

    private static void ValidateScopeNotices(
        string repositoryRoot,
        List<string> errors)
    {
        string[] noticePaths =
        {
            "docs/tutorials/index.md",
            "docs/samples/index.md",
            "samples/README.md"
        };

        foreach (string relativePath in noticePaths)
        {
            string path = Path.Combine(
                repositoryRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(path))
            {
                errors.Add($"Missing code-scope notice location '{relativePath}'.");
                continue;
            }

            string text = File.ReadAllText(path);

            if (!text.Contains("Learning-owned", StringComparison.Ordinal) ||
                !text.Contains("asibackbone-7-api-boundary.md", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{relativePath} must distinguish Learning-owned code from the AsiBackbone 7.0 API and link the current API boundary guide.");
            }
        }
    }

    private static IEnumerable<string> EnumerateTextFiles(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(repositoryRoot, path))
            .Where(path => TextExtensions.Contains(Path.GetExtension(path)));
    }

    private static bool IsExcludedPath(string repositoryRoot, string path)
    {
        string relativePath = NormalizeRelativePath(repositoryRoot, path);
        string[] segments = relativePath.Split('/');

        return segments.Any(segment =>
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("_site", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAsiBackbonePackage(string? packageId)
    {
        return !string.IsNullOrWhiteSpace(packageId) &&
               (packageId.Equals("AsiBackbone", StringComparison.OrdinalIgnoreCase) ||
                packageId.StartsWith("AsiBackbone.", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetLineNumber(string text, int characterIndex)
    {
        int lineNumber = 1;

        for (int index = 0; index < characterIndex; index++)
        {
            if (text[index] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static string NormalizeRelativePath(string repositoryRoot, string path)
    {
        return Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root from '{startDirectory}'.");
    }

    private sealed record PackageReference(
        string PackageId,
        string? Version,
        string RelativePath);
}
