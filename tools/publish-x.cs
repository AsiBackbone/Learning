using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#pragma warning disable IDE0040 // File-based application helper types are intentionally file-local.
#pragma warning disable IDE1006 // Primary-constructor backing fields are compiler generated.
#pragma warning disable CA5350 // OAuth 1.0a requires HMAC-SHA1 for X user-context authentication.

return await XPublisherProgram.RunAsync(args);

static class XPublisherProgram
{
    private static readonly Uri SiteRoot = new("https://asibackbone.github.io/Learning/");

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
        {
            return await XPublisherSelfTest.RunAsync();
        }

        PublisherOptions? options;

        try
        {
            options = PublisherOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(PublisherOptions.Usage);
            return 2;
        }

        try
        {
            string repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
            var git = new GitRepository(repositoryRoot);
            git.ValidateRevision(options.Head);

            var store = new FilePublisherStateStore(options.StatePath);
            PublisherState? state = store.Load();

            if (state is null)
            {
                state = PublisherState.Create(options.Account, options.Head);
                store.Save(state);
                Console.WriteLine(
                    $"Initialized X publisher state at {options.Head}; existing publications will not be backfilled.");
                return 0;
            }

            if (!string.Equals(state.Account, options.Account, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Publisher state belongs to account '{state.Account}', not '{options.Account}'.");
            }

            git.ValidateAncestor(state.CursorSha, options.Head);
            HashSet<string> sitemapUrls = LoadSitemap(options.SitemapPath);
            var selector = new GitPublicationSelector(git, SiteRoot);
            IReadOnlyList<LearningPublication> candidates = selector.Select(
                state.CursorSha,
                options.Head,
                sitemapUrls,
                DateOnly.FromDateTime(DateTime.UtcNow));

            if (options.DryRun)
            {
                foreach (string line in XPublisher.RenderDryRun(candidates, state))
                {
                    Console.WriteLine(line);
                }

                return 0;
            }

            XCredentials credentials = XCredentials.FromEnvironment(options.AccountId);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var client = new XApiClient(httpClient, credentials);
            var publisher = new XPublisher(client, store, new JackdawPatioPostComposer());

            PublisherState completed = await publisher.PublishAsync(
                candidates,
                state,
                options.Head,
                CancellationToken.None);

            Console.WriteLine(
                $"X publication completed at {completed.CursorSha} with {completed.Receipts.Count} durable receipt(s).");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"X publication failed: {exception.Message}");
            return 1;
        }
    }

    private static HashSet<string> LoadSitemap(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The deployed sitemap was not found.", path);
        }

        XNamespace sitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XDocument document = XDocument.Load(path);

        return document
            .Descendants(sitemapNamespace + "loc")
            .Select(static element => element.Value.Trim())
            .Where(static value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot(string startingDirectory)
    {
        var directory = new DirectoryInfo(startingDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}

sealed record PublisherOptions(
    string Head,
    string SitemapPath,
    string StatePath,
    string Account,
    string AccountId,
    bool DryRun)
{
    public const string Usage =
        "Usage: dotnet run --file tools/publish-x.cs -- --head <sha> --sitemap <path> " +
        "--state <path> --account <name> [--account-id <id>] [--dry-run]";

    public static PublisherOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool dryRun = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];

            if (string.Equals(argument, "--dry-run", StringComparison.Ordinal))
            {
                dryRun = true;
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Invalid argument '{argument}'.");
            }

            values[argument] = args[++index];
        }

        string Required(string name) =>
            values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"Missing required option '{name}'.");

        string accountId = values.GetValueOrDefault("--account-id", string.Empty);

        if (!dryRun && string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Missing required live-publication option '--account-id'.");
        }

        return new PublisherOptions(
            Required("--head"),
            Path.GetFullPath(Required("--sitemap")),
            Path.GetFullPath(Required("--state")),
            Required("--account"),
            accountId,
            dryRun);
    }
}

sealed class GitPublicationSelector(GitRepository git, Uri siteRoot)
{
    public IReadOnlyList<LearningPublication> Select(
        string baseRevision,
        string headRevision,
        IReadOnlySet<string> sitemapUrls,
        DateOnly today)
    {
        IReadOnlyList<GitChange> changes = git.GetChanges(baseRevision, headRevision);
        return SelectChanges(
            changes,
            path => git.ReadFile(baseRevision, path),
            path => git.ReadFile(headRevision, path),
            path => git.GetBlobSha(headRevision, path),
            sitemapUrls,
            today,
            siteRoot);
    }

    internal static IReadOnlyList<LearningPublication> SelectChanges(
        IReadOnlyList<GitChange> changes,
        Func<string, string?> readBase,
        Func<string, string?> readHead,
        Func<string, string> getBlobSha,
        IReadOnlySet<string> sitemapUrls,
        DateOnly today,
        Uri siteRoot)
    {
        var selected = new List<LearningPublication>();

        foreach (GitChange change in changes)
        {
            if (change.Kind == GitChangeKind.Deleted ||
                !IsMarkdownUnderDocs(change.NewPath))
            {
                continue;
            }

            PublicationMetadata? previous = change.OldPath is null
                ? null
                : PublicationMetadataReader.Read(readBase(change.OldPath), change.OldPath, today);

            PublicationMetadata? current = PublicationMetadataReader.Read(
                readHead(change.NewPath),
                change.NewPath,
                today);

            bool newlyEligible = current is not null && previous is null;
            bool deferredCopyAdded = current?.XPost is not null && previous is { XPost: null };
            if (current is null || (!newlyEligible && !deferredCopyAdded))
            {
                continue;
            }

            string relativeSource = change.NewPath["docs/".Length..];
            string relativeHtml = Path.ChangeExtension(relativeSource, ".html").Replace('\\', '/');
            string canonicalUrl = new Uri(siteRoot, relativeHtml).AbsoluteUri;

            if (!sitemapUrls.Contains(canonicalUrl))
            {
                throw new InvalidDataException(
                    $"{change.NewPath}: canonical page '{canonicalUrl}' is absent from the deployed sitemap.");
            }

            selected.Add(new LearningPublication(
                current.Title,
                current.Author,
                current.Published,
                current.Summary,
                current.XHashtags,
                change.NewPath,
                getBlobSha(change.NewPath),
                canonicalUrl,
                current.XPost,
                current.XQuestion));
        }

        return selected
            .OrderBy(static publication => publication.Published)
            .ThenBy(static publication => publication.CanonicalUrl, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsMarkdownUnderDocs(string path) =>
        path.StartsWith("docs/", StringComparison.Ordinal) &&
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
}

static class PublicationMetadataReader
{
    private static readonly HashSet<string> PublicationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "author", "published", "summary", "feed", "x_post", "x_question"
    };

    private static readonly HashSet<string> AllowedXHashtags = new(StringComparer.Ordinal)
    {
        "DotNet",
        "AspNetCore",
        "SoftwareArchitecture",
        "CyberSecurity",
        "AISecurity",
        "AIGovernance",
        "DevSecOps"
    };

    public static PublicationMetadata? Read(string? content, string path, DateOnly today)
    {
        if (content is null)
        {
            return null;
        }

        string[] lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        if (lines.Length == 0 || !string.Equals(lines[0].TrimStart('\uFEFF'), "---", StringComparison.Ordinal))
        {
            return null;
        }

        int closingIndex = Array.FindIndex(lines, 1, static line => line.Trim() == "---");

        if (closingIndex < 0)
        {
            throw new InvalidDataException($"{path}: YAML frontmatter has no closing delimiter.");
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
                throw new InvalidDataException($"{path}: publication metadata '{key}' must be single-line.");
            }

            if (!values.TryAdd(key, ParseScalar(rawValue)))
            {
                throw new InvalidDataException($"{path}: frontmatter key '{key}' appears more than once.");
            }
        }

        if (!values.TryGetValue("feed", out string? feedText))
        {
            return null;
        }

        if (!bool.TryParse(feedText, out bool feedEnabled))
        {
            throw new InvalidDataException($"{path}: 'feed' must be either true or false.");
        }

        if (!feedEnabled)
        {
            return null;
        }

        IReadOnlyList<string> xHashtags = ReadXHashtags(lines, closingIndex, path);

        string Required(string key) =>
            values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : throw new InvalidDataException(
                    $"{path}: feed-enabled documents require non-empty '{key}' metadata.");

        string publishedText = Required("published");

        if (!DateOnly.TryParseExact(
            publishedText,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly published))
        {
            throw new InvalidDataException($"{path}: 'published' must use YYYY-MM-DD format.");
        }

        if (published > today)
        {
            throw new InvalidDataException($"{path}: future publication date '{published:yyyy-MM-dd}' is not allowed.");
        }

        string? xPost = values.TryGetValue("x_post", out string? postText)
            ? XPostRules.Validate(postText, path)
            : null;
        string? xQuestion = values.TryGetValue("x_question", out string? questionText)
            ? XQuestionRules.Validate(questionText, path)
            : null;

        return new PublicationMetadata(
            Required("title"),
            Required("author"),
            published,
            Required("summary"),
            xHashtags,
            xPost,
            xQuestion);
    }

