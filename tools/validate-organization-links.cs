using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

return OrganizationLinkValidator.Run(args);

static partial class OrganizationLinkValidator
{
    private const string RegressionFixtureRelativePath =
        "tools/fixtures/link-validation/nonexistent-asibackbone-source-link.txt";

    [GeneratedRegex(
        @"https://github\.com/AsiBackbone/AsiBackbone/(?<kind>blob|tree)/(?<suffix>[^\s)\]>'?#]+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex OrganizationLinkRegex();

    public static int Run(string[] arguments)
    {
        if (!TryParseArguments(arguments, out string? sourceRepository, out bool selfTest))
        {
            PrintUsage();
            return 2;
        }

        string repositoryRoot;

        try
        {
            repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        string sourceRepositoryPath = Path.GetFullPath(sourceRepository!);

        if (!Directory.Exists(sourceRepositoryPath) ||
            (!Directory.Exists(Path.Combine(sourceRepositoryPath, ".git")) &&
             !File.Exists(Path.Combine(sourceRepositoryPath, ".git"))))
        {
            Console.Error.WriteLine(
                $"AsiBackbone source repository was not found at '{sourceRepositoryPath}'.");
            return 2;
        }

        IReadOnlyList<GitReference> gitReferences = ReadGitReferences(sourceRepositoryPath);

        if (gitReferences.Count == 0)
        {
            Console.Error.WriteLine(
                $"No branches or tags were found in the AsiBackbone source repository at '{sourceRepositoryPath}'.");
            return 2;
        }

        if (selfTest && !RunSelfTest(repositoryRoot, sourceRepositoryPath, gitReferences))
        {
            return 1;
        }

        IReadOnlyList<LinkReference> links = EnumerateMarkdownFiles(repositoryRoot)
            .SelectMany(path => ExtractLinks(repositoryRoot, path))
            .ToArray();
        IReadOnlyList<string> errors = ValidateLinks(sourceRepositoryPath, gitReferences, links);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("Organization-owned link validation failed:");

            foreach (string error in errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return 1;
        }

        Console.WriteLine(
            $"Validated {links.Count} AsiBackbone source link(s) against the fetched implementation repository.");
        return 0;
    }

    private static bool RunSelfTest(
        string repositoryRoot,
        string sourceRepositoryPath,
        IReadOnlyList<GitReference> gitReferences)
    {
        string fixturePath = Path.Combine(
            repositoryRoot,
            RegressionFixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fixturePath))
        {
            Console.Error.WriteLine($"Missing regression fixture '{RegressionFixtureRelativePath}'.");
            return false;
        }

        IReadOnlyList<LinkReference> fixtureLinks = ExtractLinks(repositoryRoot, fixturePath).ToArray();
        LinkReference slashRefLink = fixtureLinks.Single(link =>
            link.Url.Contains("release/7.0", StringComparison.Ordinal));
        var shorterRef = new GitReference("release", "refs/heads/release", 0);
        var slashBranch = new GitReference("release/7.0", "refs/heads/release/7.0", 0);
        var slashTag = new GitReference("release/7.0", "refs/tags/release/7.0", 2);
        ResolvedLink? resolvedSlashBranch = ResolveLink(
            slashRefLink,
            new[] { shorterRef, slashBranch });
        ResolvedLink? resolvedSlashTag = ResolveLink(slashRefLink, new[] { slashTag });
        IReadOnlyList<LinkReference> repositoryFixtureLinks = fixtureLinks
            .Where(link => !ReferenceEquals(link, slashRefLink))
            .ToArray();
        IReadOnlyList<string> fixtureErrors = ValidateLinks(
            sourceRepositoryPath,
            gitReferences,
            repositoryFixtureLinks);
        const string missingPath = "this-path-must-not-exist/issue-357.md";

        if (fixtureLinks.Count != 3 ||
            fixtureErrors.Count != 1 ||
            !fixtureErrors[0].Contains(missingPath, StringComparison.Ordinal) ||
            resolvedSlashBranch is null ||
            !resolvedSlashBranch.GitReference.FullName.Equals(
                "refs/heads/release/7.0",
                StringComparison.Ordinal) ||
            !resolvedSlashBranch.TargetPath.Equals("README.md", StringComparison.Ordinal) ||
            resolvedSlashTag is null ||
            !resolvedSlashTag.GitReference.FullName.Equals(
                "refs/tags/release/7.0",
                StringComparison.Ordinal) ||
            !resolvedSlashTag.TargetPath.Equals("README.md", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "Organization-link regression failed: the fixture must reject one nonexistent path and resolve a slash-containing ref against fetched refs.");

            foreach (string error in fixtureErrors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return false;
        }

        Console.WriteLine(
            "Organization-link regression passed: a nonexistent path fails and slash-containing branch and tag refs resolve correctly.");
        return true;
    }

    private static IReadOnlyList<string> ValidateLinks(
        string sourceRepositoryPath,
        IReadOnlyList<GitReference> gitReferences,
        IReadOnlyList<LinkReference> links)
    {
        var errors = new List<string>();
        var objectTypes = new Dictionary<string, GitObjectResult>(StringComparer.Ordinal);

        foreach (LinkReference link in links)
        {
            ResolvedLink? resolvedLink = ResolveLink(link, gitReferences);

            if (resolvedLink is null)
            {
                errors.Add(
                    $"{link.Location} does not match a fetched AsiBackbone branch or tag: {link.Url}");
                continue;
            }

            string objectSpec = resolvedLink.TargetPath.Length == 0
                ? $"{resolvedLink.GitReference.FullName}^{{tree}}"
                : $"{resolvedLink.GitReference.FullName}:{resolvedLink.TargetPath}";

            if (!objectTypes.TryGetValue(objectSpec, out GitObjectResult? result))
            {
                result = ReadGitObjectType(sourceRepositoryPath, objectSpec);
                objectTypes.Add(objectSpec, result);
            }

            if (!result.Exists)
            {
                errors.Add(
                    $"{link.Location} targets missing AsiBackbone object '{resolvedLink.GitReference.Name}/{resolvedLink.TargetPath}': {link.Url}");
                continue;
            }

            string expectedType = link.Kind.Equals("tree", StringComparison.OrdinalIgnoreCase)
                ? "tree"
                : "blob";

            if (!result.ObjectType.Equals(expectedType, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{link.Location} uses /{link.Kind}/ for '{objectSpec}', but Git reports a {result.ObjectType} object.");
            }
        }

        return errors;
    }

    private static ResolvedLink? ResolveLink(
        LinkReference link,
        IReadOnlyList<GitReference> gitReferences)
    {
        GitReference? gitReference = gitReferences
            .Where(candidate =>
                link.RefAndPath.Equals(candidate.Name, StringComparison.Ordinal) ||
                link.RefAndPath.StartsWith(candidate.Name + "/", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Name.Length)
            .ThenBy(candidate => candidate.Priority)
            .FirstOrDefault();

        if (gitReference is null)
        {
            return null;
        }

        string targetPath = link.RefAndPath.Equals(gitReference.Name, StringComparison.Ordinal)
            ? string.Empty
            : link.RefAndPath[(gitReference.Name.Length + 1)..].Trim('/');

        if ((targetPath.Length == 0 &&
             !link.Kind.Equals("tree", StringComparison.OrdinalIgnoreCase)) ||
            targetPath.StartsWith("-", StringComparison.Ordinal))
        {
            return null;
        }

        return new ResolvedLink(gitReference, targetPath);
    }

    private static GitObjectResult ReadGitObjectType(string sourceRepositoryPath, string objectSpec)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = sourceRepositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(objectSpec);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git for link validation.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? new GitObjectResult(true, output)
            : new GitObjectResult(false, string.Empty);
    }

    private static IReadOnlyList<GitReference> ReadGitReferences(string sourceRepositoryPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = sourceRepositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("for-each-ref");
        startInfo.ArgumentList.Add("--format=%(refname)");
        startInfo.ArgumentList.Add("refs/heads");
        startInfo.ArgumentList.Add("refs/remotes/origin");
        startInfo.ArgumentList.Add("refs/tags");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git for ref discovery.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not enumerate fetched AsiBackbone refs: {error.Trim()}");
        }

        return output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseGitReference)
            .Where(reference => reference is not null)
            .Cast<GitReference>()
            .GroupBy(reference => reference.Name, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(reference => reference.Priority)
                .First())
            .OrderByDescending(reference => reference.Name.Length)
            .ThenBy(reference => reference.Priority)
            .ToArray();
    }

    private static GitReference? ParseGitReference(string fullName)
    {
        const string headPrefix = "refs/heads/";
        const string remotePrefix = "refs/remotes/origin/";
        const string tagPrefix = "refs/tags/";

        if (fullName.StartsWith(headPrefix, StringComparison.Ordinal))
        {
            return new GitReference(fullName[headPrefix.Length..], fullName, 0);
        }

        if (fullName.StartsWith(remotePrefix, StringComparison.Ordinal))
        {
            string name = fullName[remotePrefix.Length..];

            return name.Equals("HEAD", StringComparison.Ordinal)
                ? null
                : new GitReference(name, fullName, 1);
        }

        return fullName.StartsWith(tagPrefix, StringComparison.Ordinal)
            ? new GitReference(fullName[tagPrefix.Length..], fullName, 2)
            : null;
    }

    private static IEnumerable<LinkReference> ExtractLinks(string repositoryRoot, string path)
    {
        string text = File.ReadAllText(path);
        string relativePath = NormalizeRelativePath(repositoryRoot, path);

        foreach (Match match in OrganizationLinkRegex().Matches(text))
        {
            string refAndPath;

            try
            {
                refAndPath = Uri.UnescapeDataString(match.Groups["suffix"].Value).Trim('/');
            }
            catch (UriFormatException)
            {
                refAndPath = match.Groups["suffix"].Value.Trim('/');
            }

            yield return new LinkReference(
                match.Value,
                match.Groups["kind"].Value,
                refAndPath,
                $"{relativePath}:{GetLineNumber(text, match.Index)}");
        }
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(repositoryRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(repositoryRoot, path));
    }

    private static bool IsExcludedPath(string repositoryRoot, string path)
    {
        string[] segments = NormalizeRelativePath(repositoryRoot, path).Split('/');

        return segments.Any(segment =>
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".link-targets", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("_site", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseArguments(
        string[] arguments,
        out string? sourceRepository,
        out bool selfTest)
    {
        sourceRepository = null;
        selfTest = false;

        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--source-repository" when index + 1 < arguments.Length:
                    sourceRepository = arguments[++index];
                    break;
                case "--self-test":
                    selfTest = true;
                    break;
                default:
                    return false;
            }
        }

        return !string.IsNullOrWhiteSpace(sourceRepository);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: dotnet run --file tools/validate-organization-links.cs -- --source-repository <path> [--self-test]");
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

    private sealed record LinkReference(
        string Url,
        string Kind,
        string RefAndPath,
        string Location);

    private sealed record ResolvedLink(
        GitReference GitReference,
        string TargetPath);

    private sealed record GitReference(
        string Name,
        string FullName,
        int Priority);

    private sealed record GitObjectResult(bool Exists, string ObjectType);
}
