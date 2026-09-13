using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

            if (current is null || previous is not null)
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
                change.NewPath,
                getBlobSha(change.NewPath),
                canonicalUrl));
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
        "title", "author", "published", "summary", "feed"
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

        return new PublicationMetadata(
            Required("title"),
            Required("author"),
            published,
            Required("summary"));
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
    private const int MaximumLength = 280;
    private const string Prefix = "New from ASI Backbone Learning:\n\n";

    public string Compose(LearningPublication publication)
    {
        string suffix = $"\n\n{publication.CanonicalUrl}";
        int titleBudget = MaximumLength - CountTextElements(Prefix) - CountTextElements(suffix);

        if (titleBudget < 1)
        {
            throw new InvalidDataException("The canonical URL leaves no room for a post title.");
        }

        string title = Truncate(publication.Title, titleBudget);
        string post = Prefix + title + suffix;

        if (CountTextElements(post) > MaximumLength)
        {
            throw new InvalidDataException("The composed X post exceeds 280 Unicode text elements.");
        }

        return post;
    }

    private static string Truncate(string value, int maximumElements)
    {
        int count = CountTextElements(value);

        if (count <= maximumElements)
        {
            return value;
        }

        const string ellipsis = "…";
        int kept = Math.Max(0, maximumElements - 1);
        int[] indexes = StringInfo.ParseCombiningCharacters(value);
        return value[..indexes[kept]] + ellipsis;
    }

    private static int CountTextElements(string value) =>
        StringInfo.ParseCombiningCharacters(value).Length;
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

        var lines = new List<string> { $"Dry run: {pending.Length} publication(s) pending." };
        var composer = new JackdawPatioPostComposer();

        foreach (LearningPublication publication in pending)
        {
            lines.Add($"--- {publication.SourcePath}");
            lines.Add(composer.Compose(publication));
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

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
        return retryAfter is not null && retryAfter <= TimeSpan.FromSeconds(30)
            ? retryAfter.Value
            : TimeSpan.FromSeconds(attempt);
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
        XCredentials credentials)
    {
        var oauth = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = credentials.ApiKey,
            ["oauth_nonce"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
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

        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(signatureBase)));
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

        if (state.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(state.Account) ||
            string.IsNullOrWhiteSpace(state.CursorSha))
        {
            throw new InvalidDataException("Publisher state has an unsupported or incomplete schema.");
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
    public int SchemaVersion { get; init; } = 1;
    public required string Account { get; init; }
    public required string CursorSha { get; init; }
    public Dictionary<string, XPublicationReceipt> Receipts { get; init; } = new(StringComparer.Ordinal);

    public static PublisherState Create(string account, string cursorSha) =>
        new() { Account = account, CursorSha = cursorSha };

    public PublisherState WithReceipt(string canonicalUrl, XPublicationReceipt receipt)
    {
        var receipts = new Dictionary<string, XPublicationReceipt>(Receipts, StringComparer.Ordinal)
        {
            [canonicalUrl] = receipt
        };
        return new PublisherState { Account = Account, CursorSha = CursorSha, Receipts = receipts };
    }

    public PublisherState WithCursor(string cursorSha) =>
        new()
        {
            Account = Account,
            CursorSha = cursorSha,
            Receipts = new Dictionary<string, XPublicationReceipt>(Receipts, StringComparer.Ordinal)
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
            TestComposition();
            TestResponseClassification();
            await TestApiResponsesAsync();
            await TestReceiptsAndReconciliationAsync();
            await TestAmbiguousDeliveryAsync();
            await TestReceiptFailureAsync();
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
        const string eligible = "---\ntitle: New article\nauthor: Test Author\npublished: 2026-09-12\nsummary: Summary.\nfeed: true\n---\n";
        const string ineligible = "---\ntitle: Draft\nfeed: false\n---\n";
        var baseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["docs/articles/enabled.md"] = ineligible,
            ["docs/articles/updated.md"] = eligible,
            ["docs/articles/old-name.md"] = eligible,
            ["docs/articles/deleted.md"] = eligible
        };
        var headFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["docs/articles/new.md"] = eligible,
            ["docs/articles/enabled.md"] = eligible,
            ["docs/articles/updated.md"] = eligible.Replace("Summary.", "Updated summary.", StringComparison.Ordinal),
            ["docs/articles/new-name.md"] = eligible,
            ["docs/articles/navigation.md"] = "# Navigation"
        };
        GitChange[] changes =
        [
            new(GitChangeKind.Added, null, "docs/articles/new.md"),
            new(GitChangeKind.Modified, "docs/articles/enabled.md", "docs/articles/enabled.md"),
            new(GitChangeKind.Modified, "docs/articles/updated.md", "docs/articles/updated.md"),
            new(GitChangeKind.Renamed, "docs/articles/old-name.md", "docs/articles/new-name.md"),
            new(GitChangeKind.Deleted, "docs/articles/deleted.md", "docs/articles/deleted.md"),
            new(GitChangeKind.Added, null, "docs/articles/navigation.md")
        ];
        var sitemap = new HashSet<string>(StringComparer.Ordinal)
        {
            new Uri(SiteRoot, "articles/new.html").AbsoluteUri,
            new Uri(SiteRoot, "articles/enabled.html").AbsoluteUri
        };
        IReadOnlyList<LearningPublication> selected = GitPublicationSelector.SelectChanges(
            changes,
            path => baseFiles.GetValueOrDefault(path),
            path => headFiles.GetValueOrDefault(path),
            _ => "blob-sha",
            sitemap,
            Today,
            SiteRoot);

        Assert(selected.Count == 2, "new and newly enabled documents should be selected exactly once.");
        Assert(selected.Any(item => item.SourcePath.EndsWith("new.md", StringComparison.Ordinal)), "new document missing.");
        Assert(selected.Any(item => item.SourcePath.EndsWith("enabled.md", StringComparison.Ordinal)), "enabled transition missing.");
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
        string longTitle = string.Concat(Enumerable.Repeat("👩🏽‍💻", 300));
        LearningPublication publication = Publication(longTitle);
        string post = new JackdawPatioPostComposer().Compose(publication);
        Assert(StringInfo.ParseCombiningCharacters(post).Length <= 280, "post exceeded text-element limit.");
        Assert(post.EndsWith(publication.CanonicalUrl, StringComparison.Ordinal), "canonical URL was not preserved.");
    }

    private static void TestResponseClassification()
    {
        Assert(XApiClient.Classify(HttpStatusCode.Created) == XResponseDisposition.Success, "201 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.BadRequest) == XResponseDisposition.PermanentFailure, "400 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.Unauthorized) == XResponseDisposition.AuthenticationFailure, "401 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.Forbidden) == XResponseDisposition.AuthenticationFailure, "403 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.TooManyRequests) == XResponseDisposition.Retryable, "429 classification failed.");
        Assert(XApiClient.Classify(HttpStatusCode.BadGateway) == XResponseDisposition.Retryable, "5xx classification failed.");
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

        foreach (HttpStatusCode status in new[]
                 {
                     HttpStatusCode.TooManyRequests,
                     HttpStatusCode.BadGateway
                 })
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
        IReadOnlyList<string> one = XPublisher.RenderDryRun([first, second], PublisherState.Create("test", "base"));
        IReadOnlyList<string> two = XPublisher.RenderDryRun([second, first], PublisherState.Create("test", "base"));
        Assert(one.SequenceEqual(two), "dry-run output was not deterministic.");
        Assert(!string.Join('\n', one).Contains("secret", StringComparison.OrdinalIgnoreCase), "dry run exposed secret text.");
    }

    private static LearningPublication Publication(
        string title,
        string canonicalUrl = "https://asibackbone.github.io/Learning/article.html") =>
        new(title, "Test Author", Today, "Summary", "docs/article.md", "blob", canonicalUrl);

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
sealed record PublicationMetadata(string Title, string Author, DateOnly Published, string Summary);
sealed record LearningPublication(
    string Title,
    string Author,
    DateOnly Published,
    string Summary,
    string SourcePath,
    string SourceBlobSha,
    string CanonicalUrl);
sealed record XPost(string Id, string Text);
sealed record XPublicationReceipt(string SourceBlobSha, string PostId, DateTimeOffset PostedAtUtc);
sealed record CreatePostRequest(string Text);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(PublisherState))]
[JsonSerializable(typeof(CreatePostRequest))]
internal sealed partial class XPublisherJsonContext : JsonSerializerContext;