    private static IReadOnlyList<string> ReadXHashtags(
        IReadOnlyList<string> lines,
        int closingIndex,
        string path)
    {
        var hashtags = new List<string>();
        bool found = false;

        for (int index = 1; index < closingIndex; index++)
        {
            string line = lines[index];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            int separator = line.IndexOf(':');
            if (separator <= 0 ||
                !string.Equals(line[..separator].Trim(), "x_hashtags", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (found)
            {
                throw new InvalidDataException($"{path}: frontmatter key 'x_hashtags' appears more than once.");
            }

            found = true;
            if (!string.IsNullOrWhiteSpace(line[(separator + 1)..]))
            {
                throw new InvalidDataException(
                    $"{path}: 'x_hashtags' must be a YAML block list with one hashtag per item.");
            }

            for (
                index++;
                index < closingIndex &&
                (lines[index].Length == 0 || char.IsWhiteSpace(lines[index][0]));
                index++)
            {
                string item = lines[index].Trim();
                if (item.Length == 0 || item.StartsWith('#'))
                {
                    continue;
                }

                if (!item.StartsWith("- ", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"{path}: 'x_hashtags' entries must use YAML '- Hashtag' list syntax.");
                }

                hashtags.Add(ParseScalar(item[2..].Trim()));
            }

            index--;
        }

        if (!found)
        {
            return ["DotNet"];
        }

        if (hashtags.Count is < 1 or > 2)
        {
            throw new InvalidDataException($"{path}: 'x_hashtags' must contain one or two entries.");
        }

        if (hashtags.Distinct(StringComparer.Ordinal).Count() != hashtags.Count)
        {
            throw new InvalidDataException($"{path}: 'x_hashtags' entries must be unique.");
        }

        if (!hashtags.Contains("DotNet", StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{path}: 'x_hashtags' must include 'DotNet'.");
        }

        string? unsupported = hashtags.FirstOrDefault(hashtag => !AllowedXHashtags.Contains(hashtag));
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"{path}: unsupported X hashtag '{unsupported}'. Allowed values: {string.Join(", ", AllowedXHashtags.Order())}.");
        }

        return hashtags;
    }

    private static string ParseScalar(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            using JsonDocument document = JsonDocument.Parse(value);
            return document.RootElement.GetString() ?? string.Empty;
        }

        return value;
    }
}

sealed class GitRepository(string repositoryRoot)
{
    public void ValidateRevision(string revision) =>
        Run("cat-file", "-e", $"{revision}^{{commit}}");

    public void ValidateAncestor(string ancestor, string descendant)
    {
        ProcessResult result = RunForResult("merge-base", "--is-ancestor", ancestor, descendant);

        if (result.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"Publisher cursor '{ancestor}' is not an ancestor of deployed revision '{descendant}'.");
        }
    }

    public IReadOnlyList<GitChange> GetChanges(string baseRevision, string headRevision)
    {
        string output = Run("diff", "--name-status", "-z", "-M", baseRevision, headRevision, "--", "docs");
        string[] fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var changes = new List<GitChange>();

        for (int index = 0; index < fields.Length;)
        {
            string status = fields[index++];

            if (status.StartsWith('R'))
            {
                changes.Add(new GitChange(GitChangeKind.Renamed, fields[index++], fields[index++]));
            }
            else
            {
                string path = fields[index++];
                changes.Add(status[0] switch
                {
                    'A' => new GitChange(GitChangeKind.Added, null, path),
                    'D' => new GitChange(GitChangeKind.Deleted, path, path),
                    _ => new GitChange(GitChangeKind.Modified, path, path)
                });
            }
        }

        return changes;
    }

    public string? ReadFile(string revision, string path)
    {
        ProcessResult result = RunForResult("show", $"{revision}:{path}");
        return result.ExitCode == 0 ? result.Output : null;
    }

    public string GetBlobSha(string revision, string path) =>
        Run("rev-parse", $"{revision}:{path}").Trim();

    private string Run(params string[] arguments)
    {
        ProcessResult result = RunForResult(arguments);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed: {result.Error.Trim()}");
        }

        return result.Output;
    }

    private ProcessResult RunForResult(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output, error);
    }
}

sealed class JackdawPatioPostComposer : IXPostComposer
{
    // tools/validate-doc-metadata.cs repeats this layout to reject x_post copy that
    // cannot fit before publication. Keep the two in sync.
    public string Compose(LearningPublication publication)
    {
        string xPost = publication.XPost ?? throw new InvalidDataException(
            $"{publication.SourcePath}: missing 'x_post'; add reviewed engineering-takeaway copy before publication.");
        string hashtags = string.Join(' ', publication.XHashtags.Select(static hashtag => $"#{hashtag}"));
        string question = publication.XQuestion is { } value ? $"\n\n{XAutoLinkGuard.Apply(value)}" : string.Empty;
        string text = $"{XAutoLinkGuard.Apply(xPost)}\n\n{XAutoLinkGuard.Apply(publication.Title)}{question}\n\n{hashtags}\n\n";

        if (XTextLength.Weighted(text) + XTextLength.UrlWeight > XTextLength.MaximumWeightedLength)
        {
            int over = XTextLength.Weighted(text) + XTextLength.UrlWeight - XTextLength.MaximumWeightedLength;
            throw new InvalidDataException(
                $"{publication.SourcePath}: the composed X post is {over} character(s) too long; shorten 'x_post' or 'x_question'.");
        }

        return text + publication.CanonicalUrl;
    }
}

static class XAutoLinkGuard
{
    // X turns any token shaped like a domain into a link: "ASP.NET" becomes asp.net and
    // "Task.Run" becomes task.run. The link then counts 23 characters, and X builds the
    // post's link card from it instead of from the canonical URL. X recognizes a domain
    // only with an ASCII full stop, so the dot in such a token is replaced with ONE DOT
    // LEADER (U+2024), which looks the same in common fonts. An invisible LEFT-TO-RIGHT
    // MARK before the dot was tried first and does not work: X's composer showed the
    // token unlinked, but the published post linked it again. A manual post using U+2024
    // stayed unlinked and showed the article's card (November 22, 2026).
    // Every letter-or-digit, dot, letter sequence is replaced, not only known top-level
    // domains, because X recognizes hundreds of them. The cost is one extra weighted
    // character per token and posts that X search will not match on the literal "ASP.NET".
    // tools/validate-doc-metadata.cs applies the same transformation. Keep the two in sync.
    public const char Dot = '\u2024';

    private static readonly Regex DomainDot = new(@"(?<=[\p{L}\p{N}])\.(?=\p{L})", RegexOptions.Compiled);

    public static string Apply(string text) => DomainDot.Replace(text, Dot.ToString());
}

static class XPostRules
{
    // tools/validate-doc-metadata.cs applies the same rules. Keep the two in sync.
    public static string Validate(string value, string path)
    {
        string post = value.Trim();
        string? problem = Problem(post);
        return problem is null
            ? post
            : throw new InvalidDataException($"{path}: 'x_post' {problem}");
    }

