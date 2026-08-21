using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;

return FeedGenerator.Run();

static class FeedGenerator
{
    private static readonly Uri SiteRoot = new("https://asibackbone.github.io/Learning/");
    private static readonly Uri FeedUri = new(SiteRoot, "feed.xml");
    private const string AtomNamespace = "http://www.w3.org/2005/Atom";
    private const string DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";
    private const string MediaNamespace = "http://search.yahoo.com/mrss/";
    private static readonly HashSet<string> PublicationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "author",
        "published",
        "updated",
        "summary",
        "feed"
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

        string docsRoot = Path.Combine(repositoryRoot, "docs");
        string siteRoot = Path.Combine(docsRoot, "_site");

        if (!Directory.Exists(siteRoot))
        {
            Console.Error.WriteLine(
                "DocFX output was not found at docs/_site. Build the documentation before generating the feed.");
            return 1;
        }

        var articles = new List<FeedArticle>();
        var errors = new List<string>();

        foreach (string sourcePath in EnumerateMarkdownFiles(docsRoot))
        {
            string relativeSource = NormalizePath(Path.GetRelativePath(docsRoot, sourcePath));

            FrontMatter? frontMatter;

            try
            {
                frontMatter = ReadFrontMatter(sourcePath);
            }
            catch (InvalidDataException exception)
            {
                errors.Add($"{relativeSource}: {exception.Message}");
                continue;
            }

            if (frontMatter is null || !frontMatter.Values.TryGetValue("feed", out string? feedValue))
            {
                continue;
            }

            if (!bool.TryParse(feedValue, out bool feedEnabled))
            {
                errors.Add($"{relativeSource}: 'feed' must be either true or false.");
                continue;
            }

            if (!feedEnabled)
            {
                continue;
            }

            ValidateFeedArticle(frontMatter.Values, relativeSource, siteRoot, articles, errors);
        }

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("RSS feed generation failed:");

            foreach (string error in errors.OrderBy(static value => value, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"- {error}");
            }

