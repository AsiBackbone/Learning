using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
{
    return IndexNowPreparation.RunSelfTest();
}

if (!IndexNowOptions.TryParse(args, out IndexNowOptions? options, out string? optionError))
{
    Console.Error.WriteLine(optionError);
    Console.Error.WriteLine(
        "Usage: dotnet run --file tools/prepare-indexnow.cs -- " +
        "--base <git-ref> --head <git-ref> [--sitemap <path>] [--output <path>] [--dry-run]");
    return 2;
}

return IndexNowPreparation.Run(options!);

sealed class IndexNowOptions
{
    public required string BaseRef { get; init; }
    public required string HeadRef { get; init; }
    public string? SitemapPath { get; init; }
    public string? OutputPath { get; init; }
    public bool DryRun { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out IndexNowOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        string? baseRef = null;
        string? headRef = null;
        string? sitemapPath = null;
        string? outputPath = null;
        bool dryRun = false;

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];

            if (string.Equals(argument, "--dry-run", StringComparison.Ordinal))
            {
                dryRun = true;
                continue;
            }

            if (argument is "--base" or "--head" or "--sitemap" or "--output")
            {
                if (index + 1 >= arguments.Count)
                {
                    error = $"Missing value for {argument}.";
                    return false;
                }

                string value = arguments[++index];
                switch (argument)
                {
                    case "--base":
                        baseRef = value;
                        break;
                    case "--head":
                        headRef = value;
                        break;
                    case "--sitemap":
                        sitemapPath = value;
                        break;
                    case "--output":
                        outputPath = value;
                        break;
                }

                continue;
            }

            error = $"Unknown argument '{argument}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(baseRef) || string.IsNullOrWhiteSpace(headRef))
        {
            error = "Both --base and --head are required.";
            return false;
        }

        if (!dryRun && string.IsNullOrWhiteSpace(outputPath))
        {
            error = "--output is required unless --dry-run is specified.";
            return false;
        }

        options = new IndexNowOptions
        {
            BaseRef = baseRef,
            HeadRef = headRef,
            SitemapPath = sitemapPath,
            OutputPath = outputPath,
            DryRun = dryRun
        };
        return true;
    }
}

static class IndexNowPreparation
{
    private static readonly Uri SiteRoot = new("https://asibackbone.github.io/Learning/");
    private const string SiteHost = "asibackbone.github.io";
    private const string KeySourceRelativePath = "docs/indexnow-key.txt";
    private const string KeyOutputRelativePath = "indexnow-key.txt";
    private const string KeyLocation = "https://asibackbone.github.io/Learning/indexnow-key.txt";
    private const int MaximumUrlsPerSubmission = 10_000;

    private static readonly Regex KeyRegex = new(
        "^[A-Za-z0-9-]{8,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> PublishedRoots = new(
        new[]
        {
            "advanced",
            "articles",
            "ai-integration",
            "architecture",
            "aspnetcore",
            "case-studies",
            "getting-started",
            "governance",
            "labs",
            "samples",
            "security",
            "tutorials"
        },
        StringComparer.Ordinal);