    public static string? Problem(string post) =>
        post.Length == 0 ? "must not be empty." :
        post.Any(char.IsControl) ? "must be a single line without control characters." :
        post.Contains('#') || post.Contains('@')
            ? "must not contain hashtags or mentions; use 'x_hashtags' for hashtags." :
        post.Contains("://", StringComparison.Ordinal) || post.Contains("www.", StringComparison.OrdinalIgnoreCase)
            ? "must not contain links; the publisher adds the canonical URL." :
        null;
}

static class XQuestionRules
{
    // tools/validate-doc-metadata.cs applies the same rules. Keep the two in sync.
    public static string Validate(string value, string path)
    {
        string question = value.Trim();
        string? problem = Problem(question);
        return problem is null
            ? question
            : throw new InvalidDataException($"{path}: 'x_question' {problem}");
    }

    public static string? Problem(string question) =>
        question.Length == 0 ? "must not be empty." :
        question.Any(char.IsControl) ? "must be a single line without control characters." :
        !question.EndsWith('?') ? "must end with a question mark." :
        question.Contains('#') || question.Contains('@')
            ? "must not contain hashtags or mentions; use 'x_hashtags' for hashtags." :
        question.Contains("://", StringComparison.Ordinal) || question.Contains("www.", StringComparison.OrdinalIgnoreCase)
            ? "must not contain links; the publisher adds the canonical URL." :
        null;
}

static class XTextLength
{
    // tools/validate-doc-metadata.cs repeats this counting. Keep the two in sync.
    public const int MaximumWeightedLength = 280;

    // X shortens every link with t.co and counts it as 23 characters, whatever its length.
    public const int UrlWeight = 23;

    // Approximates X's weighted count: a code point in X's light ranges, which cover
    // Latin, Greek, Cyrillic, and common punctuation, counts 1; any other code point
    // counts 2; an emoji sequence counts 2 in total. Text is NFC-normalized first, as X does.
    public static int Weighted(string text)
    {
        int total = 0;
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text.Normalize(NormalizationForm.FormC));

        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (IsEmoji(element))
            {
                total += 2;
                continue;
            }

            foreach (Rune rune in element.EnumerateRunes())
            {
                total += IsLight(rune.Value) ? 1 : 2;
            }
        }

        return total;
    }

    private static bool IsLight(int value) =>
        value <= 0x10FF ||
        value is >= 0x2000 and <= 0x200D ||
        value is >= 0x2010 and <= 0x201F ||
        value is >= 0x2032 and <= 0x2037;

    private static bool IsEmoji(string element)
    {
        foreach (Rune rune in element.EnumerateRunes())
        {
            if (rune.Value is 0x200D or 0xFE0F or (>= 0x1F000 and <= 0x1FAFF) or (>= 0x2600 and <= 0x27BF))
            {
                return true;
            }
        }

        return false;
    }
}

sealed class XPublisher(IXClient client, IPublisherStateStore store, IXPostComposer composer)
{
    public static IReadOnlyList<string> RenderDryRun(
        IReadOnlyList<LearningPublication> candidates,
        PublisherState state)
    {
        LearningPublication[] pending = candidates
            .Where(candidate => !state.Receipts.ContainsKey(candidate.CanonicalUrl))
            .OrderBy(static candidate => candidate.Published)
            .ThenBy(static candidate => candidate.CanonicalUrl, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> candidateUrls = pending
            .Select(static candidate => candidate.CanonicalUrl)
            .ToHashSet(StringComparer.Ordinal);
        KeyValuePair<string, XPublicationDeferral>[] recordedDeferrals = state.DeferredPublications
            .Where(pair => !candidateUrls.Contains(pair.Key))
            .OrderBy(static pair => pair.Value.SourcePath, StringComparer.Ordinal)
            .ToArray();
        int publishableCount = pending.Count(static candidate => candidate.XPost is not null);
        int deferredCount = pending.Length - publishableCount + recordedDeferrals.Length;

        var lines = new List<string>
        {
            $"Dry run: {publishableCount} publication(s) pending; {deferredCount} missing 'x_post'."
        };
        var composer = new JackdawPatioPostComposer();

        foreach (LearningPublication publication in pending)
        {
            lines.Add($"--- {publication.SourcePath}");
            lines.Add(publication.XPost is null
                ? "DEFERRED: missing 'x_post'; add reviewed engineering-takeaway copy to publish this article later."
                : composer.Compose(publication));
        }

        foreach (KeyValuePair<string, XPublicationDeferral> pair in recordedDeferrals)
        {
            XPublicationDeferral deferral = pair.Value;
            lines.Add($"--- {deferral.SourcePath}");
            lines.Add(
                $"DEFERRED: missing 'x_post' since {deferral.DeferredAtUtc:u}; add reviewed engineering-takeaway copy to publish this article later.");
        }

        return lines;
    }

    public async Task<PublisherState> PublishAsync(
        IReadOnlyList<LearningPublication> candidates,
        PublisherState initialState,
        string headRevision,
        CancellationToken cancellationToken)
    {
        PublisherState state = initialState;

        foreach (LearningPublication publication in candidates)
        {
            if (state.Receipts.ContainsKey(publication.CanonicalUrl))
            {
                continue;
            }

            if (publication.XPost is null)
            {
                XPublicationDeferral deferral = state.DeferredPublications.GetValueOrDefault(publication.CanonicalUrl) is { } existingDeferral
                    ? existingDeferral with
                    {
                        SourcePath = publication.SourcePath,
                        SourceBlobSha = publication.SourceBlobSha
                    }
                    : new XPublicationDeferral(publication.SourcePath, publication.SourceBlobSha, DateTimeOffset.UtcNow);
                PublisherState withDeferral = state.WithDeferral(publication.CanonicalUrl, deferral);
                store.Save(withDeferral);
                state = withDeferral;
                Console.WriteLine(
                    $"Deferred '{publication.SourcePath}': missing 'x_post'; later publications may continue.");
                continue;
            }

            IReadOnlyList<XPost> recent = await client.GetRecentPostsAsync(cancellationToken);
            XPost? existing = recent.FirstOrDefault(
                post => post.Text.Contains(publication.CanonicalUrl, StringComparison.Ordinal));
            string postId;

            if (existing is not null)
            {
                postId = existing.Id;
            }
            else
            {
                try
                {
                    postId = await client.CreatePostAsync(composer.Compose(publication), cancellationToken);
                }
                catch (AmbiguousDeliveryException)
                {
                    recent = await client.GetRecentPostsAsync(cancellationToken);
                    existing = recent.FirstOrDefault(
                        post => post.Text.Contains(publication.CanonicalUrl, StringComparison.Ordinal));

                    if (existing is null)
                    {
                        throw;
                    }

                    postId = existing.Id;
                }
            }

            PublisherState withReceipt = state.WithReceipt(
                publication.CanonicalUrl,
                new XPublicationReceipt(
                    publication.SourceBlobSha,
                    postId,
                    DateTimeOffset.UtcNow));
            store.Save(withReceipt);
            state = withReceipt;
        }

        PublisherState completed = state.WithCursor(headRevision);
        store.Save(completed);
        return completed;
    }
}

sealed class XApiClient(HttpClient httpClient, XCredentials credentials) : IXClient
{
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);

    private static readonly Uri ApiRoot = new("https://api.x.com/2/");

    public async Task<IReadOnlyList<XPost>> GetRecentPostsAsync(CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["max_results"] = "100",
            ["tweet.fields"] = "created_at"
        };
        Uri uri = BuildUri(new Uri(ApiRoot, $"users/{credentials.AccountId}/tweets"), query);
        using HttpResponseMessage response = await SendWithRetryAsync(
            HttpMethod.Get,
            uri,
            query,
            content: null,
            cancellationToken);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (!document.RootElement.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(static item => new XPost(
                item.GetProperty("id").GetString() ?? string.Empty,
                item.GetProperty("text").GetString() ?? string.Empty))
            .ToArray();
    }

    public async Task<string> CreatePostAsync(string text, CancellationToken cancellationToken)
    {
        Uri uri = new(ApiRoot, "tweets");
        string json = JsonSerializer.Serialize(
            new CreatePostRequest(text),
            XPublisherJsonContext.Default.CreatePostRequest);
        using HttpResponseMessage response = await SendWithRetryAsync(
            HttpMethod.Post,
            uri,
            new Dictionary<string, string>(),
            json,
            cancellationToken);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("data").GetProperty("id").GetString() ??
            throw new InvalidDataException("X create-post response did not contain a post ID.");
    }

