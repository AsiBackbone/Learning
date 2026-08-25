using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
{
    return SitemapGenerator.RunSelfTest();
}

if (args.Length == 1 && string.Equals(args[0], "--validate", StringComparison.Ordinal))
{
    return SitemapGenerator.RunValidation();
}

if (args.Length > 0)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --file tools/generate-sitemap.cs [-- --self-test|--validate]");
    return 2;
}

return SitemapGenerator.Run();

static class SitemapGenerator
{
    private static readonly Uri SiteRoot = new("https://asibackbone.github.io/Learning/");
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static readonly Regex CanonicalRegex = new(
        "<link\\s+rel=\\\"canonical\\\"\\s+href=\\\"(?<href>[^\\\"]+)\\\">",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MetaTagRegex = new(
        "<meta\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlAttributeRegex = new(
        "(?<name>[A-Za-z_:][A-Za-z0-9_:.-]*)\\s*=\\s*(?<quote>[\\\"'])(?<value>.*?)\\k<quote>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] RepresentativeLocations =
    {
        SiteRoot.AbsoluteUri,
        new Uri(SiteRoot, "getting-started/").AbsoluteUri,
        new Uri(SiteRoot, "tutorials/").AbsoluteUri,
        new Uri(SiteRoot, "samples/").AbsoluteUri,
        new Uri(SiteRoot, "labs/").AbsoluteUri,
        new Uri(SiteRoot, "architecture/").AbsoluteUri,
        new Uri(SiteRoot, "aspnetcore/").AbsoluteUri,
        new Uri(SiteRoot, "case-studies/").AbsoluteUri,
        new Uri(SiteRoot, "security/").AbsoluteUri,
        new Uri(SiteRoot, "governance/").AbsoluteUri,
        new Uri(SiteRoot, "ai-integration/").AbsoluteUri,
        new Uri(SiteRoot, "advanced/").AbsoluteUri,
        new Uri(SiteRoot, "articles/").AbsoluteUri,
        new Uri(SiteRoot, "articles/2026/authorization-check-runs-too-late.html").AbsoluteUri
    };

    public static int Run()
    {
        if (!TryResolvePaths(out string repositoryRoot, out string outputRoot))
        {
            return 1;
        }

        IReadOnlyDictionary<string, DateOnly> lastModifiedDates =
            ReadGitLastModifiedDates(repositoryRoot, out string? gitWarning);

        if (gitWarning is not null)
        {
            Console.Error.WriteLine($"Sitemap generation warning: {gitWarning}");
        }

        var errors = new List<string>();
        List<SitemapEntry> entries = DiscoverEntries(outputRoot, lastModifiedDates, errors);

        if (errors.Count > 0)
        {
            return ReportErrors("Sitemap generation failed:", errors);
        }

        if (entries.Count == 0)
        {
            Console.Error.WriteLine(
                "Sitemap generation failed: no canonical indexable DocFX pages were found under docs/_site.");
            return 1;
        }

        string outputPath = Path.Combine(outputRoot, "sitemap.xml");
        WriteSitemap(outputPath, entries);

        int lastModifiedCount = entries.Count(static entry => entry.LastModified is not null);
        Console.WriteLine(
            $"Generated {NormalizePath(Path.GetRelativePath(repositoryRoot, outputPath))} " +
            $"with {entries.Count} URL(s) and {lastModifiedCount} reliable lastmod value(s).");
        return 0;
    }

    public static int RunValidation()
    {
        if (!TryResolvePaths(out string repositoryRoot, out string outputRoot))
        {
            return 1;
        }

        string sitemapPath = Path.Combine(outputRoot, "sitemap.xml");
        IReadOnlyDictionary<string, DateOnly> lastModifiedDates =
            ReadGitLastModifiedDates(repositoryRoot, out string? gitWarning);

        if (gitWarning is not null)
        {
            Console.Error.WriteLine($"Sitemap validation warning: {gitWarning}");
        }

        var errors = new List<string>();
        List<SitemapEntry> expectedEntries = DiscoverEntries(
            outputRoot,
            lastModifiedDates,
            errors);

        ValidateSitemap(outputRoot, sitemapPath, expectedEntries, RepresentativeLocations, errors);

        if (errors.Count > 0)
        {
            return ReportErrors("Sitemap validation failed:", errors);
        }

        Console.WriteLine(
            $"Validated {NormalizePath(Path.GetRelativePath(repositoryRoot, sitemapPath))} " +
            $"with {expectedEntries.Count} canonical indexable URL(s).");
        return 0;
    }

    public static int RunSelfTest()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"learning-sitemap-generator-{Guid.NewGuid():N}");

        try
        {
            string outputRoot = Path.Combine(temporaryRoot, "_site");
            Directory.CreateDirectory(outputRoot);

            WriteSyntheticPage(
                outputRoot,
                "index.html",
                SiteRoot.AbsoluteUri);
            WriteSyntheticPage(
                outputRoot,
                "getting-started/index.html",
                new Uri(SiteRoot, "getting-started/").AbsoluteUri);
            WriteSyntheticPage(
                outputRoot,
                "tutorials/example.html",
                new Uri(SiteRoot, "tutorials/example.html").AbsoluteUri);
            WriteSyntheticPage(
                outputRoot,
                "alias.html",
                new Uri(SiteRoot, "tutorials/example.html").AbsoluteUri);
            WriteSyntheticPage(
                outputRoot,
                "hidden.html",
                new Uri(SiteRoot, "hidden.html").AbsoluteUri,
                extraHead: "<meta name=\"searchOption\" content=\"noindex\">");
            WriteSyntheticPage(
                outputRoot,
                "robots-hidden.html",
                new Uri(SiteRoot, "robots-hidden.html").AbsoluteUri,
                extraHead: "<meta name=\"robots\" content=\"noindex,follow\">");
            WriteSyntheticPage(
                outputRoot,
                "redirect.html",
                new Uri(SiteRoot, "redirect.html").AbsoluteUri,
                extraHead: "<meta http-equiv=\"refresh\" content=\"0;URL='/Learning/'\">");

            var lastModifiedDates = new Dictionary<string, DateOnly>(StringComparer.Ordinal)
            {
                ["docs/index.md"] = new DateOnly(2026, 8, 18),
                ["docs/getting-started/index.md"] = new DateOnly(2026, 8, 19),
                ["docs/tutorials/example.md"] = new DateOnly(2026, 8, 20)
            };

            var discoveryErrors = new List<string>();
            List<SitemapEntry> entries = DiscoverEntries(
                outputRoot,
                lastModifiedDates,
                discoveryErrors);

            if (discoveryErrors.Count != 0)
            {
                return SelfTestFailure(
                    $"synthetic discovery returned {discoveryErrors.Count} unexpected error(s).");
            }

            string[] expectedLocations =
            {
                SiteRoot.AbsoluteUri,
                new Uri(SiteRoot, "getting-started/").AbsoluteUri,
                new Uri(SiteRoot, "tutorials/example.html").AbsoluteUri
            };

            if (!entries.Select(static entry => entry.Location)
                .SequenceEqual(expectedLocations, StringComparer.Ordinal))
            {
                return SelfTestFailure(
                    "canonical pages, aliases, redirects, or noindex exclusions were not handled as expected.");
            }

            SitemapEntry tutorial = entries.Single(
                static entry => entry.Location.EndsWith("tutorials/example.html", StringComparison.Ordinal));

            if (tutorial.LastModified != new DateOnly(2026, 8, 20))
            {
                return SelfTestFailure("a reliable source lastmod value was not preserved.");
            }

            string sitemapPath = Path.Combine(outputRoot, "sitemap.xml");
            WriteSitemap(sitemapPath, entries);

            var validationErrors = new List<string>();
            ValidateSitemap(
                outputRoot,
                sitemapPath,
                entries,
                expectedLocations,
                validationErrors);

            if (validationErrors.Count != 0)
            {
                return SelfTestFailure(
                    $"synthetic sitemap validation returned {validationErrors.Count} unexpected error(s).");
            }

            string firstOutput = File.ReadAllText(sitemapPath);
            WriteSitemap(sitemapPath, entries);
            string secondOutput = File.ReadAllText(sitemapPath);

            if (!string.Equals(firstOutput, secondOutput, StringComparison.Ordinal))
            {
                return SelfTestFailure("identical input did not produce deterministic sitemap output.");
            }

            File.WriteAllText(
                sitemapPath,
                firstOutput.Replace(
                    "</urlset>",
                    $"  <url><loc>{SiteRoot.AbsoluteUri}</loc></url>\n</urlset>",
                    StringComparison.Ordinal));

            var duplicateErrors = new List<string>();
            ValidateSitemap(
                outputRoot,
                sitemapPath,
                entries,
                expectedLocations,
                duplicateErrors);

            if (!duplicateErrors.Any(
                static error => error.Contains("duplicate sitemap URL", StringComparison.Ordinal)))
            {
                return SelfTestFailure("duplicate sitemap URLs were not rejected.");
            }

            Console.WriteLine("Sitemap generator self-test passed.");
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

    private static List<SitemapEntry> DiscoverEntries(
        string outputRoot,
        IReadOnlyDictionary<string, DateOnly> lastModifiedDates,
        ICollection<string> errors)
    {
        var entries = new List<SitemapEntry>();
        var locations = new HashSet<string>(StringComparer.Ordinal);

        foreach (string path in Directory
            .EnumerateFiles(outputRoot, "*.html", SearchOption.AllDirectories)
            .OrderBy(static value => value, StringComparer.Ordinal))
        {
            string html = File.ReadAllText(path);

            // Full DocFX pages carry this marker; generated TOC fragments and copied resources do not.
            if (!html.Contains("<meta name=\"docfx:navrel\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsNoIndex(html) || IsRedirect(html))
            {
                continue;
            }

            string relativeHtml = NormalizePath(Path.GetRelativePath(outputRoot, path));
            MatchCollection canonicalMatches = CanonicalRegex.Matches(html);

            if (canonicalMatches.Count != 1)
            {
                errors.Add(
                    $"{relativeHtml}: expected exactly one canonical link, found {canonicalMatches.Count}.");
                continue;
            }

            string canonical = canonicalMatches[0].Groups["href"].Value;

            if (!Uri.TryCreate(canonical, UriKind.Absolute, out Uri? canonicalUri) ||
                !IsLearningHttpsUri(canonicalUri))
            {
                errors.Add(
                    $"{relativeHtml}: canonical URL '{canonical}' must be an absolute HTTPS URL under {SiteRoot.AbsoluteUri}.");
                continue;
            }

            string expectedCanonical = CanonicalFor(relativeHtml);

            // A generated alias that points at another canonical page is not a sitemap entry.
            if (!string.Equals(canonical, expectedCanonical, StringComparison.Ordinal))
            {
                continue;
            }

            if (!locations.Add(canonical))
            {
                errors.Add($"{relativeHtml}: canonical URL '{canonical}' appears more than once.");
                continue;
            }

            string relativeSource = NormalizePath(Path.ChangeExtension(relativeHtml, ".md"));
            string repositorySource = $"docs/{relativeSource}";
            DateOnly? lastModified = lastModifiedDates.TryGetValue(repositorySource, out DateOnly value)
                ? value
                : null;

            entries.Add(new SitemapEntry(canonical, lastModified));
        }

        return entries
            .OrderBy(static entry => entry.Location, StringComparer.Ordinal)
            .ToList();
    }

    private static void ValidateSitemap(
        string outputRoot,
        string sitemapPath,
        IReadOnlyCollection<SitemapEntry> expectedEntries,
        IReadOnlyCollection<string> representativeLocations,
        ICollection<string> errors)
    {
        if (!File.Exists(sitemapPath))
        {
            errors.Add("sitemap.xml: generated sitemap was not found.");
            return;
        }

        var document = new XmlDocument
        {
            XmlResolver = null
        };

        try
        {
            document.Load(sitemapPath);
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            errors.Add($"sitemap.xml: malformed XML ({exception.Message}).");
            return;
        }

        XmlElement? root = document.DocumentElement;
        if (root is null ||
            !string.Equals(root.LocalName, "urlset", StringComparison.Ordinal) ||
            !string.Equals(root.NamespaceURI, SitemapNamespace, StringComparison.Ordinal))
        {
            errors.Add(
                $"sitemap.xml: root element must be urlset in the '{SitemapNamespace}' namespace.");
            return;
        }

        var namespaces = new XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("sm", SitemapNamespace);

        XmlNodeList? urlNodes = document.SelectNodes("/sm:urlset/sm:url", namespaces);
        if (urlNodes is null)
        {
            errors.Add("sitemap.xml: could not read sitemap URL entries.");
            return;
        }

        var actualLocations = new HashSet<string>(StringComparer.Ordinal);
        var actualOrder = new List<string>();
        Dictionary<string, SitemapEntry> expectedByLocation = expectedEntries
            .ToDictionary(static entry => entry.Location, StringComparer.Ordinal);

        foreach (XmlNode urlNode in urlNodes)
        {
            XmlNodeList? locNodes = urlNode.SelectNodes("sm:loc", namespaces);
            if (locNodes is null || locNodes.Count != 1)
            {
                errors.Add("sitemap.xml: every url entry must contain exactly one loc element.");
                continue;
            }

            string location = locNodes[0]?.InnerText.Trim() ?? string.Empty;
            if (location.Length == 0)
            {
                errors.Add("sitemap.xml: loc values must not be empty.");
                continue;
            }

            actualOrder.Add(location);

            if (!actualLocations.Add(location))
            {
                errors.Add($"sitemap.xml: duplicate sitemap URL '{location}'.");
            }

            if (!Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) || !IsLearningHttpsUri(uri))
            {
                errors.Add(
                    $"sitemap.xml: URL '{location}' must be an absolute HTTPS URL under {SiteRoot.AbsoluteUri}.");
                continue;
            }

            if (!TryGetGeneratedHtmlPath(outputRoot, uri, out string generatedPath, out string relativeHtml))
            {
                errors.Add($"sitemap.xml: URL '{location}' is not a canonical Learning HTML page URL.");
                continue;
            }

            if (!File.Exists(generatedPath))
            {
                errors.Add(
                    $"sitemap.xml: URL '{location}' has no generated page at '{relativeHtml}'.");
                continue;
            }

            string html = File.ReadAllText(generatedPath);
            MatchCollection canonicalMatches = CanonicalRegex.Matches(html);

            if (canonicalMatches.Count != 1)
            {
                errors.Add(
                    $"{relativeHtml}: sitemap target must contain exactly one canonical link, found {canonicalMatches.Count}.");
            }
            else if (!string.Equals(
                canonicalMatches[0].Groups["href"].Value,
                location,
                StringComparison.Ordinal))
            {
                errors.Add(
                    $"{relativeHtml}: sitemap URL '{location}' does not match the generated canonical URL " +
                    $"'{canonicalMatches[0].Groups["href"].Value}'.");
            }

            string expectedCanonical = CanonicalFor(relativeHtml);
            if (!string.Equals(location, expectedCanonical, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{relativeHtml}: sitemap URL '{location}' is not the canonical URL '{expectedCanonical}'.");
            }

            if (IsNoIndex(html))
            {
                errors.Add($"{relativeHtml}: intentionally noindexed page must not appear in the sitemap.");
            }

            if (IsRedirect(html))
            {
                errors.Add($"{relativeHtml}: redirect page must not appear in the sitemap.");
            }

            XmlNodeList? lastModifiedNodes = urlNode.SelectNodes("sm:lastmod", namespaces);
            if (lastModifiedNodes is not null && lastModifiedNodes.Count > 1)
            {
                errors.Add($"sitemap.xml: URL '{location}' contains more than one lastmod value.");
            }
            else if (lastModifiedNodes is not null && lastModifiedNodes.Count == 1 &&
                !TryParseDate(lastModifiedNodes[0]?.InnerText ?? string.Empty, out _))
            {
                errors.Add(
                    $"sitemap.xml: URL '{location}' has lastmod '{lastModifiedNodes[0]?.InnerText}' " +
                    "instead of YYYY-MM-DD.");
            }

            if (expectedByLocation.TryGetValue(location, out SitemapEntry expectedEntry))
            {
                ValidateLastModified(location, expectedEntry.LastModified, lastModifiedNodes, errors);
            }
        }

        List<string> sortedOrder = actualOrder
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();

        if (!actualOrder.SequenceEqual(sortedOrder, StringComparer.Ordinal))
        {
            errors.Add("sitemap.xml: URL entries must be sorted deterministically by canonical URL.");
        }

        var expectedLocations = new HashSet<string>(
            expectedEntries.Select(static entry => entry.Location),
            StringComparer.Ordinal);

        foreach (string expected in expectedLocations.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!actualLocations.Contains(expected))
            {
                errors.Add($"sitemap.xml: canonical indexable URL '{expected}' is missing.");
            }
        }

        foreach (string actual in actualLocations.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!expectedLocations.Contains(actual))
            {
                errors.Add($"sitemap.xml: noncanonical or nonindexable URL '{actual}' must not be included.");
            }
        }

        foreach (string representative in representativeLocations)
        {
            if (!actualLocations.Contains(representative))
            {
                errors.Add($"sitemap.xml: representative URL '{representative}' is missing.");
            }
        }
    }

    private static void ValidateLastModified(
        string location,
        DateOnly? expected,
        XmlNodeList? actualNodes,
        ICollection<string> errors)
    {
        int actualCount = actualNodes?.Count ?? 0;

        if (expected is null)
        {
            if (actualCount != 0)
            {
                errors.Add(
                    $"sitemap.xml: URL '{location}' must omit lastmod when no reliable source change date is available.");
            }

            return;
        }

        string expectedText = expected.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (actualCount != 1 ||
            !string.Equals(actualNodes?[0]?.InnerText.Trim(), expectedText, StringComparison.Ordinal))
        {
            errors.Add(
                $"sitemap.xml: URL '{location}' lastmod must equal reliable source change date '{expectedText}'.");
        }
    }

    private static IReadOnlyDictionary<string, DateOnly> ReadGitLastModifiedDates(
        string repositoryRoot,
        out string? warning)
    {
        warning = null;
        var dates = new Dictionary<string, DateOnly>(StringComparer.Ordinal);

        string gitDirectory = Path.Combine(repositoryRoot, ".git");
        if (!Directory.Exists(gitDirectory) && !File.Exists(gitDirectory))
        {
            warning = "Git history is unavailable; lastmod values will be omitted.";
            return dates;
        }

        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryRoot);
        startInfo.ArgumentList.Add("log");
        startInfo.ArgumentList.Add("--format=@@DATE@@%cs");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("docs");

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                warning = "Git history could not be started; lastmod values will be omitted.";
                return dates;
            }

            string output = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                warning = string.IsNullOrWhiteSpace(standardError)
                    ? "Git history could not be read; lastmod values will be omitted."
                    : $"Git history could not be read ({standardError.Trim()}); lastmod values will be omitted.";
                return dates;
            }