            return 1;
        }

        if (articles.Count == 0)
        {
            Console.Error.WriteLine("RSS feed generation failed: no documents with 'feed: true' were found under docs/.");
            return 1;
        }

        List<FeedArticle> orderedArticles = articles
            .OrderByDescending(static article => article.Published)
            .ThenBy(static article => article.CanonicalUri.AbsoluteUri, StringComparer.Ordinal)
            .ToList();

        string outputPath = Path.Combine(siteRoot, "feed.xml");
        WriteFeed(outputPath, orderedArticles);

        Console.WriteLine($"Generated {NormalizePath(Path.GetRelativePath(repositoryRoot, outputPath))} with {orderedArticles.Count} item(s).");
        return 0;
    }

    private static void ValidateFeedArticle(
        IReadOnlyDictionary<string, string> values,
        string relativeSource,
        string siteRoot,
        ICollection<FeedArticle> articles,
        ICollection<string> errors)
    {
        string? title = Required(values, "title", relativeSource, errors);
        string? author = Required(values, "author", relativeSource, errors);
        string? publishedText = Required(values, "published", relativeSource, errors);
        string? summary = Required(values, "summary", relativeSource, errors);

        if (title is null || author is null || publishedText is null || summary is null)
        {
            return;
        }

        if (!TryParseDate(publishedText, out DateOnly published))
        {
            errors.Add($"{relativeSource}: 'published' must use YYYY-MM-DD format.");
            return;
        }

        DateOnly? updated = null;

        if (values.TryGetValue("updated", out string? updatedText) && !string.IsNullOrWhiteSpace(updatedText))
        {
            if (!TryParseDate(updatedText, out DateOnly updatedDate))
            {
                errors.Add($"{relativeSource}: 'updated' must use YYYY-MM-DD format when present.");
                return;
            }

            if (updatedDate < published)
            {
                errors.Add($"{relativeSource}: 'updated' cannot be earlier than 'published'.");
                return;
            }

            updated = updatedDate;
        }

        string relativeHtml = NormalizePath(Path.ChangeExtension(relativeSource, ".html"));
        string generatedHtmlPath = Path.Combine(
            siteRoot,
            relativeHtml.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(generatedHtmlPath))
        {
            errors.Add(
                $"{relativeSource}: expected generated HTML target '{relativeHtml}' was not found under docs/_site.");
            return;
        }

        var canonicalUri = new Uri(SiteRoot, relativeHtml);

        articles.Add(new FeedArticle(
            title,
            author,
            published,
            updated,
            summary,
            canonicalUri));
    }

    private static string? Required(
        IReadOnlyDictionary<string, string> values,
        string key,
        string relativeSource,
        ICollection<string> errors)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{relativeSource}: feed-enabled documents require non-empty '{key}' metadata.");
            return null;
        }

        return value.Trim();
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static FrontMatter? ReadFrontMatter(string sourcePath)
    {
        string[] lines = File.ReadAllLines(sourcePath);

        if (lines.Length == 0 || !string.Equals(lines[0].TrimStart('\uFEFF'), "---", StringComparison.Ordinal))
        {
            return null;
        }

        int closingIndex = -1;

        for (int index = 1; index < lines.Length; index++)
        {
            if (string.Equals(lines[index].Trim(), "---", StringComparison.Ordinal))
            {
                closingIndex = index;
                break;
            }
        }

        if (closingIndex < 0)
        {
            throw new InvalidDataException("YAML frontmatter starts with '---' but has no closing delimiter.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 1; index < closingIndex; index++)
        {
            string line = lines[index];
            string trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#') || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            int separator = line.IndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();

            if (!PublicationKeys.Contains(key))
            {
                continue;
            }

            string rawValue = line[(separator + 1)..].Trim();

            if (rawValue.StartsWith('|') || rawValue.StartsWith('>'))
            {
                throw new InvalidDataException(
                    $"publication metadata '{key}' must use a single-line scalar value.");
            }

            if (!values.TryAdd(key, ParseScalar(rawValue)))
            {
                throw new InvalidDataException($"frontmatter key '{key}' appears more than once.");
            }
        }

        return new FrontMatter(values);
    }

    private static string ParseScalar(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(value);

                if (document.RootElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException(
                        "double-quoted frontmatter scalar must contain a string.");
                }

                return document.RootElement.GetString() ?? string.Empty;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("invalid double-quoted frontmatter scalar.", exception);
            }
        }

        return value;
    }

    private static void WriteFeed(string outputPath, IReadOnlyList<FeedArticle> articles)
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
        writer.WriteStartElement("rss");
        writer.WriteAttributeString("version", "2.0");
        writer.WriteAttributeString("xmlns", "atom", null, AtomNamespace);
        writer.WriteAttributeString("xmlns", "dc", null, DublinCoreNamespace);
        writer.WriteAttributeString("xmlns", "media", null, MediaNamespace);

        writer.WriteStartElement("channel");

        writer.WriteElementString("title", "ASI Backbone Learning");
        writer.WriteElementString("link", SiteRoot.AbsoluteUri);
        writer.WriteElementString(
            "description",
            "Long-form technical articles from Accountable Systems Infrastructure (ASI) Backbone Learning on " +
            "governed .NET decision flow and execution, secure application architecture, AI integration, and policy-" +
            "driven systems.");
        writer.WriteElementString("language", "en-us");
        writer.WriteElementString(
            "lastBuildDate",
            DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));

        writer.WriteStartElement("image");
        writer.WriteElementString(
            "url",
            new Uri(SiteRoot, "images/asibackbone-icon-50.png").AbsoluteUri);
        writer.WriteElementString("title", "ASI Backbone Learning");
        writer.WriteElementString("link", SiteRoot.AbsoluteUri);
        writer.WriteEndElement();

        writer.WriteStartElement("atom", "link", AtomNamespace);
        writer.WriteAttributeString("href", FeedUri.AbsoluteUri);
        writer.WriteAttributeString("rel", "self");
        writer.WriteAttributeString("type", "application/rss+xml");
        writer.WriteEndElement();

        foreach (FeedArticle article in articles)
        {
            writer.WriteStartElement("item");
            writer.WriteElementString("title", article.Title);
            writer.WriteElementString("link", article.CanonicalUri.AbsoluteUri);

            writer.WriteStartElement("guid");
            writer.WriteAttributeString("isPermaLink", "true");
            writer.WriteString(article.CanonicalUri.AbsoluteUri);
            writer.WriteEndElement();

            writer.WriteElementString("description", article.Summary);
            writer.WriteElementString(
                "pubDate",
                ToRfc822(article.Published));
            writer.WriteElementString(
                "dc",
                "creator",
                DublinCoreNamespace,
                article.Author);

            writer.WriteStartElement("media", "thumbnail", MediaNamespace);
            writer.WriteAttributeString(
                "url",
                new Uri(SiteRoot, "images/asibackbone-icon-50.png").AbsoluteUri);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static string ToRfc822(DateOnly date) =>
        new DateTimeOffset(
            date.Year,
            date.Month,
            date.Day,
            0,
            0,
            0,
            TimeSpan.Zero)
        .ToString("R", CultureInfo.InvariantCulture);

    private static IEnumerable<string> EnumerateMarkdownFiles(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }

        foreach (string childDirectory in Directory.EnumerateDirectories(directory))
        {
            if (string.Equals(
                    Path.GetFileName(childDirectory),
                    "_site",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string file in EnumerateMarkdownFiles(childDirectory))
            {
                yield return file;
            }
        }
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
            "Could not locate the repository root. Run the feed generator from within the Learning repository.");
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}

sealed record FrontMatter(IReadOnlyDictionary<string, string> Values);

sealed record FeedArticle(
    string Title,
    string Author,
    DateOnly Published,
    DateOnly? Updated,
    string Summary,
    Uri CanonicalUri);