    internal static XResponseDisposition Classify(HttpStatusCode statusCode)
    {
        int numericStatus = (int)statusCode;

        if (statusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            return XResponseDisposition.Success;
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return XResponseDisposition.AuthenticationFailure;
        }

        return statusCode == HttpStatusCode.TooManyRequests || numericStatus >= 500
            ? XResponseDisposition.Retryable
            : XResponseDisposition.PermanentFailure;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpMethod method,
        Uri uri,
        IReadOnlyDictionary<string, string> query,
        string? content,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "OAuth",
                OAuth1Signer.CreateAuthorizationParameter(method, uri, query, credentials));

            if (content is not null)
            {
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;

            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException exception) when (
                !cancellationToken.IsCancellationRequested && method == HttpMethod.Post)
            {
                throw new AmbiguousDeliveryException("X request timed out after it may have been sent.", exception);
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested && attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                continue;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("X read request exhausted its timeout retries.", exception);
            }
            catch (HttpRequestException exception) when (method == HttpMethod.Post)
            {
                throw new AmbiguousDeliveryException(
                    "X request failed after it may have been sent.",
                    exception);
            }
            catch (HttpRequestException) when (attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                continue;
            }

            XResponseDisposition disposition = Classify(response.StatusCode);

            if (disposition == XResponseDisposition.Success)
            {
                return response;
            }

            _ = await response.Content.ReadAsStringAsync(cancellationToken);

            if (method == HttpMethod.Post && (int)response.StatusCode >= 500)
            {
                int ambiguousStatusCode = (int)response.StatusCode;
                response.Dispose();
                throw new AmbiguousDeliveryException(
                    $"X returned HTTP {ambiguousStatusCode} after the request may have been accepted.");
            }

            if (disposition == XResponseDisposition.Retryable && attempt < maximumAttempts)
            {
                TimeSpan delay = GetRetryDelay(response, attempt);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            int statusCode = (int)response.StatusCode;
            response.Dispose();
            string category = disposition == XResponseDisposition.AuthenticationFailure
                ? "authentication or application-permission"
                : disposition == XResponseDisposition.Retryable
                    ? "retry limit"
                    : "request";
            throw new InvalidOperationException(
                $"X {category} failure (HTTP {statusCode}). Response body was intentionally not logged.");
        }

        throw new InvalidOperationException("X request exhausted its retry limit.");
    }

    internal static TimeSpan GetRetryDelay(
        HttpResponseMessage response,
        int attempt,
        DateTimeOffset? currentTime = null)
    {
        RetryConditionHeaderValue? retryCondition = response.Headers.RetryAfter;
        TimeSpan? retryAfter = retryCondition?.Delta;

        if (retryAfter is null && retryCondition?.Date is DateTimeOffset retryDate)
        {
            retryAfter = retryDate - (currentTime ?? DateTimeOffset.UtcNow);
        }

        if (retryAfter is null)
        {
            return TimeSpan.FromSeconds(attempt);
        }

        return retryAfter.Value <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(attempt)
            : retryAfter.Value >= MaximumRetryDelay
                ? MaximumRetryDelay
                : retryAfter.Value;
    }

    private static Uri BuildUri(Uri uri, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", query.Select(
                pair => $"{OAuth1Signer.Encode(pair.Key)}={OAuth1Signer.Encode(pair.Value)}"))
        };
        return builder.Uri;
    }
}

static class OAuth1Signer
{
    public static string CreateAuthorizationParameter(
        HttpMethod method,
        Uri uri,
        IReadOnlyDictionary<string, string> query,
        XCredentials credentials,
        DateTimeOffset? currentTime = null,
        string? nonce = null)
    {
        var oauth = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = credentials.ApiKey,
            ["oauth_nonce"] = nonce ??
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = (currentTime ?? DateTimeOffset.UtcNow)
                .ToUnixTimeSeconds()
                .ToString(CultureInfo.InvariantCulture),
            ["oauth_token"] = credentials.AccessToken,
            ["oauth_version"] = "1.0"
        };

        IEnumerable<KeyValuePair<string, string>> signatureParameters = oauth.Concat(query)
            .OrderBy(static pair => Encode(pair.Key), StringComparer.Ordinal)
            .ThenBy(static pair => Encode(pair.Value), StringComparer.Ordinal);
        string normalized = string.Join(
            "&",
            signatureParameters.Select(pair => $"{Encode(pair.Key)}={Encode(pair.Value)}"));
        string baseUri = uri.GetLeftPart(UriPartial.Path);
        string signatureBase = $"{method.Method.ToUpperInvariant()}&{Encode(baseUri)}&{Encode(normalized)}";
        string signingKey = $"{Encode(credentials.ApiKeySecret)}&{Encode(credentials.AccessTokenSecret)}";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase)));
        oauth["oauth_signature"] = signature;

        return string.Join(
            ", ",
            oauth.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{Encode(pair.Key)}=\"{Encode(pair.Value)}\""));
    }

    public static string Encode(string value) =>
        Uri.EscapeDataString(value)
            .Replace("%7E", "~", StringComparison.Ordinal);
}

sealed class FilePublisherStateStore(string path) : IPublisherStateStore
{
    public PublisherState? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        PublisherState state = JsonSerializer.Deserialize<PublisherState>(
            File.ReadAllText(path),
            XPublisherJsonContext.Default.PublisherState) ??
            throw new InvalidDataException("Publisher state is empty.");

        if (state.SchemaVersion is not (1 or 2) ||
            string.IsNullOrWhiteSpace(state.Account) ||
            string.IsNullOrWhiteSpace(state.CursorSha) ||
            state.Receipts is null ||
            (state.SchemaVersion == 2 && state.DeferredPublications is null))
        {
            throw new InvalidDataException("Publisher state has an unsupported or incomplete schema.");
        }

        if (state.SchemaVersion == 1)
        {
            state = new PublisherState
            {
                Account = state.Account,
                CursorSha = state.CursorSha,
                Receipts = new Dictionary<string, XPublicationReceipt>(state.Receipts, StringComparer.Ordinal),
                DeferredPublications = state.DeferredPublications is null
                    ? new Dictionary<string, XPublicationDeferral>(StringComparer.Ordinal)
                    : new Dictionary<string, XPublicationDeferral>(state.DeferredPublications, StringComparer.Ordinal)
            };
        }

        return state;
    }

    public void Save(PublisherState state)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(state, XPublisherJsonContext.Default.PublisherState) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

sealed class PublisherState
{
    public int SchemaVersion { get; init; } = 2;
    public required string Account { get; init; }
    public required string CursorSha { get; init; }
    public Dictionary<string, XPublicationReceipt> Receipts { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, XPublicationDeferral> DeferredPublications { get; init; } = new(StringComparer.Ordinal);

    public static PublisherState Create(string account, string cursorSha) =>
        new() { Account = account, CursorSha = cursorSha };

    public PublisherState WithReceipt(string canonicalUrl, XPublicationReceipt receipt)
    {
        var receipts = new Dictionary<string, XPublicationReceipt>(Receipts, StringComparer.Ordinal)
        {
            [canonicalUrl] = receipt
        };
        var deferred = new Dictionary<string, XPublicationDeferral>(DeferredPublications, StringComparer.Ordinal);
        deferred.Remove(canonicalUrl);
        return new PublisherState
        {
            Account = Account,
            CursorSha = CursorSha,
            Receipts = receipts,
            DeferredPublications = deferred
        };
    }

    public PublisherState WithDeferral(string canonicalUrl, XPublicationDeferral deferral)
    {
        var deferred = new Dictionary<string, XPublicationDeferral>(DeferredPublications, StringComparer.Ordinal)
        {
            [canonicalUrl] = deferral
        };
        return new PublisherState
        {
            Account = Account,
            CursorSha = CursorSha,
            Receipts = new Dictionary<string, XPublicationReceipt>(Receipts, StringComparer.Ordinal),
            DeferredPublications = deferred
        };
    }

    public PublisherState WithCursor(string cursorSha) =>
        new()
        {
            Account = Account,
            CursorSha = cursorSha,
            Receipts = new Dictionary<string, XPublicationReceipt>(Receipts, StringComparer.Ordinal),
            DeferredPublications = new Dictionary<string, XPublicationDeferral>(DeferredPublications, StringComparer.Ordinal)
        };
}

sealed record XCredentials(
    string ApiKey,
    string ApiKeySecret,
    string AccessToken,
    string AccessTokenSecret,
    string AccountId)
{
    public static XCredentials FromEnvironment(string accountId) =>
        new(
            Required("X_API_KEY"),
            Required("X_API_KEY_SECRET"),
            Required("X_ACCESS_TOKEN"),
            Required("X_ACCESS_TOKEN_SECRET"),
            accountId);

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Required secret '{name}' is not configured.");
}

static class XPublisherSelfTest
{
    private static readonly Uri SiteRoot = new("https://asibackbone.github.io/Learning/");
    private static readonly DateOnly Today = new(2026, 9, 13);

