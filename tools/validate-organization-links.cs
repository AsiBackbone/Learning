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
        @"https://github\.com/AsiBackbone/AsiBackbone/(?<kind>blob|tree)/(?<ref>[^/\s)\]>'?#]+)/(?<path>[^\s)\]>'?#]+)",
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

        if (selfTest && !RunSelfTest(repositoryRoot, sourceRepositoryPath))
        {
            return 1;
        }

        IReadOnlyList<LinkReference> links = EnumerateMarkdownFiles(repositoryRoot)
            .SelectMany(path => ExtractLinks(repositoryRoot, path))
            .ToArray();
        IReadOnlyList<string> errors = ValidateLinks(sourceRepositoryPath, links);

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

    private static bool RunSelfTest(string repositoryRoot, string sourceRepositoryPath)
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
        IReadOnlyList<string> fixtureErrors = ValidateLinks(sourceRepositoryPath, fixtureLinks);
        const string missingPath = "this-path-must-not-exist/issue-357.md";

        if (fixtureLinks.Count != 2 ||
            fixtureErrors.Count != 1 ||
            !fixtureErrors[0].Contains(missingPath, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "Organization-link regression failed: the fixture must contain one valid link and reject exactly one nonexistent source path.");

            foreach (string error in fixtureErrors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return false;
        }

        Console.WriteLine("Organization-link regression passed: a nonexistent GitHub source path fails validation.");
        return true;
    }

    private static IReadOnlyList<string> ValidateLinks(
        string sourceRepositoryPath,
        IReadOnlyList<LinkReference> links)
    {
        var errors = new List<string>();
        var objectTypes = new Dictionary<string, GitObjectResult>(StringComparer.Ordinal);

        foreach (LinkReference link in links)
        {
            if (link.GitRef.StartsWith("-", StringComparison.Ordinal) ||
                link.TargetPath.StartsWith("-", StringComparison.Ordinal))
            {
                errors.Add($"{link.Location} contains an invalid ref or target path: {link.Url}");
                continue;
            }

            string objectSpec = $"{link.GitRef}:{link.TargetPath}";

            if (!objectTypes.TryGetValue(objectSpec, out GitObjectResult? result))
            {
                result = ReadGitObjectType(sourceRepositoryPath, objectSpec);
                objectTypes.Add(objectSpec, result);
            }

            if (!result.Exists)
            {
                errors.Add(
                    $"{link.Location} targets missing AsiBackbone object '{objectSpec}': {link.Url}");
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

    private static IEnumerable<LinkReference> ExtractLinks(string repositoryRoot, string path)
    {
        string text = File.ReadAllText(path);
        string relativePath = NormalizeRelativePath(repositoryRoot, path);

        foreach (Match match in OrganizationLinkRegex().Matches(text))
        {
            string targetPath;

            try
            {
                targetPath = Uri.UnescapeDataString(match.Groups["path"].Value).Trim('/');
            }
            catch (UriFormatException)
            {
                targetPath = match.Groups["path"].Value.Trim('/');
            }

            yield return new LinkReference(
                match.Value,
                match.Groups["kind"].Value,
                match.Groups["ref"].Value,
                targetPath,
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
        string GitRef,
        string TargetPath,
        string Location);

    private sealed record GitObjectResult(bool Exists, string ObjectType);
}
