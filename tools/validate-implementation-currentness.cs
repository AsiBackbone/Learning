using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Validates the implementation-review metadata that current, implementation-dependent Learning pages declare in
// their front matter, and reports pages whose reviewed AsiBackbone or NetCoreApplicationTemplate baseline is behind
// the selected Learning baseline in eng/implementation-baseline.json.
//
// Usage:
//   dotnet run --file tools/validate-implementation-currentness.cs                      report; fail on invalid metadata
//   dotnet run --file tools/validate-implementation-currentness.cs -- --require-current  also fail on stale reviews
//   dotnet run --file tools/validate-implementation-currentness.cs -- --self-test
//
// The tool only reads and reports. It never rewrites a page, never treats a framework-neutral teaching model as
// package API, and never asks historical material to modernize its original pins.
return args.Contains("--self-test", StringComparer.Ordinal)
    ? ImplementationCurrentnessValidator.RunSelfTest()
    : ImplementationCurrentnessValidator.Run(args.Contains("--require-current", StringComparer.Ordinal));

static partial class ImplementationCurrentnessValidator
{
    private const string BaselineManifestRelativePath = "eng/implementation-baseline.json";

    // Front-matter key prefix for each implementation repository a page can declare a review of.
    private static readonly IReadOnlyDictionary<string, string> RepositoryPrefixes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AsiBackbone"] = "asibackbone",
            ["NetCoreApplicationTemplate"] = "netcoreapplicationtemplate"
        };

    // Existing pin and status keys owned by tools/validate-asibackbone-api-references.cs.
    private static readonly IReadOnlyDictionary<string, string[]> PinKeySuffixes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AsiBackbone"] = ["ref", "status"],
            ["NetCoreApplicationTemplate"] = ["ref"]
        };

    private static readonly string[] ReviewKeySuffixes = ["reviewed_ref", "reviewed_on", "reference", "next_review"];

    private static readonly HashSet<string> ReferenceKinds = new(StringComparer.Ordinal)
    {
        "conceptual",
        "api",
        "runtime",
        "generated-template"
    };

    private static readonly HashSet<string> ReviewTriggers = new(StringComparer.Ordinal)
    {
        "major",
        "minor",
        "patch"
    };

    [GeneratedRegex(
        @"^(?<key>[A-Za-z0-9_]+):[ \t]*(?<value>.*?)[ \t]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FrontMatterLineRegex();

    [GeneratedRegex(
        @"^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseTagRegex();

    [GeneratedRegex(
        @"^\d{4}-\d{2}-\d{2}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReviewDateRegex();

    [GeneratedRegex(
        @"^[0-9a-f]{40}$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FullCommitShaRegex();

    [GeneratedRegex(
        @"https://github\.com/AsiBackbone/(?<repo>AsiBackbone|NetCoreApplicationTemplate)/(?:blob|tree|commit|releases/tag)/(?<ref>[^/\s)\]'>#?]+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ImplementationLinkRegex();

    [GeneratedRegex(
        @"^docs/getting-started/learning-[0-9-]+-release-readiness\.md$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseReadinessRecordRegex();

    public static int Run(bool requireCurrent)
    {
        string repositoryRoot;
        LearningBaseline baseline;

        try
        {
            repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
            baseline = ParseBaseline(
                ReadRequiredFile(repositoryRoot, BaselineManifestRelativePath),
                BaselineManifestRelativePath);
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        var errors = new List<string>();
        var pages = new List<PageEvaluation>();
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        string docsRoot = Path.Combine(repositoryRoot, "docs");

        foreach (string path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
                     .Where(path => !IsExcludedPath(repositoryRoot, path))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string relativePath = NormalizeRelativePath(repositoryRoot, path);
            pages.Add(EvaluatePage(relativePath, File.ReadAllText(path), baseline, today, errors));
        }

        ValidateRequiredPages(baseline, pages, errors);

        PageReview[] reviews = pages.SelectMany(page => page.Reviews).ToArray();
        string[] report = BuildReport(baseline, pages, reviews);
        WriteReport(report);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("Implementation-currentness validation failed:");

            foreach (string error in errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return 1;
        }

        PageReview[] stale = reviews.Where(review => review.Status == ReviewStatus.Stale).ToArray();

        if (requireCurrent && stale.Length > 0)
        {
            Console.Error.WriteLine(
                $"{stale.Length} implementation review(s) are older than the selected Learning baseline. Re-review each page against the selected baseline and update its *_reviewed_ref and *_reviewed_on metadata, or keep the previous baseline selected.");
            return 1;
        }

        return 0;
    }

    public static int RunSelfTest()
    {
        var failures = new List<string>();
        var today = new DateOnly(2026, 10, 23);
        LearningBaseline baseline = ParseBaseline(
            """
            {
              "selectedBaselines": { "AsiBackbone": "v7.2.0", "NetCoreApplicationTemplate": "v2.11.2" },
              "requiredPages": [ "docs/required.md" ]
            }
            """,
            "self-test.json");

        string currentApiPage =
            "---\nasibackbone_reviewed_ref: v7.2.0\nasibackbone_reviewed_on: \"2026-10-11\"\nasibackbone_reference: api\n---\n" +
            $"[Source]({SourceLink("AsiBackbone", "blob", "v7.2.0")})\n";

        AssertPage("a review at the selected baseline is current", currentApiPage, baseline, today, 0, ReviewStatus.Current, failures);
        AssertPage(
            "a review behind the selected baseline is stale",
            currentApiPage.Replace("v7.2.0", "v7.0.0", StringComparison.Ordinal),
            baseline,
            today,
            0,
            ReviewStatus.Stale,
            failures);
        AssertPage(
            "a minor trigger tolerates a patch-only difference",
            "---\nasibackbone_reviewed_ref: v7.2.0\nasibackbone_reviewed_on: \"2026-10-11\"\nasibackbone_reference: conceptual\nasibackbone_next_review: minor\n---\n",
            ParseBaseline("""{ "selectedBaselines": { "AsiBackbone": "v7.2.3", "NetCoreApplicationTemplate": "v2.11.2" } }""", "self-test.json"),
            today,
            0,
            ReviewStatus.WithinTrigger,
            failures);
        AssertPage(
            "a minor trigger is stale after a minor difference",
            "---\nasibackbone_reviewed_ref: v7.0.0\nasibackbone_reviewed_on: \"2026-10-11\"\nasibackbone_reference: conceptual\nasibackbone_next_review: minor\n---\n",
            baseline,
            today,
            0,
            ReviewStatus.Stale,
            failures);
        AssertPage(
            "a major trigger tolerates a minor difference",
            "---\nasibackbone_reviewed_ref: v7.0.0\nasibackbone_reviewed_on: \"2026-10-11\"\nasibackbone_reference: conceptual\nasibackbone_next_review: major\n---\n",
            baseline,
            today,
            0,
            ReviewStatus.WithinTrigger,
            failures);
        AssertPage(
            "a review ahead of the selected baseline is rejected",
            currentApiPage.Replace("v7.2.0", "v7.3.0", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "a missing review date is rejected",
            "---\nasibackbone_reviewed_ref: v7.2.0\nasibackbone_reference: api\n---\n",
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "a future review date is rejected",
            currentApiPage.Replace("2026-10-11", "2026-10-24", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "an impossible review date is rejected",
            currentApiPage.Replace("2026-10-11", "2026-02-30", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "a branch name is not a reviewed release",
            currentApiPage.Replace("asibackbone_reviewed_ref: v7.2.0", "asibackbone_reviewed_ref: main", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "an unknown reference kind is rejected",
            currentApiPage.Replace("asibackbone_reference: api", "asibackbone_reference: tutorial", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "generated-template references belong to NetCoreApplicationTemplate",
            currentApiPage.Replace("asibackbone_reference: api", "asibackbone_reference: generated-template", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "an unknown review trigger is rejected",
            currentApiPage.Replace("---\n[", "asibackbone_next_review: weekly\n---\n[", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "a misspelled review key is rejected",
            currentApiPage.Replace("---\n[", "asibackbone_reviewed: v7.2.0\n---\n[", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "a duplicated review key is rejected",
            currentApiPage.Replace("---\n[", "asibackbone_reference: api\n---\n[", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "an existing pin must agree with the reviewed release",
            currentApiPage.Replace("---\n[", "asibackbone_ref: v7.0.0\n---\n[", StringComparison.Ordinal),
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "an implementation link at another release is rejected",
            currentApiPage + $"[Old]({SourceLink("AsiBackbone", "tree", "v7.0.0", "docs")})\n",
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "a full commit SHA link is accepted",
            currentApiPage + $"[Commit]({SourceLink("AsiBackbone", "commit", "0123456789abcdef0123456789abcdef01234567", string.Empty)})\n",
            baseline,
            today,
            0,
            ReviewStatus.Current,
            failures);
        AssertPage(
            "a participating page must review every implementation repository it links",
            currentApiPage + $"[Template]({SourceLink("NetCoreApplicationTemplate", "blob", "v2.11.2")})\n",
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "historical material keeps its pins and declares no review",
            $"---\nasibackbone_ref: v6.0.0\nasibackbone_status: historical\n---\n[Old]({SourceLink("AsiBackbone", "blob", "v6.0.0")})\n",
            baseline,
            today,
            0,
            null,
            failures);
        AssertPage(
            "historical material cannot declare a currentness review",
            "---\nasibackbone_ref: v6.0.0\nasibackbone_status: historical\nasibackbone_reviewed_ref: v6.0.0\nasibackbone_reviewed_on: \"2026-10-11\"\nasibackbone_reference: api\n---\n",
            baseline,
            today,
            1,
            null,
            failures);
        AssertPage(
            "release-readiness records are historical by path",
            currentApiPage,
            baseline,
            today,
            1,
            null,
            failures,
            "docs/getting-started/learning-1-3-release-readiness.md");
        AssertPage(
            "pages without review metadata do not participate",
            $"---\ndescription: A framework-neutral teaching page.\n---\n[Source]({SourceLink("AsiBackbone", "blob", "v7.0.0")})\n",
            baseline,
            today,
            0,
            null,
            failures);

        AssertRequiredPages(
            "a required page with a review is accepted",
            [new PageEvaluation("docs/required.md", Historical: false, [new PageReview("docs/required.md", "AsiBackbone", "v7.2.0", "2026-10-11", "api", null, ReviewStatus.Current)])],
            baseline,
            0,
            failures);
        AssertRequiredPages(
            "a required page without a review is rejected",
            [new PageEvaluation("docs/required.md", Historical: false, [])],
            baseline,
            1,
            failures);
        AssertRequiredPages(
            "a missing required page is rejected",
            [],
            baseline,
            1,
            failures);
        AssertRequiredPages(
            "a historical required page is rejected",
            [new PageEvaluation("docs/required.md", Historical: true, [])],
            baseline,
            1,
            failures);

        AssertBaselineRejected(
            "a baseline that is not a release tag is rejected",
            """{ "selectedBaselines": { "AsiBackbone": "main", "NetCoreApplicationTemplate": "v2.11.2" } }""",
            failures);
        AssertBaselineRejected(
            "an unknown repository baseline is rejected",
            """{ "selectedBaselines": { "AsiBackbone": "v7.0.0", "NetCoreApplicationTemplate": "v2.11.2", "Other": "v1.0.0" } }""",
            failures);

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("Implementation-currentness validator self-test failed:");

            foreach (string failure in failures)
            {
                Console.Error.WriteLine($"- {failure}");
            }

            return 1;
        }

        Console.WriteLine("Implementation-currentness validator self-test passed.");
        return 0;
    }

    // Self-test links are assembled at run time so tools/validate-asibackbone-api-references.cs, which scans this file
    // for implementation links, does not treat the fixtures' synthetic releases as real pinned links.
    private static string SourceLink(string repository, string kind, string reference, string path = "src/Example.cs")
    {
        string link = string.Concat("https://github.com/AsiBackbone/", repository, "/", kind, "/", reference);
        return path.Length == 0 ? link : $"{link}/{path}";
    }

    private static LearningBaseline ParseBaseline(string json, string source)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("selectedBaselines", out JsonElement selected) ||
                selected.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"{source} must define a selectedBaselines object.");
            }

            var baselines = new Dictionary<string, ReleaseVersion>(StringComparer.Ordinal);

            foreach (JsonProperty property in selected.EnumerateObject())
            {
                if (!RepositoryPrefixes.ContainsKey(property.Name))
                {
                    throw new InvalidDataException(
                        $"{source} selects a baseline for unknown repository '{property.Name}'. Supported: {string.Join(", ", RepositoryPrefixes.Keys)}.");
                }

                if (property.Value.ValueKind != JsonValueKind.String ||
                    !TryParseRelease(property.Value.GetString(), out ReleaseVersion version))
                {
                    throw new InvalidDataException(
                        $"{source} must select a release tag such as v1.2.3 for {property.Name}.");
                }

                baselines[property.Name] = version;
            }

            foreach (string repository in RepositoryPrefixes.Keys.Where(repository => !baselines.ContainsKey(repository)))
            {
                throw new InvalidDataException($"{source} must select a baseline for {repository}.");
            }

            var requiredPages = new List<string>();

            if (root.TryGetProperty("requiredPages", out JsonElement required))
            {
                if (required.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"{source} requiredPages must be an array of repository-relative paths.");
                }

                foreach (JsonElement page in required.EnumerateArray())
                {
                    if (page.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(page.GetString()))
                    {
                        throw new InvalidDataException($"{source} requiredPages must contain only repository-relative paths.");
                    }

                    requiredPages.Add(page.GetString()!);
                }
            }

            return new LearningBaseline(baselines, requiredPages);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{source} is not valid JSON: {exception.Message}");
        }
    }

    private static PageEvaluation EvaluatePage(
        string relativePath,
        string text,
        LearningBaseline baseline,
        DateOnly today,
        List<string> errors)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Dictionary<string, string> frontMatter = ReadFrontMatter(relativePath, normalized, errors);
        bool historical = IsHistorical(relativePath, frontMatter);
        var reviews = new List<PageReview>();

        foreach (string key in frontMatter.Keys)
        {
            ValidateKeyName(relativePath, key, errors);
        }

        foreach ((string repository, string prefix) in RepositoryPrefixes)
        {
            bool declaresReview = ReviewKeySuffixes.Any(suffix => frontMatter.ContainsKey($"{prefix}_{suffix}"));

            if (!declaresReview)
            {
                continue;
            }

            if (historical)
            {
                errors.Add(
                    $"{relativePath} is historical material and keeps its original pins; remove its {prefix}_* review metadata instead of modernizing it.");
                continue;
            }

            PageReview? review = ReadReview(relativePath, repository, prefix, frontMatter, baseline, today, errors);

            if (review is not null)
            {
                reviews.Add(review);
            }
        }

        if (!historical && reviews.Count > 0)
        {
            ValidateImplementationLinks(relativePath, normalized, frontMatter, reviews, errors);
        }

        return new PageEvaluation(relativePath, historical, reviews);
    }

    private static PageReview? ReadReview(
        string relativePath,
        string repository,
        string prefix,
        IReadOnlyDictionary<string, string> frontMatter,
        LearningBaseline baseline,
        DateOnly today,
        List<string> errors)
    {
        int errorCount = errors.Count;

        frontMatter.TryGetValue($"{prefix}_reviewed_ref", out string? reviewedRef);
        frontMatter.TryGetValue($"{prefix}_reviewed_on", out string? reviewedOn);
        frontMatter.TryGetValue($"{prefix}_reference", out string? reference);
        frontMatter.TryGetValue($"{prefix}_next_review", out string? trigger);

        ReleaseVersion reviewed = default;

        if (!TryParseRelease(reviewedRef, out reviewed))
        {
            errors.Add(
                $"{relativePath} must declare {prefix}_reviewed_ref as the {repository} release tag the page was reviewed against, such as v1.2.3.");
        }

        if (string.IsNullOrEmpty(reviewedOn) ||
            !ReviewDateRegex().IsMatch(reviewedOn) ||
            !DateOnly.TryParseExact(reviewedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly reviewDate))
        {
            errors.Add($"{relativePath} must declare {prefix}_reviewed_on as a quoted review date such as \"2026-10-11\".");
        }
        else if (reviewDate > today)
        {
            errors.Add($"{relativePath} declares {prefix}_reviewed_on: {reviewedOn}, which is in the future.");
        }

        if (string.IsNullOrEmpty(reference) || !ReferenceKinds.Contains(reference))
        {
            errors.Add(
                $"{relativePath} must declare {prefix}_reference as one of: {string.Join(", ", ReferenceKinds)}.");
        }
        else if (reference == "generated-template" && repository != "NetCoreApplicationTemplate")
        {
            errors.Add(
                $"{relativePath} declares {prefix}_reference: generated-template, which applies only to NetCoreApplicationTemplate.");
        }

        if (trigger is not null && !ReviewTriggers.Contains(trigger))
        {
            errors.Add($"{relativePath} declares {prefix}_next_review: {trigger}. Use major, minor, or patch.");
        }

        if (frontMatter.TryGetValue($"{prefix}_ref", out string? pinnedRef) &&
            reviewedRef is not null &&
            !pinnedRef.Equals(reviewedRef, StringComparison.Ordinal))
        {
            errors.Add(
                $"{relativePath} pins {prefix}_ref: {pinnedRef} but declares {prefix}_reviewed_ref: {reviewedRef}. A page is reviewed against the release it pins.");
        }

        ReleaseVersion selected = baseline.Selected[repository];

        if (errors.Count == errorCount && reviewed.CompareTo(selected) > 0)
        {
            errors.Add(
                $"{relativePath} declares {prefix}_reviewed_ref: {reviewedRef}, which is newer than the selected Learning baseline {selected}. Select the newer baseline in {BaselineManifestRelativePath} first.");
        }

        if (errors.Count != errorCount)
        {
            return null;
        }

        return new PageReview(
            relativePath,
            repository,
            reviewedRef!,
            reviewedOn!,
            reference!,
            trigger,
            Classify(reviewed, selected, trigger));
    }

    // A review is stale when the selected baseline is newer at or above the level its trigger names. Without a
    // trigger, any newer selected baseline makes the review stale.
    private static ReviewStatus Classify(ReleaseVersion reviewed, ReleaseVersion selected, string? trigger)
    {
        if (reviewed.CompareTo(selected) == 0)
        {
            return ReviewStatus.Current;
        }

        string difference = selected.Major != reviewed.Major
            ? "major"
            : selected.Minor != reviewed.Minor ? "minor" : "patch";

        bool triggered = (trigger ?? "patch") switch
        {
            "major" => difference == "major",
            "minor" => difference is "major" or "minor",
            _ => true
        };

        return triggered ? ReviewStatus.Stale : ReviewStatus.WithinTrigger;
    }

    // Implementation links on a participating page must point at the release it was reviewed against, or at an
    // exact commit, so a reviewed baseline describes every implementation link on the page.
    private static void ValidateImplementationLinks(
        string relativePath,
        string text,
        IReadOnlyDictionary<string, string> frontMatter,
        List<PageReview> reviews,
        List<string> errors)
    {
        Dictionary<string, string> reviewedRefs = reviews.ToDictionary(
            review => review.Repository,
            review => review.ReviewedRef,
            StringComparer.OrdinalIgnoreCase);
        var unreviewedRepositories = new SortedSet<string>(StringComparer.Ordinal);

        foreach (Match link in ImplementationLinkRegex().Matches(text))
        {
            string repository = RepositoryPrefixes.Keys.First(
                key => key.Equals(link.Groups["repo"].Value, StringComparison.OrdinalIgnoreCase));
            string linkRef = link.Groups["ref"].Value;

            if (!reviewedRefs.TryGetValue(repository, out string? reviewedRef))
            {
                // A repository with its own declared review keys already reported its errors.
                if (!ReviewKeySuffixes.Any(suffix => frontMatter.ContainsKey($"{RepositoryPrefixes[repository]}_{suffix}")))
                {
                    unreviewedRepositories.Add(repository);
                }

                continue;
            }

            if (!linkRef.Equals(reviewedRef, StringComparison.Ordinal) && !FullCommitShaRegex().IsMatch(linkRef))
            {
                errors.Add(
                    $"{relativePath}:{GetLineNumber(text, link.Index)} links {repository} at '{linkRef}', but the page was reviewed against {reviewedRef}. Pin the link to the reviewed release or an exact commit.");
            }
        }

        foreach (string repository in unreviewedRepositories)
        {
            errors.Add(
                $"{relativePath} links {repository} implementation source but declares no {RepositoryPrefixes[repository]}_reviewed_ref review.");
        }
    }

    private static void ValidateRequiredPages(
        LearningBaseline baseline,
        IReadOnlyList<PageEvaluation> pages,
        List<string> errors)
    {
        Dictionary<string, PageEvaluation> byPath = pages.ToDictionary(page => page.RelativePath, StringComparer.Ordinal);

        foreach (string requiredPage in baseline.RequiredPages)
        {
            if (!byPath.TryGetValue(requiredPage, out PageEvaluation? page))
            {
                errors.Add(
                    $"{BaselineManifestRelativePath} requires {requiredPage}, which does not exist. Update the manifest if the page moved.");
            }
            else if (page.Historical)
            {
                errors.Add(
                    $"{BaselineManifestRelativePath} requires {requiredPage}, but the page is historical material. Remove it from requiredPages.");
            }
            else if (page.Reviews.Count == 0)
            {
                errors.Add(
                    $"{requiredPage} must declare a reviewed AsiBackbone or NetCoreApplicationTemplate baseline in its front matter.");
            }
        }
    }

    private static Dictionary<string, string> ReadFrontMatter(string relativePath, string text, List<string> errors)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] lines = text.Split('\n');

        if (lines.Length == 0 || !string.Equals(lines[0].TrimStart('﻿').TrimEnd(), "---", StringComparison.Ordinal))
        {
            return values;
        }

        for (int index = 1; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd();

            if (string.Equals(line, "---", StringComparison.Ordinal))
            {
                return values;
            }

            Match match = FrontMatterLineRegex().Match(line);

            if (!match.Success)
            {
                continue;
            }

            string key = match.Groups["key"].Value;

            if (!IsImplementationKey(key))
            {
                continue;
            }

            if (!values.TryAdd(key, Unquote(match.Groups["value"].Value)))
            {
                errors.Add($"{relativePath}: front-matter key '{key}' appears more than once.");
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static bool IsImplementationKey(string key)
    {
        return RepositoryPrefixes.Values.Any(prefix => key.StartsWith($"{prefix}_", StringComparison.Ordinal));
    }

    private static void ValidateKeyName(string relativePath, string key, List<string> errors)
    {
        foreach ((string repository, string prefix) in RepositoryPrefixes)
        {
            if (!key.StartsWith($"{prefix}_", StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = key[(prefix.Length + 1)..];

            if (!ReviewKeySuffixes.Contains(suffix, StringComparer.Ordinal) &&
                !PinKeySuffixes[repository].Contains(suffix, StringComparer.Ordinal))
            {
                errors.Add(
                    $"{relativePath} uses unsupported front-matter key '{key}'. Review keys are {string.Join(", ", ReviewKeySuffixes.Select(value => $"{prefix}_{value}"))}.");
            }
        }
    }

    // Release-readiness records and pages marked asibackbone_status: historical record what was true when they were
    // written. They are exempt from currentness review and keep their original pins.
    private static bool IsHistorical(string relativePath, IReadOnlyDictionary<string, string> frontMatter)
    {
        return ReleaseReadinessRecordRegex().IsMatch(relativePath) ||
               (frontMatter.TryGetValue("asibackbone_status", out string? status) &&
                status.Equals("historical", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] BuildReport(
        LearningBaseline baseline,
        IReadOnlyList<PageEvaluation> pages,
        IReadOnlyList<PageReview> reviews)
    {
        var lines = new List<string>
        {
            "## Implementation currentness",
            string.Empty,
            $"Selected Learning baseline ({BaselineManifestRelativePath}): " +
                string.Join(", ", baseline.Selected.Select(pair => $"{pair.Key} {pair.Value}")) + ".",
            string.Empty,
            $"- Participating pages: {pages.Count(page => page.Reviews.Count > 0)} ({reviews.Count} review(s))",
            $"- Current: {reviews.Count(review => review.Status == ReviewStatus.Current)}",
            $"- Behind but within the page's review trigger: {reviews.Count(review => review.Status == ReviewStatus.WithinTrigger)}",
            $"- Stale, needing review: {reviews.Count(review => review.Status == ReviewStatus.Stale)}",
            $"- Historical material, exempt: {pages.Count(page => page.Historical)}",
            string.Empty
        };

        if (reviews.Count == 0)
        {
            lines.Add("No page declares an implementation review.");
            return [.. lines];
        }

        lines.Add("| Status | Page | Repository | Reference | Reviewed | Reviewed on | Trigger |");
        lines.Add("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (PageReview review in reviews
                     .OrderBy(review => review.Status)
                     .ThenBy(review => review.RelativePath, StringComparer.Ordinal)
                     .ThenBy(review => review.Repository, StringComparer.Ordinal))
        {
            string status = review.Status switch
            {
                ReviewStatus.Stale => "Stale",
                ReviewStatus.WithinTrigger => "Within trigger",
                _ => "Current"
            };

            lines.Add(
                $"| {status} | `{review.RelativePath}` | {review.Repository} | {review.Reference} | {review.ReviewedRef} | {review.ReviewedOn} | {review.Trigger ?? "any release"} |");
        }

        return [.. lines];
    }

    private static void WriteReport(IEnumerable<string> report)
    {
        string text = string.Join(Environment.NewLine, report) + Environment.NewLine;
        Console.Write(text);

        string? summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

        if (!string.IsNullOrWhiteSpace(summaryPath))
        {
            File.AppendAllText(summaryPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static bool TryParseRelease(string? value, out ReleaseVersion version)
    {
        version = default;

        if (value is null)
        {
            return false;
        }

        Match match = ReleaseTagRegex().Match(value);

        if (!match.Success ||
            !int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minor) ||
            !int.TryParse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static void AssertPage(
        string name,
        string markdown,
        LearningBaseline baseline,
        DateOnly today,
        int expectedErrors,
        ReviewStatus? expectedStatus,
        List<string> failures,
        string relativePath = "docs/self-test.md")
    {
        var errors = new List<string>();
        PageEvaluation page = EvaluatePage(relativePath, markdown, baseline, today, errors);

        if (errors.Count != expectedErrors)
        {
            failures.Add($"{name}: expected {expectedErrors} error(s), found {errors.Count}: {string.Join(" | ", errors)}");
            return;
        }

        if (expectedStatus is { } status &&
            (page.Reviews.Count != 1 || page.Reviews[0].Status != status))
        {
            string actual = string.Join(", ", page.Reviews.Select(review => review.Status));
            failures.Add($"{name}: expected one {status} review, found [{actual}].");
        }
    }

    private static void AssertRequiredPages(
        string name,
        IReadOnlyList<PageEvaluation> pages,
        LearningBaseline baseline,
        int expectedErrors,
        List<string> failures)
    {
        var errors = new List<string>();
        ValidateRequiredPages(baseline, pages, errors);

        if (errors.Count != expectedErrors)
        {
            failures.Add($"{name}: expected {expectedErrors} error(s), found {errors.Count}: {string.Join(" | ", errors)}");
        }
    }

    private static void AssertBaselineRejected(string name, string json, List<string> failures)
    {
        try
        {
            _ = ParseBaseline(json, "self-test.json");
            failures.Add($"{name}: expected the baseline manifest to be rejected.");
        }
        catch (InvalidDataException)
        {
            // Expected.
        }
    }

    private static string ReadRequiredFile(string repositoryRoot, string relativePath)
    {
        string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{relativePath} is missing; it records the selected Learning implementation baseline.");
        }

        return File.ReadAllText(path);
    }

    private static bool IsExcludedPath(string repositoryRoot, string path)
    {
        return NormalizeRelativePath(repositoryRoot, path)
            .Split('/')
            .Any(segment => segment.Equals("_site", StringComparison.OrdinalIgnoreCase) ||
                            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
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

    private enum ReviewStatus
    {
        Stale,
        WithinTrigger,
        Current
    }

    private readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
    {
        public int CompareTo(ReleaseVersion other)
        {
            int major = Major.CompareTo(other.Major);

            if (major != 0)
            {
                return major;
            }

            int minor = Minor.CompareTo(other.Minor);
            return minor != 0 ? minor : Patch.CompareTo(other.Patch);
        }

        public override string ToString()
        {
            return string.Create(CultureInfo.InvariantCulture, $"v{Major}.{Minor}.{Patch}");
        }
    }

    private sealed record LearningBaseline(
        IReadOnlyDictionary<string, ReleaseVersion> Selected,
        IReadOnlyList<string> RequiredPages);

    private sealed record PageEvaluation(
        string RelativePath,
        bool Historical,
        IReadOnlyList<PageReview> Reviews);

    private sealed record PageReview(
        string RelativePath,
        string Repository,
        string ReviewedRef,
        string ReviewedOn,
        string Reference,
        string? Trigger,
        ReviewStatus Status);
}