    public static async Task<int> RunAsync()
    {
        try
        {
            TestSelection();
            TestInvalidMetadata();
            TestXMetadata();
            TestComposition();
            TestResponseClassification();
            TestRetryDelays();
            TestOAuthSignature();
            await TestApiResponsesAsync();
            await TestReceiptsAndReconciliationAsync();
            await TestDeferralAsync();
            await TestAmbiguousDeliveryAsync();
            await TestReceiptFailureAsync();
            TestStateMigration();
            TestDryRun();
            Console.WriteLine("X publisher self-test passed (selection, metadata, composition, delivery, and state contracts).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"X publisher self-test failed: {exception.Message}");
            return 1;
        }
    }

    private static void TestSelection()
    {
        const string eligible = "---\ntitle: New article\nauthor: Test Author\npublished: 2026-09-12\nsummary: Summary.\nfeed: true\nx_post: A reviewed engineering takeaway.\n---\n";
        const string eligibleWithHashtags = "---\ntitle: New article\nauthor: Test Author\npublished: 2026-09-12\nsummary: Summary.\nfeed: true\nx_post: A reviewed engineering takeaway.\nx_hashtags:\n  - DotNet\n  - AISecurity\n---\n";
        const string eligibleWithoutCopy = "---\ntitle: Deferred article\nauthor: Test Author\npublished: 2026-09-12\nsummary: Summary.\nfeed: true\n---\n";
        const string ineligible = "---\ntitle: Draft\nfeed: false\n---\n";
        var baseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["docs/articles/enabled.md"] = ineligible,
            ["docs/articles/updated.md"] = eligible,
            ["docs/articles/old-name.md"] = eligible,
            ["docs/articles/deleted.md"] = eligible,
            ["docs/articles/deferred.md"] = eligibleWithoutCopy
        };
        var headFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["docs/articles/new.md"] = eligibleWithHashtags,
            ["docs/articles/enabled.md"] = eligible,
            ["docs/articles/updated.md"] = eligible.Replace("Summary.", "Updated summary.", StringComparison.Ordinal),
            ["docs/articles/new-name.md"] = eligible,
            ["docs/articles/navigation.md"] = "# Navigation",
            ["docs/articles/deferred.md"] = eligible,
            ["docs/articles/missing-copy.md"] = eligibleWithoutCopy
        };
        GitChange[] changes =
        [
            new(GitChangeKind.Added, null, "docs/articles/new.md"),
            new(GitChangeKind.Modified, "docs/articles/enabled.md", "docs/articles/enabled.md"),
            new(GitChangeKind.Modified, "docs/articles/updated.md", "docs/articles/updated.md"),
            new(GitChangeKind.Renamed, "docs/articles/old-name.md", "docs/articles/new-name.md"),
            new(GitChangeKind.Deleted, "docs/articles/deleted.md", "docs/articles/deleted.md"),
            new(GitChangeKind.Added, null, "docs/articles/navigation.md"),
            new(GitChangeKind.Modified, "docs/articles/deferred.md", "docs/articles/deferred.md"),
            new(GitChangeKind.Added, null, "docs/articles/missing-copy.md")
        ];
        var sitemap = new HashSet<string>(StringComparer.Ordinal)
        {
            new Uri(SiteRoot, "articles/new.html").AbsoluteUri,
            new Uri(SiteRoot, "articles/enabled.html").AbsoluteUri,
            new Uri(SiteRoot, "articles/deferred.html").AbsoluteUri,
            new Uri(SiteRoot, "articles/missing-copy.html").AbsoluteUri
        };
        IReadOnlyList<LearningPublication> selected = GitPublicationSelector.SelectChanges(
            changes,
            path => baseFiles.GetValueOrDefault(path),
            path => headFiles.GetValueOrDefault(path),
            _ => "blob-sha",
            sitemap,
            Today,
            SiteRoot);

        Assert(selected.Count == 4, "new, newly enabled, deferred, and copy-ready documents should be selected exactly once.");
        Assert(selected.Any(item => item.SourcePath.EndsWith("new.md", StringComparison.Ordinal)), "new document missing.");
        Assert(selected.Any(item => item.SourcePath.EndsWith("enabled.md", StringComparison.Ordinal)), "enabled transition missing.");
        Assert(
            selected.Single(item => item.SourcePath.EndsWith("new.md", StringComparison.Ordinal))
                .XHashtags.SequenceEqual(["DotNet", "AISecurity"]),
            "explicit X hashtags were not preserved.");
        Assert(
            selected.Single(item => item.SourcePath.EndsWith("enabled.md", StringComparison.Ordinal))
                .XHashtags.SequenceEqual(["DotNet"]),
            "default X hashtag was not applied.");
        Assert(
            selected.Single(item => item.SourcePath.EndsWith("missing-copy.md", StringComparison.Ordinal)).XPost is null,
            "an eligible document without x_post should remain selectable for deferral.");
        Assert(
            selected.Single(item => item.SourcePath.EndsWith("deferred.md", StringComparison.Ordinal)).XPost is not null,
            "adding x_post to a deferred document should make it selectable again.");
    }

    private static void TestXMetadata()
    {
        const string header = "---\ntitle: Q\nauthor: Test\npublished: 2026-09-12\nsummary: S.\nfeed: true\n";

        Assert(
            PublicationMetadataReader.Read(header + "---", "docs/none.md", Today) is { XPost: null, XQuestion: null },
            "missing X copy should remain available for deferral.");
        Assert(
            PublicationMetadataReader.Read(header + "x_post: Keep unknown payment outcomes pending until reconciliation.\n---", "docs/post.md", Today)!
                .XPost == "Keep unknown payment outcomes pending until reconciliation.",
            "a plain x_post was not read.");
        Assert(
            PublicationMetadataReader.Read(header + "x_question: Ever been fooled by a log search?\n---", "docs/plain.md", Today)!
                .XQuestion == "Ever been fooled by a log search?",
            "a plain x_question was not read.");
        Assert(
            PublicationMetadataReader.Read(header + "x_question: \"Where does your check live: API or worker?\"\n---", "docs/quoted.md", Today)!
                .XQuestion == "Where does your check live: API or worker?",
            "a quoted x_question was not read.");

        IReadOnlyList<LearningPublication> selected = GitPublicationSelector.SelectChanges(
            [new GitChange(GitChangeKind.Added, null, "docs/q.md")],
            _ => null,
            _ => header + "x_post: Treat a timeout as unknown until evidence resolves it.\nx_question: Ever been fooled?\n---",
            _ => "blob",
            new HashSet<string> { "https://asibackbone.github.io/Learning/q.html" },
            Today,
            SiteRoot);
        Assert(selected.Single().XQuestion == "Ever been fooled?", "x_question was not carried into the publication.");
        Assert(selected.Single().XPost == "Treat a timeout as unknown until evidence resolves it.", "x_post was not carried into the publication.");

        foreach (string line in new[]
                 {
                     "x_post: \"\"",
                     "x_post: Use #DotNet.",
                     "x_post: Ask @someone.",
                     "x_post: Read https://example.com.",
                     "x_post: |\n  Multi-line copy.",
                     "x_post: One.\nx_post: Two."
                 })
        {
            AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(header + line + "\n---", "docs/invalid-post.md", Today));
        }