    public static int Run(IndexNowOptions options)
    {
        if (!TryFindRepositoryRoot(out string repositoryRoot))
        {
            Console.Error.WriteLine(
                "IndexNow preparation failed: repository root containing docs/docfx.json was not found.");
            return 1;
        }

        string sitemapPath = ResolvePath(
            repositoryRoot,
            options.SitemapPath ?? "docs/_site/sitemap.xml");
        string outputRoot = Path.GetDirectoryName(sitemapPath) ?? string.Empty;

        if (!TryReadVerificationKey(
            repositoryRoot,
            outputRoot,
            validateBuiltCopy: true,
            out string key,
            out string? keyError))
        {
            Console.Error.WriteLine($"IndexNow preparation failed: {keyError}");
            return 1;
        }

        if (!TryReadSitemap(sitemapPath, out HashSet<string> currentUrls, out string? sitemapError))
        {
            Console.Error.WriteLine($"IndexNow preparation failed: {sitemapError}");
            return 1;
        }

        if (!TryResolveCommit(repositoryRoot, options.BaseRef, out string baseCommit, out string? baseError))
        {
            Console.Error.WriteLine($"IndexNow preparation failed: {baseError}");
            return 1;
        }

        if (!TryResolveCommit(repositoryRoot, options.HeadRef, out string headCommit, out string? headError))
        {
            Console.Error.WriteLine($"IndexNow preparation failed: {headError}");
            return 1;
        }

        if (!TrySelectChangedUrls(
            repositoryRoot,
            baseCommit,
            headCommit,
            currentUrls,
            out List<SelectedUrl> selected,
            out string? selectionError))
        {
            Console.Error.WriteLine($"IndexNow preparation failed: {selectionError}");
            return 1;
        }

        if (selected.Count > MaximumUrlsPerSubmission)
        {
            Console.Error.WriteLine(
                $"IndexNow preparation failed: selected {selected.Count} URLs, which exceeds the " +
                $"{MaximumUrlsPerSubmission}-URL IndexNow batch limit.");
            return 1;
        }

        PrintSummary(baseCommit, headCommit, selected);

        if (options.DryRun)
        {
            foreach (SelectedUrl item in selected)
            {
                Console.WriteLine($"{item.Kind.ToString().ToLowerInvariant(),-8} {item.Url}");
            }

            return 0;
        }

        string outputPath = ResolvePath(repositoryRoot, options.OutputPath!);
        WritePayload(outputPath, key, selected.Select(static item => item.Url).ToArray());
        Console.WriteLine(
            $"Prepared {NormalizePath(Path.GetRelativePath(repositoryRoot, outputPath))} " +
            $"with {selected.Count} changed URL(s).");
        return 0;
    }

    public static int RunSelfTest()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"learning-indexnow-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(temporaryRoot);
            if (!InitializeSyntheticRepository(temporaryRoot, out string baseCommit, out string headCommit, out string? initError))
            {
                return SelfTestFailure(initError ?? "could not initialize the synthetic repository.");
            }

            string outputRoot = Path.Combine(temporaryRoot, "docs", "_site");
            Directory.CreateDirectory(outputRoot);

