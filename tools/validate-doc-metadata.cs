using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

return MetadataValidator.Run();

static class MetadataValidator
{
    private static readonly Uri SiteRoot = new("https://asibackbone.github.io/Learning/");

    private static readonly Regex CanonicalRegex = new(
        "<link\\s+rel=\\\"canonical\\\"\\s+href=\\\"(?<href>[^\\\"]+)\\\">",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DescriptionRegex = new(
        "<meta\\s+name=\\\"description\\\"\\s+content=\\\"[^\\\"]*\\\">",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JsonLdRegex = new(
        "<script\\s+type=\\\"application/ld\\+json\\\">(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly ExpectedPage[] RepresentativePages =
    {
        new("index.html", SiteRoot.AbsoluteUri, Article: false),
        new("aspnetcore/index.html", new Uri(SiteRoot, "aspnetcore/").AbsoluteUri, Article: false),
        new(
            "tutorials/decision-before-execution.html",
            new Uri(SiteRoot, "tutorials/decision-before-execution.html").AbsoluteUri,
            Article: false),
        new(
            "architecture/when-aspnet-core-authorization-is-enough.html",
            new Uri(SiteRoot, "architecture/when-aspnet-core-authorization-is-enough.html").AbsoluteUri,
            Article: true)
    };

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

        string outputRoot = Path.Combine(repositoryRoot, "docs", "_site");

        if (!Directory.Exists(outputRoot))
        {
            Console.Error.WriteLine(
                "DocFX output was not found at docs/_site. Build the documentation before validating metadata.");
            return 1;
        }

        var errors = new List<string>();
        int pageCount = ValidateCanonicalUrls(outputRoot, errors);

        foreach (ExpectedPage page in RepresentativePages)
        {
            ValidateRepresentativePage(outputRoot, page, errors);
        }

        if (pageCount == 0)
        {
            errors.Add("No generated DocFX pages were found for canonical validation.");
        }

        if (errors.Count == 0)
        {
            Console.WriteLine(
                $"Validated canonical URLs across {pageCount} DocFX page(s) and metadata on {RepresentativePages.Length} representative page(s).");
            return 0;
        }

        Console.Error.WriteLine("Documentation metadata validation failed:");
        foreach (string error in errors.OrderBy(static value => value, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"- {error}");
        }

        return 1;
    }

    private static int ValidateCanonicalUrls(string outputRoot, ICollection<string> errors)
    {
        int count = 0;

        foreach (string path in Directory.EnumerateFiles(outputRoot, "*.html", SearchOption.AllDirectories))
        {
            string html = File.ReadAllText(path);

            // Full DocFX pages carry this marker; generated TOC fragments and copied resources do not.
            if (!html.Contains("<meta name=\"docfx:navrel\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            count++;
            string relativePath = NormalizePath(Path.GetRelativePath(outputRoot, path));
            MatchCollection matches = CanonicalRegex.Matches(html);

            if (matches.Count != 1)
            {
                errors.Add($"{relativePath}: expected exactly one canonical link, found {matches.Count}.");
                continue;
            }

            string expected = CanonicalFor(relativePath);
            string actual = matches[0].Groups["href"].Value;

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                errors.Add($"{relativePath}: canonical URL '{actual}' does not match '{expected}'.");
            }
        }

        return count;
    }

    private static void ValidateRepresentativePage(
        string outputRoot,
        ExpectedPage page,
        ICollection<string> errors)
    {
        string path = Path.Combine(outputRoot, page.Path.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
        {
            errors.Add($"{page.Path}: expected generated page was not found.");
            return;
        }

        string html = File.ReadAllText(path);
        ExpectCount(page.Path, "meta description", DescriptionRegex.Matches(html).Count, 1, errors);

        MatchCollection canonicals = CanonicalRegex.Matches(html);
        ExpectCount(page.Path, "canonical link", canonicals.Count, 1, errors);
        if (canonicals.Count == 1 &&
            !string.Equals(canonicals[0].Groups["href"].Value, page.CanonicalUrl, StringComparison.Ordinal))
        {
            errors.Add($"{page.Path}: canonical URL must be '{page.CanonicalUrl}'.");
        }

        MatchCollection jsonLd = JsonLdRegex.Matches(html);
        ExpectCount(page.Path, "JSON-LD block", jsonLd.Count, 1, errors);
        if (jsonLd.Count != 1)
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonLd[0].Groups["json"].Value);
            ValidateStructuredData(page, document.RootElement, errors);
        }
        catch (JsonException exception)
        {
            errors.Add($"{page.Path}: JSON-LD is invalid JSON ({exception.Message}).");
        }
    }

    private static void ValidateStructuredData(
        ExpectedPage page,
        JsonElement root,
        ICollection<string> errors)
    {
        if (!root.TryGetProperty("@context", out JsonElement context) ||
            context.ValueKind != JsonValueKind.String ||
            !string.Equals(context.GetString(), "https://schema.org", StringComparison.Ordinal))
        {
            errors.Add($"{page.Path}: JSON-LD must use the https://schema.org context.");
        }

        if (!root.TryGetProperty("@graph", out JsonElement graph) || graph.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{page.Path}: JSON-LD must contain an @graph array.");
            return;
        }

        JsonElement? website = FindNode(graph, "WebSite");
        JsonElement? publisher = FindNode(graph, "Organization");
        JsonElement? webPage = FindNode(graph, "WebPage");
        JsonElement? article = FindNode(graph, "Article");

        ExpectProperty(page.Path, website, "WebSite", "url", SiteRoot.AbsoluteUri, errors);
        ExpectProperty(page.Path, website, "WebSite", "name", "ASI Backbone Learning", errors);
        ExpectProperty(
            page.Path,
            website,
            "WebSite",
            "alternateName",
            "Accountable Systems Infrastructure (ASI) Backbone Learning",
            errors);

        ExpectProperty(page.Path, publisher, "Organization", "name", "ASI Backbone", errors);
        ExpectProperty(page.Path, publisher, "Organization", "url", "https://github.com/AsiBackbone", errors);
        ExpectProperty(page.Path, webPage, "WebPage", "url", page.CanonicalUrl, errors);

        if (!page.Article)
        {
            if (article is not null)
            {
                errors.Add($"{page.Path}: general documentation should not be emitted as an Article.");
            }

            return;
        }

        if (article is null)
        {
            errors.Add($"{page.Path}: feed-enabled authored page is missing an Article node.");
            return;
        }

        ExpectProperty(page.Path, article, "Article", "url", page.CanonicalUrl, errors);
        ExpectNonEmptyProperty(page.Path, article.Value, "Article", "headline", errors);
        ExpectNonEmptyProperty(page.Path, article.Value, "Article", "datePublished", errors);

        if (!article.Value.TryGetProperty("author", out JsonElement author) ||
            (author.ValueKind != JsonValueKind.Object && author.ValueKind != JsonValueKind.Array))
        {
            errors.Add($"{page.Path}: Article node must include author metadata.");
        }
    }

    private static JsonElement? FindNode(JsonElement graph, string type)
    {
        foreach (JsonElement node in graph.EnumerateArray())
        {
            if (node.TryGetProperty("@type", out JsonElement value) &&
                value.ValueKind == JsonValueKind.String &&
                string.Equals(value.GetString(), type, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    private static void ExpectProperty(
        string path,
        JsonElement? node,
        string type,
        string property,
        string expected,
        ICollection<string> errors)
    {
        if (node is null)
        {
            errors.Add($"{path}: JSON-LD is missing a {type} node.");
            return;
        }

        if (!node.Value.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            errors.Add($"{path}: {type}.{property} must equal '{expected}'.");
        }
    }

    private static void ExpectNonEmptyProperty(
        string path,
        JsonElement node,
        string type,
        string property,
        ICollection<string> errors)
    {
        if (!node.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            errors.Add($"{path}: {type}.{property} must be a non-empty string.");
        }
    }

    private static void ExpectCount(
        string path,
        string label,
        int actual,
        int expected,
        ICollection<string> errors)
    {
        if (actual != expected)
        {
            errors.Add($"{path}: expected {expected} {label}, found {actual}.");
        }
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

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}

sealed record ExpectedPage(string Path, string CanonicalUrl, bool Article);