        string[] invalid =
        [
            "x_question: Not a question",
            "x_question: \"\"",
            "x_question: Is this #DotNet?",
            "x_question: Have you asked @someone?",
            "x_question: Seen https://example.com?",
            "x_question: |\n  Multi-line?",
            "x_question: One?\nx_question: Two?",
        ];

        foreach (string line in invalid)
        {
            AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(header + line + "\n---", "docs/invalid.md", Today));
        }
    }

    private static void TestInvalidMetadata()
    {
        AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(
            "---\ntitle: Future\nauthor: Test\npublished: 2026-09-14\nsummary: Future.\nfeed: true\n---",
            "docs/future.md",
            Today));
        AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(
            "---\ntitle: Bad\nauthor: Test\npublished: not-a-date\nsummary: Bad.\nfeed: true\n---",
            "docs/bad.md",
            Today));
        AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(
            "---\ntitle: Bad tags\nauthor: Test\npublished: 2026-09-12\nsummary: Bad.\nfeed: true\nx_hashtags:\n  - AISecurity\n---",
            "docs/missing-dotnet-tag.md",
            Today));
        AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(
            "---\ntitle: Bad tags\nauthor: Test\npublished: 2026-09-12\nsummary: Bad.\nfeed: true\nx_hashtags:\n  - DotNet\n  - MadeUpTopic\n---",
            "docs/unsupported-tag.md",
            Today));
        AssertThrows<InvalidDataException>(() => PublicationMetadataReader.Read(
            "---\ntitle: Too many tags\nauthor: Test\npublished: 2026-09-12\nsummary: Bad.\nfeed: true\nx_hashtags:\n  - DotNet\n  - AISecurity\n  - DevSecOps\n---",
            "docs/too-many-tags.md",
            Today));

        const string eligible = "---\ntitle: Missing page\nauthor: Test\npublished: 2026-09-12\nsummary: Missing.\nfeed: true\n---";
        AssertThrows<InvalidDataException>(() => GitPublicationSelector.SelectChanges(
            [new GitChange(GitChangeKind.Added, null, "docs/missing.md")],
            _ => null,
            _ => eligible,
            _ => "blob",
            new HashSet<string>(),
            Today,
            SiteRoot));
    }

    private static void TestComposition()
    {
        var composer = new JackdawPatioPostComposer();

        const string takeaway = "A timeout ends the caller's observation, not the provider's work.";
        LearningPublication publication = Publication("Article title", hashtags: ["DotNet", "AISecurity"], post: takeaway);
        string post = composer.Compose(publication);
        Assert(WeightedPostLength(post, publication.CanonicalUrl) <= 280, "post exceeded the weighted limit.");
        Assert(post.Contains("\n\n#DotNet #AISecurity\n\n", StringComparison.Ordinal), "hashtags were not composed.");
        Assert(post.EndsWith(publication.CanonicalUrl, StringComparison.Ordinal), "canonical URL was not preserved.");

        Assert(
            composer.Compose(Publication("Short title", post: takeaway)) ==
                $"{takeaway}\n\nShort title\n\n#DotNet\n\nhttps://asibackbone.github.io/Learning/article.html",
            "x_post was not placed at the beginning of the post.");

        string question = "Has a correlation ID search ever told you the wrong story?";
        Assert(
            composer.Compose(Publication("Short title", post: takeaway, question: question)) ==
                $"{takeaway}\n\nShort title\n\n{question}\n\n#DotNet\n\nhttps://asibackbone.github.io/Learning/article.html",
            "the title and question were not placed after x_post.");

        string longUrl = "https://asibackbone.github.io/Learning/articles/2026/" + new string('a', 250) + ".html";
        string fullPost = new('t', 200);
        Assert(
            composer.Compose(Publication("Title", canonicalUrl: longUrl, post: fullPost)).Contains(fullPost, StringComparison.Ordinal),
            "a long URL should count as 23 characters, not shorten x_post.");

        AssertThrows<InvalidDataException>(() => composer.Compose(Publication("Missing", post: null)));
        AssertThrows<InvalidDataException>(
            () => composer.Compose(Publication("Too long", post: new string('p', 240), question: new string('q', 30) + "?")));

        Assert(XTextLength.Weighted("abc") == 3, "Latin text should count 1 per character.");
        Assert(XTextLength.Weighted("日本") == 4, "CJK text should count 2 per character.");
        Assert(XTextLength.Weighted("👩🏽‍💻") == 2, "an emoji sequence should count 2.");
        Assert(XTextLength.Weighted("…") == 2, "the ellipsis is outside X's light ranges.");

        TestAutoLinkGuard(composer);
    }

    private static void TestAutoLinkGuard(JackdawPatioPostComposer composer)
    {
        const char dot = XAutoLinkGuard.Dot;
        Assert(XAutoLinkGuard.Apply("Why Moving ASP.NET Core Middleware Breaks Your Endpoint") ==
                $"Why Moving ASP{dot}NET Core Middleware Breaks Your Endpoint",
            "ASP.NET should use the lookalike dot so X does not link it.");
        Assert(XAutoLinkGuard.Apply("Task.Run Does Not Make Work Durable") == $"Task{dot}Run Does Not Make Work Durable",
            "Task.Run should use the lookalike dot so X does not link it.");
        Assert(XAutoLinkGuard.Apply("Version 1.5 shipped. Then it failed.") == "Version 1.5 shipped. Then it failed.",
            "numbers and sentence punctuation should be unchanged.");
        Assert(XAutoLinkGuard.Apply("Plain words only") == "Plain words only", "text without dots should be unchanged.");
        Assert(!XAutoLinkGuard.Apply("ASP.NET").Contains('\u200E'), "the ineffective LEFT-TO-RIGHT MARK must not be used.");
        Assert(XTextLength.Weighted(dot.ToString()) == 2, "U+2024 is outside X's light ranges and should count 2.");

        const string title = "Why Moving ASP.NET Core Middleware Breaks Your Endpoint";
        const string takeaway = "Middleware order is behavior; HttpContext.User comes from authentication.";
        LearningPublication publication = Publication(title, post: takeaway, question: "Have you used ASP.NET Core?");
        string post = composer.Compose(publication);
        Assert(post.Contains($"ASP{dot}NET Core Middleware", StringComparison.Ordinal), "the title was not guarded.");
        Assert(post.Contains($"HttpContext{dot}User", StringComparison.Ordinal), "x_post was not guarded.");
        Assert(post.Contains($"used ASP{dot}NET Core?", StringComparison.Ordinal), "x_question was not guarded.");
        Assert(post.EndsWith("\n\n" + publication.CanonicalUrl, StringComparison.Ordinal) &&
                publication.CanonicalUrl.Contains('.') && !publication.CanonicalUrl.Contains(dot),
            "the canonical URL must be appended unchanged.");
        Assert(post.Count(static c => c == dot) == 3, "each guarded token should receive exactly one lookalike dot.");

        // The weighted check includes the replacement: each lookalike dot counts 2 instead of 1.
        string unguardedText = post[..^publication.CanonicalUrl.Length].Replace(dot, '.');
        Assert(WeightedPostLength(post, publication.CanonicalUrl) ==
                XTextLength.Weighted(unguardedText) + XTextLength.UrlWeight + 3,
            "the weighted length should count one extra character per replaced dot.");
        int room = 280 - XTextLength.UrlWeight - XTextLength.Weighted($"\n\n{title}\n\n#DotNet\n\n");
        string fitsOnlyWithPlainDot = new('p', room);
        AssertThrows<InvalidDataException>(() => composer.Compose(Publication(title, post: fitsOnlyWithPlainDot)));
        Assert(composer.Compose(Publication(title, post: new string('p', room - 1))).EndsWith(publication.CanonicalUrl, StringComparison.Ordinal),
            "a post that fits including the lookalike dot should compose.");
    }

    private static int WeightedPostLength(string post, string canonicalUrl) =>
        XTextLength.Weighted(post[..^canonicalUrl.Length]) + XTextLength.UrlWeight;

    private static void TestResponseClassification()
    {
        Assert(XApiClient.Classify(HttpStatusCode.Created) == XResponseDisposition.Success, "201 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.BadRequest) == XResponseDisposition.PermanentFailure, "400 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.Unauthorized) == XResponseDisposition.AuthenticationFailure, "401 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.Forbidden) == XResponseDisposition.AuthenticationFailure, "403 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.TooManyRequests) == XResponseDisposition.Retryable, "429 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.BadGateway) == XResponseDisposition.Retryable, "5xx classification failed.");
    }

    private static void TestRetryDelays()
    {
        DateTimeOffset currentTime = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

        using var deltaResponse = Response(HttpStatusCode.TooManyRequests, "{}");
        deltaResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
        Assert(
            XApiClient.GetRetryDelay(deltaResponse, attempt: 1, currentTime) == TimeSpan.FromSeconds(30),
            "long Retry-After deltas should be clamped to the maximum delay.");

        using var dateResponse = Response(HttpStatusCode.TooManyRequests, "{}");
        dateResponse.Headers.RetryAfter = new RetryConditionHeaderValue(currentTime.AddSeconds(20));
        Assert(
            XApiClient.GetRetryDelay(dateResponse, attempt: 1, currentTime) == TimeSpan.FromSeconds(20),
            "Retry-After dates should delay until the requested time.");

        using var expiredDateResponse = Response(HttpStatusCode.TooManyRequests, "{}");
        expiredDateResponse.Headers.RetryAfter = new RetryConditionHeaderValue(currentTime.AddSeconds(-1));
        Assert(
            XApiClient.GetRetryDelay(expiredDateResponse, attempt: 1, currentTime) == TimeSpan.FromSeconds(1),
            "expired Retry-After dates should use the attempt-based fallback.");

        using var zeroDeltaResponse = Response(HttpStatusCode.TooManyRequests, "{}");
        zeroDeltaResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        Assert(
            XApiClient.GetRetryDelay(zeroDeltaResponse, attempt: 2, currentTime) == TimeSpan.FromSeconds(2),
            "non-positive Retry-After deltas should use the attempt-based fallback.");

        using var fallbackResponse = Response(HttpStatusCode.BadGateway, "{}");
        Assert(
            XApiClient.GetRetryDelay(fallbackResponse, attempt: 2, currentTime) == TimeSpan.FromSeconds(2),
            "responses without Retry-After should use the attempt-based fallback.");
    }

    private static void TestOAuthSignature()
    {
        DateTimeOffset currentTime = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        XCredentials credentials = new("key", "sécret", "token", "töken-secret", "1234");
        string authorization = OAuth1Signer.CreateAuthorizationParameter(
            HttpMethod.Post,
            new Uri("https://api.x.com/2/tweets"),
            new Dictionary<string, string>(),
            credentials,
            currentTime,
            nonce: "fixed-nonce");

        const string expectedAuthorization =
            "oauth_consumer_key=\"key\", " +
            "oauth_nonce=\"fixed-nonce\", " +
            "oauth_signature=\"FcjdoFnRX21Z523PnUwLIXp%2Bvbo%3D\", " +
            "oauth_signature_method=\"HMAC-SHA1\", " +
            "oauth_timestamp=\"1789905600\", " +
            "oauth_token=\"token\", " +
            "oauth_version=\"1.0\"";

        Assert(
            string.Equals(authorization, expectedAuthorization, StringComparison.Ordinal),
            "OAuth authorization should match the deterministic UTF-8 signature vector.");
    }

    private static async Task TestApiResponsesAsync()
    {
        XCredentials credentials = new("key", "key-secret", "token", "token-secret", "1234");

        using (var successHttpClient = new HttpClient(new SequenceHttpMessageHandler(
            Response(HttpStatusCode.Created, "{\"data\":{\"id\":\"created-id\"}}"))))
        {
            var client = new XApiClient(successHttpClient, credentials);
            string postId = await client.CreatePostAsync("Synthetic post", CancellationToken.None);
            Assert(postId == "created-id", "mocked 201 response did not return the post ID.");
        }

        foreach (HttpStatusCode status in new[]
                 {
                     HttpStatusCode.BadRequest,
                     HttpStatusCode.Unauthorized,
                     HttpStatusCode.Forbidden
                 })
        {
            using var httpClient = new HttpClient(new SequenceHttpMessageHandler(Response(status, "{}")));
            var client = new XApiClient(httpClient, credentials);
            await AssertThrowsAsync<InvalidOperationException>(
                () => client.CreatePostAsync("Synthetic post", CancellationToken.None));
        }

        foreach (HttpStatusCode status in new[] { HttpStatusCode.TooManyRequests })
        {
            HttpResponseMessage[] responses =
            [
                Response(status, "{}", retryImmediately: true),
                Response(status, "{}", retryImmediately: true),
                Response(status, "{}", retryImmediately: true)
            ];
            using var httpClient = new HttpClient(new SequenceHttpMessageHandler(responses));
            var client = new XApiClient(httpClient, credentials);
            await AssertThrowsAsync<InvalidOperationException>(
                () => client.CreatePostAsync("Synthetic post", CancellationToken.None));
        }

        using var serverErrorHttpClient = new HttpClient(new SequenceHttpMessageHandler(
            Response(HttpStatusCode.BadGateway, "{}")));
        var serverErrorClient = new XApiClient(serverErrorHttpClient, credentials);
        await AssertThrowsAsync<AmbiguousDeliveryException>(
            () => serverErrorClient.CreatePostAsync("Synthetic post", CancellationToken.None));

        using var networkFailureHttpClient = new HttpClient(new SequenceHttpMessageHandler(
            new HttpRequestException("Synthetic connection reset.")));
        var networkFailureClient = new XApiClient(networkFailureHttpClient, credentials);
        await AssertThrowsAsync<AmbiguousDeliveryException>(
            () => networkFailureClient.CreatePostAsync("Synthetic post", CancellationToken.None));

        using var timeoutHttpClient = new HttpClient(new SequenceHttpMessageHandler(
            new TaskCanceledException("Synthetic timeout.")));
        var timeoutClient = new XApiClient(timeoutHttpClient, credentials);
        await AssertThrowsAsync<AmbiguousDeliveryException>(
            () => timeoutClient.CreatePostAsync("Synthetic post", CancellationToken.None));
    }

    private static HttpResponseMessage Response(
        HttpStatusCode status,
        string body,
        bool retryImmediately = false)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (retryImmediately)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        }

        return response;
    }

    private static async Task TestReceiptsAndReconciliationAsync()
    {
        LearningPublication publication = Publication("Reconcile");
        var client = new FakeXClient([new XPost("existing-id", "Already posted " + publication.CanonicalUrl)]);
        var store = new MemoryStateStore();
        var publisher = new XPublisher(client, store, new JackdawPatioPostComposer());
        PublisherState result = await publisher.PublishAsync(
            [publication],
            PublisherState.Create("test", "base"),
            "head",
            CancellationToken.None);
        Assert(client.CreateCalls == 0, "reconciliation should prevent duplicate creation.");
        Assert(result.Receipts[publication.CanonicalUrl].PostId == "existing-id", "receipt was not reconstructed.");

        var duplicateClient = new FakeXClient([]);
        var duplicatePublisher = new XPublisher(
            duplicateClient,
            new MemoryStateStore(),
            new JackdawPatioPostComposer());
        await duplicatePublisher.PublishAsync([publication], result, "next", CancellationToken.None);
        Assert(duplicateClient.CreateCalls == 0, "existing receipt should prevent creation.");
    }

    private static async Task TestDeferralAsync()
    {
        LearningPublication missingCopy = Publication(
            "Deferred",
            "https://asibackbone.github.io/Learning/deferred.html",
            post: null);
        LearningPublication ready = Publication(
            "Ready",
            "https://asibackbone.github.io/Learning/ready.html");
        var client = new FakeXClient([]);
        var store = new MemoryStateStore();
        var publisher = new XPublisher(client, store, new JackdawPatioPostComposer());

        PublisherState deferred = await publisher.PublishAsync(
            [missingCopy, ready],
            PublisherState.Create("test", "base"),
            "head",
            CancellationToken.None);

        Assert(deferred.CursorSha == "head", "a missing x_post should not block cursor advancement.");
        Assert(deferred.DeferredPublications.ContainsKey(missingCopy.CanonicalUrl), "missing x_post was not recorded durably.");
        Assert(
            deferred.DeferredPublications[missingCopy.CanonicalUrl].SourcePath == missingCopy.SourcePath,
            "the deferral did not retain the source path for later review.");
        Assert(deferred.Receipts.ContainsKey(ready.CanonicalUrl), "a later ready publication was not published.");
        Assert(client.CreateCalls == 1, "only the ready publication should have been created.");

        LearningPublication copyAdded = missingCopy with
        {
            SourceBlobSha = "copy-ready-blob",
            XPost = "A deferred engineering takeaway is now ready."
        };
        PublisherState completed = await publisher.PublishAsync(
            [copyAdded],
            deferred,
            "next",
            CancellationToken.None);

        Assert(completed.Receipts.ContainsKey(copyAdded.CanonicalUrl), "the deferred publication was not published when copy became ready.");
        Assert(!completed.DeferredPublications.ContainsKey(copyAdded.CanonicalUrl), "the resolved deferral was not cleared.");
        Assert(client.CreateCalls == 2, "the deferred publication should create exactly one later post.");
    }

    private static async Task TestAmbiguousDeliveryAsync()
    {
        LearningPublication publication = Publication("Ambiguous");
        var client = new FakeXClient([], ambiguousThenPost: new XPost("reconciled-id", "Posted " + publication.CanonicalUrl));
        var publisher = new XPublisher(client, new MemoryStateStore(), new JackdawPatioPostComposer());
        PublisherState result = await publisher.PublishAsync(
            [publication],
            PublisherState.Create("test", "base"),
            "head",
            CancellationToken.None);
        Assert(result.Receipts[publication.CanonicalUrl].PostId == "reconciled-id", "ambiguous timeout was not reconciled.");
    }

    private static async Task TestReceiptFailureAsync()
    {
        LearningPublication publication = Publication("Receipt failure");
        var store = new MemoryStateStore { FailOnSave = true };
        var publisher = new XPublisher(new FakeXClient([]), store, new JackdawPatioPostComposer());
        PublisherState initial = PublisherState.Create("test", "base");
        await AssertThrowsAsync<IOException>(() => publisher.PublishAsync(
            [publication], initial, "head", CancellationToken.None));
        Assert(initial.CursorSha == "base" && initial.Receipts.Count == 0, "failed receipt write advanced durable state.");
    }

    private static void TestDryRun()
    {
        LearningPublication first = Publication("B title", "https://asibackbone.github.io/Learning/b.html");
        LearningPublication second = Publication("A title", "https://asibackbone.github.io/Learning/a.html");
        LearningPublication deferred = Publication("Deferred", "https://asibackbone.github.io/Learning/deferred.html", post: null);
        PublisherState state = PublisherState.Create("test", "base").WithDeferral(
            "https://asibackbone.github.io/Learning/existing-deferral.html",
            new XPublicationDeferral("docs/existing-deferral.md", "deferred-blob", new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero)));
        IReadOnlyList<string> one = XPublisher.RenderDryRun([first, second, deferred], state);
        IReadOnlyList<string> two = XPublisher.RenderDryRun([deferred, second, first], state);
        Assert(one.SequenceEqual(two), "dry-run output was not deterministic.");
        Assert(!string.Join('\n', one).Contains("secret", StringComparison.OrdinalIgnoreCase), "dry run exposed secret text.");
        Assert(string.Join('\n', one).Contains("DEFERRED: missing 'x_post'", StringComparison.Ordinal), "dry run did not surface missing copy.");
        Assert(string.Join('\n', one).Contains("docs/existing-deferral.md", StringComparison.Ordinal), "dry run omitted a recorded deferral after cursor advancement.");
    }

    private static void TestStateMigration()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                "{\"schemaVersion\":1,\"account\":\"test\",\"cursorSha\":\"base\",\"receipts\":{}}");
            PublisherState legacy = new FilePublisherStateStore(path).Load() ??
                throw new InvalidOperationException("legacy state was not loaded.");
            Assert(legacy.DeferredPublications.Count == 0, "legacy state should initialize an empty deferral set.");
            Assert(legacy.WithCursor("next").SchemaVersion == 2, "saving legacy state should migrate it to schema version 2.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static LearningPublication Publication(
        string title,
        string canonicalUrl = "https://asibackbone.github.io/Learning/article.html",
        IReadOnlyList<string>? hashtags = null,
        string? post = "A concise engineering takeaway.",
        string? question = null) =>
        new(title, "Test Author", Today, "Summary", hashtags ?? ["DotNet"], "docs/article.md", "blob", canonicalUrl, post, question);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}