            const string testKey = "0123456789abcdef0123456789abcdef";
            File.WriteAllText(Path.Combine(temporaryRoot, KeySourceRelativePath), testKey + "\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(outputRoot, KeyOutputRelativePath), testKey + "\n", Encoding.UTF8);

            string[] currentUrls =
            {
                SiteRoot.AbsoluteUri,
                CanonicalForSourcePath("docs/tutorials/added.md")!,
                CanonicalForSourcePath("docs/tutorials/modified.md")!,
                CanonicalForSourcePath("docs/tutorials/renamed-new.md")!
            };

            string sitemapPath = Path.Combine(outputRoot, "sitemap.xml");
            WriteSyntheticSitemap(sitemapPath, currentUrls);

            if (!TryReadVerificationKey(
                temporaryRoot,
                outputRoot,
                validateBuiltCopy: true,
                out string key,
                out string? keyError))
            {
                return SelfTestFailure(keyError ?? "verification key validation unexpectedly failed.");
            }

            if (!string.Equals(key, testKey, StringComparison.Ordinal))
            {
                return SelfTestFailure("verification key content changed while being read.");
            }

            if (!TryReadSitemap(sitemapPath, out HashSet<string> sitemapUrls, out string? sitemapError))
            {
                return SelfTestFailure(sitemapError ?? "synthetic sitemap validation unexpectedly failed.");
            }

            if (!TrySelectChangedUrls(
                temporaryRoot,
                baseCommit,
                headCommit,
                sitemapUrls,
                out List<SelectedUrl> selected,
                out string? selectionError))
            {
                return SelfTestFailure(selectionError ?? "changed URL selection unexpectedly failed.");
            }

            var expected = new[]
            {
                new SelectedUrl(CanonicalForSourcePath("docs/tutorials/added.md")!, ChangeKind.Added),
                new SelectedUrl(CanonicalForSourcePath("docs/tutorials/deleted.md")!, ChangeKind.Removed),
                new SelectedUrl(CanonicalForSourcePath("docs/tutorials/modified.md")!, ChangeKind.Modified),
                new SelectedUrl(CanonicalForSourcePath("docs/tutorials/renamed-new.md")!, ChangeKind.Added),
                new SelectedUrl(CanonicalForSourcePath("docs/tutorials/renamed-old.md")!, ChangeKind.Removed),
                new SelectedUrl(CanonicalForSourcePath("docs/tutorials/retired.md")!, ChangeKind.Removed)
            }
            .OrderBy(static item => item.Url, StringComparer.Ordinal)
            .ToArray();

            if (!selected.SequenceEqual(expected))
            {
                string actualText = string.Join(", ", selected.Select(static item => $"{item.Kind}:{item.Url}"));
                return SelfTestFailure($"unexpected changed URL selection: {actualText}");
            }

            if (selected.Any(static item => item.Url.Contains("hidden", StringComparison.Ordinal)))
            {
                return SelfTestFailure("a noindex page was selected for IndexNow notification.");
            }

            string firstPayload = Path.Combine(temporaryRoot, "payload-one.json");
            string secondPayload = Path.Combine(temporaryRoot, "payload-two.json");
            string[] selectedUrls = selected.Select(static item => item.Url).ToArray();
            WritePayload(firstPayload, testKey, selectedUrls);
            WritePayload(secondPayload, testKey, selectedUrls);

            if (!File.ReadAllBytes(firstPayload).SequenceEqual(File.ReadAllBytes(secondPayload)))
            {
                return SelfTestFailure("identical URL selections did not produce deterministic payload output.");
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(firstPayload));
            if (document.RootElement.GetProperty("urlList").GetArrayLength() != expected.Length)
            {
                return SelfTestFailure("payload URL count did not match the selected URL count.");
            }

            Console.WriteLine("IndexNow URL-selection self-test passed.");
            return 0;
        }
        catch (Exception exception)
        {
            return SelfTestFailure(exception.Message);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static bool InitializeSyntheticRepository(
        string repositoryRoot,
        out string baseCommit,
        out string headCommit,
        out string? error)
    {
        baseCommit = string.Empty;
        headCommit = string.Empty;
        error = null;

        if (!RunGit(repositoryRoot, new[] { "init", "--quiet" }, out _, out error) ||
            !RunGit(repositoryRoot, new[] { "config", "user.email", "indexnow-self-test@example.invalid" }, out _, out error) ||
            !RunGit(repositoryRoot, new[] { "config", "user.name", "IndexNow Self Test" }, out _, out error))
        {
            return false;
        }

        Directory.CreateDirectory(Path.Combine(repositoryRoot, "docs", "tutorials"));
        File.WriteAllText(Path.Combine(repositoryRoot, "docs", "docfx.json"), "{}\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/index.md", "# Home\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/tutorials/modified.md", "# Modified before\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/tutorials/deleted.md", "# Delete me\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/tutorials/renamed-old.md", "# Rename me\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/tutorials/retired.md", "# Retire me\n");
        WriteSyntheticMarkdown(
            repositoryRoot,
            "docs/tutorials/hidden.md",
            "---\n_noindex: true\n---\n# Hidden\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/internal-note.md", "# Not a DocFX content root\n");

        if (!RunGit(repositoryRoot, new[] { "add", "." }, out _, out error) ||
            !RunGit(repositoryRoot, new[] { "commit", "--quiet", "-m", "baseline" }, out _, out error) ||
            !TryResolveCommit(repositoryRoot, "HEAD", out baseCommit, out error))
        {
            return false;
        }

        WriteSyntheticMarkdown(repositoryRoot, "docs/tutorials/modified.md", "# Modified after\n");
        File.Delete(Path.Combine(repositoryRoot, "docs", "tutorials", "deleted.md"));
        File.Move(
            Path.Combine(repositoryRoot, "docs", "tutorials", "renamed-old.md"),
            Path.Combine(repositoryRoot, "docs", "tutorials", "renamed-new.md"));
        WriteSyntheticMarkdown(repositoryRoot, "docs/tutorials/added.md", "# Added\n");
        WriteSyntheticMarkdown(
            repositoryRoot,
            "docs/tutorials/retired.md",
            "---\n_noindex: true\n---\n# Retired\n");
        WriteSyntheticMarkdown(
            repositoryRoot,
            "docs/tutorials/hidden.md",
            "---\n_noindex: true\n---\n# Hidden changed\n");
        WriteSyntheticMarkdown(repositoryRoot, "docs/internal-note.md", "# Still not published\n");

        if (!RunGit(repositoryRoot, new[] { "add", "-A" }, out _, out error) ||
            !RunGit(repositoryRoot, new[] { "commit", "--quiet", "-m", "changes" }, out _, out error) ||
            !TryResolveCommit(repositoryRoot, "HEAD", out headCommit, out error))
        {
            return false;
        }

        return true;
    }

    private static void WriteSyntheticMarkdown(string repositoryRoot, string relativePath, string content)
    {
        string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteSyntheticSitemap(string path, IEnumerable<string> urls)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var document = new XDocument(
            new XElement(
                ns + "urlset",
                urls.OrderBy(static value => value, StringComparer.Ordinal)
                    .Select(url => new XElement(ns + "url", new XElement(ns + "loc", url)))));
        document.Save(path);
    }

    private static bool TrySelectChangedUrls(
        string repositoryRoot,
        string baseCommit,
        string headCommit,
        IReadOnlySet<string> currentUrls,
        out List<SelectedUrl> selected,
        out string? error)
    {
        selected = new List<SelectedUrl>();
        error = null;

        if (string.Equals(baseCommit, headCommit, StringComparison.Ordinal))
        {
            return true;
        }

        if (!TryReadGitChanges(repositoryRoot, baseCommit, headCommit, out List<GitChange> changes, out error))
        {
            return false;
        }

        var byUrl = new Dictionary<string, ChangeKind>(StringComparer.Ordinal);

        foreach (GitChange change in changes)
        {
            switch (change.Status)
            {
                case 'A':
                    AddCurrent(change.NewPath, ChangeKind.Added, currentUrls, byUrl);
                    break;

                case 'M':
                    if (change.NewPath is null || !IsPotentialPublishedMarkdownPath(change.NewPath))
                    {
                        break;
                    }

                    string? modifiedUrl = CanonicalForSourcePath(change.NewPath);
                    if (modifiedUrl is null)
                    {
                        break;
                    }

                    if (currentUrls.Contains(modifiedUrl))
                    {
                        AddSelection(byUrl, modifiedUrl, ChangeKind.Modified);
                    }
                    else if (WasIndexableAt(repositoryRoot, baseCommit, change.NewPath))
                    {
                        AddSelection(byUrl, modifiedUrl, ChangeKind.Removed);
                    }
                    break;

                case 'D':
                    AddRemoved(repositoryRoot, baseCommit, change.OldPath, byUrl);
                    break;

                case 'R':
                    AddRemoved(repositoryRoot, baseCommit, change.OldPath, byUrl);
                    AddCurrent(change.NewPath, ChangeKind.Added, currentUrls, byUrl);
                    break;
            }
        }

        selected = byUrl
            .Select(static pair => new SelectedUrl(pair.Key, pair.Value))
            .OrderBy(static item => item.Url, StringComparer.Ordinal)
            .ToList();
        return true;
    }

    private static void AddCurrent(
        string? sourcePath,
        ChangeKind kind,
        IReadOnlySet<string> currentUrls,
        IDictionary<string, ChangeKind> selections)
    {
        if (sourcePath is null || !IsPotentialPublishedMarkdownPath(sourcePath))
        {
            return;
        }

        string? url = CanonicalForSourcePath(sourcePath);
        if (url is not null && currentUrls.Contains(url))
        {
            AddSelection(selections, url, kind);
        }
    }

    private static void AddRemoved(
        string repositoryRoot,
        string baseCommit,
        string? sourcePath,
        IDictionary<string, ChangeKind> selections)
    {
        if (sourcePath is null ||
            !IsPotentialPublishedMarkdownPath(sourcePath) ||
            !WasIndexableAt(repositoryRoot, baseCommit, sourcePath))
        {
            return;
        }

        string? url = CanonicalForSourcePath(sourcePath);
        if (url is not null)
        {
            AddSelection(selections, url, ChangeKind.Removed);
        }
    }

    private static void AddSelection(
        IDictionary<string, ChangeKind> selections,
        string url,
        ChangeKind kind)
    {
        if (!selections.TryGetValue(url, out ChangeKind existing))
        {
            selections[url] = kind;
            return;
        }

        // Removal is the most important signal if multiple source changes collapse to one URL.
        if (kind == ChangeKind.Removed || existing == ChangeKind.Removed)
        {
            selections[url] = ChangeKind.Removed;
        }
        else if (kind == ChangeKind.Added || existing == ChangeKind.Added)
        {
            selections[url] = ChangeKind.Added;
        }
        else
        {
            selections[url] = ChangeKind.Modified;
        }
    }

    private static bool WasIndexableAt(string repositoryRoot, string commit, string sourcePath)
    {
        if (!RunGit(
            repositoryRoot,
            new[] { "show", $"{commit}:{sourcePath}" },
            out string content,
            out _))
        {
            return false;
        }

        return !FrontMatterSuppressesIndexing(content);
    }

    private static bool FrontMatterSuppressesIndexing(string content)
    {
        using var reader = new StringReader(content);
        string? firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), "---", StringComparison.Ordinal))
        {
            return false;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            string trimmed = line.Trim();
            if (string.Equals(trimmed, "---", StringComparison.Ordinal))
            {
                break;
            }

            int colon = trimmed.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            string key = trimmed[..colon].Trim();
            string value = trimmed[(colon + 1)..].Trim().Trim('"', '\'');

            if ((string.Equals(key, "_noindex", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(key, "noindex", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(key, "searchOption", StringComparison.OrdinalIgnoreCase) &&
                value.Contains("noindex", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(key, "redirect_url", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(value) &&
                !string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, "~", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadGitChanges(
        string repositoryRoot,
        string baseCommit,
        string headCommit,
        out List<GitChange> changes,
        out string? error)
    {
        changes = new List<GitChange>();
        error = null;

        if (!RunGit(
            repositoryRoot,
            new[]
            {
                "diff",
                "--name-status",
                "-z",
                "--find-renames=50%",
                "--diff-filter=ADMR",
                baseCommit,
                headCommit,
                "--",
                "docs"
            },
            out string output,
            out error))
        {
            return false;
        }

        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        int index = 0;

        while (index < tokens.Length)
        {
            string statusToken = tokens[index++];
            if (statusToken.Length == 0)
            {
                continue;
            }

            char status = statusToken[0];
            if (status == 'R')
            {
                if (index + 1 >= tokens.Length)
                {
                    error = "git diff returned an incomplete rename record.";
                    return false;
                }

                string oldPath = NormalizePath(tokens[index++]);
                string newPath = NormalizePath(tokens[index++]);
                changes.Add(new GitChange(status, oldPath, newPath));
                continue;
            }

            if (index >= tokens.Length)
            {
                error = $"git diff returned an incomplete {status} record.";
                return false;
            }

            string path = NormalizePath(tokens[index++]);
            changes.Add(
                status == 'D'
                    ? new GitChange(status, path, null)
                    : new GitChange(status, path, path));
        }

        return true;
    }

    private static bool IsPotentialPublishedMarkdownPath(string path)
    {
        string normalized = NormalizePath(path);
        if (!normalized.StartsWith("docs/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string relative = normalized["docs/".Length..];
        if (string.Equals(relative, "index.md", StringComparison.Ordinal))
        {
            return true;
        }

        int slash = relative.IndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        string root = relative[..slash];
        return PublishedRoots.Contains(root);
    }

    private static string? CanonicalForSourcePath(string sourcePath)
    {
        if (!IsPotentialPublishedMarkdownPath(sourcePath))
        {
            return null;
        }

        string relative = NormalizePath(sourcePath)["docs/".Length..];
        string canonicalRelative;

        if (string.Equals(relative, "index.md", StringComparison.Ordinal))
        {
            canonicalRelative = string.Empty;
        }
        else if (relative.EndsWith("/index.md", StringComparison.Ordinal))
        {
            canonicalRelative = relative[..^"index.md".Length];
        }
        else
        {
            canonicalRelative = relative[..^".md".Length] + ".html";
        }

        string escaped = EscapeRelativePath(canonicalRelative);
        return new Uri(SiteRoot, escaped).AbsoluteUri;
    }

    private static string EscapeRelativePath(string relative)
    {
        if (relative.Length == 0)
        {
            return string.Empty;
        }

        bool trailingSlash = relative.EndsWith("/", StringComparison.Ordinal);
        string[] segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string escaped = string.Join("/", segments.Select(Uri.EscapeDataString));
        return trailingSlash ? escaped + "/" : escaped;
    }

    private static bool TryReadSitemap(
        string sitemapPath,
        out HashSet<string> urls,
        out string? error)
    {
        urls = new HashSet<string>(StringComparer.Ordinal);
        error = null;

        if (!File.Exists(sitemapPath))
        {
            error = $"sitemap '{sitemapPath}' was not found.";
            return false;
        }

        try
        {
            XDocument document = XDocument.Load(sitemapPath, LoadOptions.None);
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XElement? root = document.Root;

            if (root is null || root.Name != ns + "urlset")
            {
                error = "sitemap.xml does not contain the expected sitemap urlset root.";
                return false;
            }

            foreach (XElement urlElement in root.Elements(ns + "url"))
            {
                XElement? locationElement = urlElement.Element(ns + "loc");
                string location = locationElement?.Value.Trim() ?? string.Empty;

                if (!Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) || !IsLearningUrl(uri))
                {
                    error = $"sitemap contains an invalid Learning URL '{location}'.";
                    return false;
                }

                if (!urls.Add(uri.AbsoluteUri))
                {
                    error = $"sitemap contains duplicate URL '{uri.AbsoluteUri}'.";
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            error = $"could not read sitemap.xml ({exception.Message}).";
            return false;
        }
    }

    private static bool IsLearningUrl(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.Host, SiteHost, StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment) &&
            uri.AbsolutePath.StartsWith(SiteRoot.AbsolutePath, StringComparison.Ordinal);
    }

    private static bool TryReadVerificationKey(
        string repositoryRoot,
        string outputRoot,
        bool validateBuiltCopy,
        out string key,
        out string? error)
    {
        key = string.Empty;
        error = null;

        string sourcePath = Path.Combine(
            repositoryRoot,
            KeySourceRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(sourcePath))
        {
            error = $"verification key file '{KeySourceRelativePath}' was not found.";
            return false;
        }

        key = File.ReadAllText(sourcePath).Trim();
        if (!KeyRegex.IsMatch(key))
        {
            error = "IndexNow verification key must be 8-128 characters using only letters, numbers, or dashes.";
            return false;
        }

        if (!validateBuiltCopy)
        {
            return true;
        }

        string builtKeyPath = Path.Combine(outputRoot, KeyOutputRelativePath);
        if (!File.Exists(builtKeyPath))
        {
            error = $"built verification file '{builtKeyPath}' was not found; DocFX must publish it at {KeyLocation}.";
            return false;
        }

        string builtKey = File.ReadAllText(builtKeyPath).Trim();
        if (!string.Equals(builtKey, key, StringComparison.Ordinal))
        {
            error = "built IndexNow verification file does not match docs/indexnow-key.txt.";
            return false;
        }

        return true;
    }

    private static void WritePayload(string outputPath, string key, IReadOnlyCollection<string> urls)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        var payload = new IndexNowPayload
        {
            Host = SiteHost,
            Key = key,
            KeyLocation = KeyLocation,
            UrlList = urls.OrderBy(static value => value, StringComparer.Ordinal).ToArray()
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("host", payload.Host);
            writer.WriteString("key", payload.Key);
            writer.WriteString("keyLocation", payload.KeyLocation);
            writer.WriteStartArray("urlList");

            foreach (string url in payload.UrlList)
            {
                writer.WriteStringValue(url);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        stream.WriteByte((byte)'\n');
        File.WriteAllBytes(outputPath, stream.ToArray());
    }

    private static void PrintSummary(string baseCommit, string headCommit, IReadOnlyCollection<SelectedUrl> selected)
    {
        int added = selected.Count(static item => item.Kind == ChangeKind.Added);
        int modified = selected.Count(static item => item.Kind == ChangeKind.Modified);
        int removed = selected.Count(static item => item.Kind == ChangeKind.Removed);

        Console.WriteLine(
            $"IndexNow change selection {ShortSha(baseCommit)}..{ShortSha(headCommit)}: " +
            $"{selected.Count} URL(s) ({added} added, {modified} modified, {removed} removed).");
    }

    private static string ShortSha(string sha) => sha.Length <= 12 ? sha : sha[..12];

    private static bool TryResolveCommit(
        string repositoryRoot,
        string reference,
        out string commit,
        out string? error)
    {
        commit = string.Empty;
        error = null;

        if (!RunGit(
            repositoryRoot,
            new[] { "rev-parse", "--verify", $"{reference}^{{commit}}" },
            out string output,
            out string? gitError))
        {
            error = $"git ref '{reference}' is not available ({gitError}).";
            return false;
        }

        commit = output.Trim();
        if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
        {
            error = $"git ref '{reference}' did not resolve to a 40-character commit SHA.";
            return false;
        }

        return true;
    }

    private static bool RunGit(
        string repositoryRoot,
        IReadOnlyCollection<string> arguments,
        out string standardOutput,
        out string? error)
    {
        standardOutput = string.Empty;
        error = null;

        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                error = "could not start git.";
                return false;
            }

            standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(standardError)
                    ? $"git exited with code {process.ExitCode}."
                    : standardError.Trim();
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryFindRepositoryRoot(out string repositoryRoot)
    {
        DirectoryInfo? current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "docs", "docfx.json")))
            {
                repositoryRoot = current.FullName;
                return true;
            }

            current = current.Parent;
        }

        repositoryRoot = string.Empty;
        return false;
    }

    private static string ResolvePath(string repositoryRoot, string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(repositoryRoot, path);

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static int SelfTestFailure(string message)
    {
        Console.Error.WriteLine($"IndexNow URL-selection self-test failed: {message}");
        return 1;
    }

    private sealed class IndexNowPayload
    {
        [JsonPropertyName("host")]
        public required string Host { get; init; }

        [JsonPropertyName("key")]
        public required string Key { get; init; }

        [JsonPropertyName("keyLocation")]
        public required string KeyLocation { get; init; }

        [JsonPropertyName("urlList")]
        public required string[] UrlList { get; init; }
    }
}

readonly record struct GitChange(char Status, string? OldPath, string? NewPath);
readonly record struct SelectedUrl(string Url, ChangeKind Kind);

enum ChangeKind
{
    Added,
    Modified,
    Removed
}