            DateOnly? currentDate = null;

            foreach (string rawLine in output.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');

                if (line.StartsWith("@@DATE@@", StringComparison.Ordinal))
                {
                    string dateText = line["@@DATE@@".Length..].Trim();
                    currentDate = TryParseDate(dateText, out DateOnly parsedDate)
                        ? parsedDate
                        : null;
                    continue;
                }

                if (currentDate is null || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string repositoryPath = NormalizePath(line.Trim());
                if (!repositoryPath.StartsWith("docs/", StringComparison.Ordinal) ||
                    !repositoryPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dates.TryAdd(repositoryPath, currentDate.Value);
            }

            return dates;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warning = $"Git history could not be read ({exception.Message}); lastmod values will be omitted.";
            return dates;
        }
    }

    private static void WriteSitemap(string outputPath, IReadOnlyCollection<SitemapEntry> entries)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace
        };

        using XmlWriter writer = XmlWriter.Create(outputPath, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("urlset", SitemapNamespace);

        foreach (SitemapEntry entry in entries.OrderBy(static value => value.Location, StringComparer.Ordinal))
        {
            writer.WriteStartElement("url", SitemapNamespace);
            writer.WriteElementString("loc", SitemapNamespace, entry.Location);

            if (entry.LastModified is DateOnly lastModified)
            {
                writer.WriteElementString(
                    "lastmod",
                    SitemapNamespace,
                    lastModified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static bool IsNoIndex(string html)
    {
        foreach (Match metaTag in MetaTagRegex.Matches(html))
        {
            IReadOnlyDictionary<string, string> attributes = ReadAttributes(metaTag.Value);
            if (!attributes.TryGetValue("name", out string? name) ||
                !attributes.TryGetValue("content", out string? content))
            {
                continue;
            }

            if ((string.Equals(name, "robots", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(name, "googlebot", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(name, "searchOption", StringComparison.OrdinalIgnoreCase)) &&
                ContainsDirective(content, "noindex"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRedirect(string html)
    {
        foreach (Match metaTag in MetaTagRegex.Matches(html))
        {
            IReadOnlyDictionary<string, string> attributes = ReadAttributes(metaTag.Value);
            if (attributes.TryGetValue("http-equiv", out string? httpEquiv) &&
                string.Equals(httpEquiv, "refresh", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in HtmlAttributeRegex.Matches(tag))
        {
            attributes[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return attributes;
    }

    private static bool ContainsDirective(string content, string directive) =>
        content
            .Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(value => string.Equals(value, directive, StringComparison.OrdinalIgnoreCase));

    private static bool IsLearningHttpsUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Host, SiteRoot.Host, StringComparison.OrdinalIgnoreCase) &&
        uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        SiteRoot.IsBaseOf(uri);

    private static bool TryGetGeneratedHtmlPath(
        string outputRoot,
        Uri canonicalUri,
        out string generatedPath,
        out string relativeHtml)
    {
        generatedPath = string.Empty;
        relativeHtml = string.Empty;

        if (!IsLearningHttpsUri(canonicalUri))
        {
            return false;
        }

        string relative = Uri.UnescapeDataString(SiteRoot.MakeRelativeUri(canonicalUri).OriginalString);
        relative = relative.Replace('\\', '/');

        if (relative.Length == 0)
        {
            relativeHtml = "index.html";
        }
        else if (relative.EndsWith("/", StringComparison.Ordinal))
        {
            relativeHtml = relative + "index.html";
        }
        else if (relative.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            relativeHtml = relative;
        }
        else
        {
            return false;
        }

        if (relativeHtml.Split('/').Any(static segment => segment is "." or ".."))
        {
            return false;
        }

        generatedPath = Path.Combine(
            outputRoot,
            relativeHtml.Replace('/', Path.DirectorySeparatorChar));
        return true;
    }

    private static string CanonicalFor(string relativePath)
    {
        const string indexName = "index.html";

        if (string.Equals(relativePath, indexName, StringComparison.OrdinalIgnoreCase))
        {
            return SiteRoot.AbsoluteUri;
        }

        if (relativePath.EndsWith("/" + indexName, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(SiteRoot, relativePath[..^indexName.Length]).AbsoluteUri;
        }

        return new Uri(SiteRoot, relativePath).AbsoluteUri;
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static bool TryResolvePaths(out string repositoryRoot, out string outputRoot)
    {
        repositoryRoot = string.Empty;
        outputRoot = string.Empty;

        try
        {
            repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return false;
        }

        outputRoot = Path.Combine(repositoryRoot, "docs", "_site");
        if (!Directory.Exists(outputRoot))
        {
            Console.Error.WriteLine(
                "DocFX output was not found at docs/_site. Build the documentation before generating or validating the sitemap.");
            return false;
        }

        return true;
    }

    private static string FindRepositoryRoot(string startingPath)
    {
        DirectoryInfo? current = new(Path.GetFullPath(startingPath));

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "docs", "docfx.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the Learning repository root from the current directory.");
    }

    private static void WriteSyntheticPage(
        string outputRoot,
        string relativePath,
        string canonical,
        string? extraHead = null)
    {
        string path = Path.Combine(
            outputRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(
            path,
            $"<!doctype html>\n<html><head>\n" +
            $"<link rel=\"canonical\" href=\"{canonical}\">\n" +
            "<meta name=\"docfx:navrel\" content=\"toc.html\">\n" +
            (extraHead is null ? string.Empty : extraHead + "\n") +
            "</head><body>synthetic page</body></html>\n");
    }

    private static int ReportErrors(string heading, IEnumerable<string> errors)
    {
        Console.Error.WriteLine(heading);

        foreach (string error in errors.OrderBy(static value => value, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"- {error}");
        }

        return 1;
    }

    private static int SelfTestFailure(string message)
    {
        Console.Error.WriteLine($"Sitemap generator self-test failed: {message}");
        return 1;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}

sealed record SitemapEntry(string Location, DateOnly? LastModified);