sealed class FakeXClient(List<XPost> recent, XPost? ambiguousThenPost = null) : IXClient
{
    private bool ambiguityTriggered;
    public int CreateCalls { get; private set; }

    public Task<IReadOnlyList<XPost>> GetRecentPostsAsync(CancellationToken cancellationToken)
    {
        if (ambiguityTriggered && ambiguousThenPost is not null && !recent.Contains(ambiguousThenPost))
        {
            recent.Add(ambiguousThenPost);
        }

        return Task.FromResult<IReadOnlyList<XPost>>(recent);
    }

    public Task<string> CreatePostAsync(string text, CancellationToken cancellationToken)
    {
        CreateCalls++;

        if (ambiguousThenPost is not null)
        {
            ambiguityTriggered = true;
            throw new AmbiguousDeliveryException("Synthetic ambiguous timeout.");
        }

        return Task.FromResult("created-id");
    }
}

sealed class MemoryStateStore : IPublisherStateStore
{
    public bool FailOnSave { get; init; }
    public PublisherState? Saved { get; private set; }
    public PublisherState? Load() => Saved;

    public void Save(PublisherState state)
    {
        if (FailOnSave)
        {
            throw new IOException("Synthetic receipt-write failure.");
        }

        Saved = state;
    }
}

sealed class SequenceHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<object> _outcomes;

    public SequenceHttpMessageHandler(params object[] outcomes)
    {
        _outcomes = new Queue<object>(outcomes);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_outcomes.Count == 0)
        {
            throw new InvalidOperationException("No synthetic HTTP outcome remains.");
        }

        object outcome = _outcomes.Dequeue();
        return outcome is Exception exception
            ? Task.FromException<HttpResponseMessage>(exception)
            : Task.FromResult((HttpResponseMessage)outcome);
    }
}

interface IXClient
{
    Task<IReadOnlyList<XPost>> GetRecentPostsAsync(CancellationToken cancellationToken);
    Task<string> CreatePostAsync(string text, CancellationToken cancellationToken);
}

interface IXPostComposer
{
    string Compose(LearningPublication publication);
}

interface IPublisherStateStore
{
    PublisherState? Load();
    void Save(PublisherState state);
}

sealed class AmbiguousDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);

enum XResponseDisposition
{
    Success,
    PermanentFailure,
    AuthenticationFailure,
    Retryable
}

enum GitChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed
}

sealed record GitChange(GitChangeKind Kind, string? OldPath, string NewPath);
sealed record ProcessResult(int ExitCode, string Output, string Error);
sealed record PublicationMetadata(
    string Title,
    string Author,
    DateOnly Published,
    string Summary,
    IReadOnlyList<string> XHashtags,
    string? XPost = null,
    string? XQuestion = null);
sealed record LearningPublication(
    string Title,
    string Author,
    DateOnly Published,
    string Summary,
    IReadOnlyList<string> XHashtags,
    string SourcePath,
    string SourceBlobSha,
    string CanonicalUrl,
    string? XPost = null,
    string? XQuestion = null);
sealed record XPost(string Id, string Text);
sealed record XPublicationReceipt(string SourceBlobSha, string PostId, DateTimeOffset PostedAtUtc);
sealed record XPublicationDeferral(string SourcePath, string SourceBlobSha, DateTimeOffset DeferredAtUtc);
sealed record CreatePostRequest(string Text);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(PublisherState))]
[JsonSerializable(typeof(CreatePostRequest))]
internal sealed partial class XPublisherJsonContext : JsonSerializerContext;
