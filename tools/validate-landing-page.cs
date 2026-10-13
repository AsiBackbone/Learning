using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

return LandingPageValidator.Run(args);

// Validates the Learning homepage against the organization landing-page standard
// (AsiBackbone/.github LANDING-PAGE-STANDARD.md). The checks assert required
// destinations, section order, and shared statements; visual review remains manual.
static class LandingPageValidator
{
    private const string ToolManifestRelativePath = ".config/dotnet-tools.json";
    private const string HomepageRelativePath = "docs/index.md";
    private const string TocRelativePath = "docs/toc.yml";
    private const string DocFxConfigRelativePath = "docs/docfx.json";
    private const string BuiltHomepageRelativePath = "docs/_site/index.html";

    private const string DesignPhilosophyStatement =
        "We favor architectures where consequential operations are explicit, constrained, reviewable, and auditable.";

    // Required homepage sections in the order the standard prescribes.
    private static readonly string[] RequiredHomepageMarkers =
    [
        "<div class=\"home-hero\">",
        "## Start here",
        "## Is Learning a fit?",
        "## Related projects",
        "## Design philosophy",
    ];

    private static readonly string[] RequiredHomepageContent =
    [
        "class=\"btn btn-primary\" href=\"getting-started/index.md\"",
        "href=\"getting-started/find-your-path.md\"",
        "href=\"articles/index.md\"",
        "architecture/when-aspnet-core-authorization-is-enough.md",
        "architecture/when-a-simple-application-service-is-enough.md",
        "Learning does not require installing an <code>AsiBackbone</code> package.",
        "<article class=\"project-card\" aria-current=\"page\">",
        "https://asibackbone.github.io/AsiBackbone/",
        "https://asibackbone.github.io/NetCoreApplicationTemplate/",
        "https://github.com/AsiBackbone/Learning",
        "https://doi.org/10.5281/zenodo.21938556",
        "https://doi.org/10.5281/zenodo.20546032",
        "https://doi.org/10.5281/zenodo.20373042",
        DesignPhilosophyStatement,
    ];

    // Ecosystem navigation labels and destinations shared by every AsiBackbone site.
    private static readonly (string Label, string Href)[] EcosystemNavigation =
    [
        ("Organization", "https://asibackbone.github.io/"),
        ("Learning", "index.md"),
        ("Community", "https://asibackbone.github.io/#community"),
        ("Framework", "https://asibackbone.github.io/AsiBackbone/"),
        ("Template", "https://asibackbone.github.io/NetCoreApplicationTemplate/"),
        ("GitHub", "https://github.com/AsiBackbone"),
    ];

    private static readonly string[] RequiredFooterContent =
    [
        "Part of <a href=\\\"https://asibackbone.github.io/\\\">AsiBackbone</a>",
        "https://github.com/AsiBackbone/Learning\\\"",
        "https://github.com/orgs/AsiBackbone/discussions",
        "https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md",
        "https://doi.org/10.5281/zenodo.21938556",
    ];

    // DocFX heading anchors, in order, expected in the built homepage.
    private static readonly string[] RequiredBuiltSectionIds =
    [
        "id=\"start-here\"",
        "id=\"is-learning-a-fit\"",
        "id=\"related-projects\"",
        "id=\"design-philosophy\"",
    ];

    public static int Run(string[] arguments)
    {
        bool selfTest = arguments.Contains("--self-test", StringComparer.Ordinal);
        bool validateSite = arguments.Contains("--site", StringComparer.Ordinal);

        if (arguments.Any(argument => argument is not "--self-test" and not "--site"))
        {
            Console.Error.WriteLine("Usage: dotnet run --file tools/validate-landing-page.cs [-- --self-test | --site]");
            return 2;
        }

        if (selfTest)
        {
            return RunSelfTest();
        }

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
        ValidateHomepage(ReadRequired(repositoryRoot, HomepageRelativePath, errors), errors);
        ValidateNavigation(ReadRequired(repositoryRoot, TocRelativePath, errors), errors);
        ValidateFooter(ReadRequired(repositoryRoot, DocFxConfigRelativePath, errors), errors);

        if (validateSite)
        {
            ValidateBuiltHomepage(ReadRequired(repositoryRoot, BuiltHomepageRelativePath, errors), errors);
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        Console.WriteLine(validateSite
            ? "Learning landing-page source and built homepage satisfy the organization landing-page standard."
            : "Learning landing-page source satisfies the organization landing-page standard.");
        return 0;
    }

    private static void ValidateHomepage(string? homepage, ICollection<string> errors)
    {
        if (homepage is null)
        {
            return;
        }

        RequireInOrder(homepage, RequiredHomepageMarkers, HomepageRelativePath, errors);

        foreach (string requiredText in RequiredHomepageContent)
        {
            if (!homepage.Contains(requiredText, StringComparison.Ordinal))
            {
                errors.Add($"'{HomepageRelativePath}' is missing required landing-page content: {requiredText}");
            }
        }
    }

    private static void ValidateNavigation(string? toc, ICollection<string> errors)
    {
        if (toc is null)
        {
            return;
        }

        string[] lines = toc.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach ((string label, string href) in EcosystemNavigation)
        {
            int labelIndex = Array.FindIndex(lines, line => line.Trim() == $"- name: {label}");
            bool linked = labelIndex >= 0 &&
                labelIndex + 1 < lines.Length &&
                lines[labelIndex + 1].Trim() == $"href: {href}";

            if (!linked)
            {
                errors.Add($"'{TocRelativePath}' must link the '{label}' ecosystem destination to '{href}'.");
            }
        }
    }

    private static void ValidateFooter(string? docFxConfig, ICollection<string> errors)
    {
        if (docFxConfig is null)
        {
            return;
        }

        string? footerLine = docFxConfig
            .Split('\n')
            .FirstOrDefault(line => line.Contains("\"_appFooter\"", StringComparison.Ordinal));

        if (footerLine is null)
        {
            errors.Add($"'{DocFxConfigRelativePath}' must define _appFooter.");
            return;
        }

        foreach (string requiredText in RequiredFooterContent)
        {
            if (!footerLine.Contains(requiredText, StringComparison.Ordinal))
            {
                errors.Add($"'{DocFxConfigRelativePath}' _appFooter is missing required footer content: {requiredText}");
            }
        }
    }

    private static void ValidateBuiltHomepage(string? html, ICollection<string> errors)
    {
        if (html is null)
        {
            return;
        }

        RequireInOrder(html, RequiredBuiltSectionIds, BuiltHomepageRelativePath, errors);

        if (!html.Contains(DesignPhilosophyStatement, StringComparison.Ordinal))
        {
            errors.Add($"'{BuiltHomepageRelativePath}' does not render the Design Philosophy statement.");
        }
    }

    private static void RequireInOrder(
        string content,
        IReadOnlyList<string> markers,
        string relativePath,
        ICollection<string> errors)
    {
        int previousIndex = -1;
        string? previousMarker = null;

        foreach (string marker in markers)
        {
            int index = content.IndexOf(marker, StringComparison.Ordinal);

            if (index < 0)
            {
                errors.Add($"'{relativePath}' is missing required section: {marker}");
                continue;
            }

            if (index < previousIndex)
            {
                errors.Add($"'{relativePath}' places '{marker}' before '{previousMarker}'; required order is {string.Join(", ", markers)}.");
            }

            previousIndex = index;
            previousMarker = marker;
        }
    }

    private static int RunSelfTest()
    {
        string validHomepage = string.Join(
            "\n",
            RequiredHomepageMarkers.Concat(RequiredHomepageContent));
        string validToc = string.Join(
            "\n",
            EcosystemNavigation.SelectMany(entry => new[] { $"  - name: {entry.Label}", $"    href: {entry.Href}" }));
        string validFooter = $"\"_appFooter\": \"{string.Join(" · ", RequiredFooterContent)}\",";
        string validSite = string.Join("\n", RequiredBuiltSectionIds) + "\n" + DesignPhilosophyStatement;

        var failures = new List<string>();

        void Expect(string name, Action<ICollection<string>> check, bool shouldPass)
        {
            var errors = new List<string>();
            check(errors);

            if ((errors.Count == 0) != shouldPass)
            {
                failures.Add($"Self-test '{name}' expected {(shouldPass ? "success" : "failure")} but got {errors.Count} error(s).");
            }
        }

        Expect("valid homepage", errors => ValidateHomepage(validHomepage, errors), shouldPass: true);
        Expect("missing product fit", errors => ValidateHomepage(validHomepage.Replace("## Is Learning a fit?", string.Empty, StringComparison.Ordinal), errors), shouldPass: false);
        Expect(
            "philosophy before related projects",
            errors => ValidateHomepage(
                string.Join("\n", RequiredHomepageMarkers.Take(3).Append("## Design philosophy").Append("## Related projects").Concat(RequiredHomepageContent)),
                errors),
            shouldPass: false);
        Expect("missing philosophy statement", errors => ValidateHomepage(validHomepage.Replace(DesignPhilosophyStatement, string.Empty, StringComparison.Ordinal), errors), shouldPass: false);
        Expect("valid navigation", errors => ValidateNavigation(validToc, errors), shouldPass: true);
        Expect("missing Community", errors => ValidateNavigation(validToc.Replace("- name: Community", "- name: Discussions", StringComparison.Ordinal), errors), shouldPass: false);
        Expect("wrong GitHub destination", errors => ValidateNavigation(validToc.Replace("href: https://github.com/AsiBackbone", "href: https://github.com/AsiBackbone/Learning", StringComparison.Ordinal), errors), shouldPass: false);
        Expect("valid footer", errors => ValidateFooter(validFooter, errors), shouldPass: true);
        Expect("footer without license", errors => ValidateFooter(validFooter.Replace("LICENSING.md", "README.md", StringComparison.Ordinal), errors), shouldPass: false);
        Expect("valid built homepage", errors => ValidateBuiltHomepage(validSite, errors), shouldPass: true);
        Expect("built homepage missing section", errors => ValidateBuiltHomepage(validSite.Replace("id=\"related-projects\"", string.Empty, StringComparison.Ordinal), errors), shouldPass: false);

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        Console.WriteLine("Landing-page validator self-test passed.");
        return 0;
    }

    private static string? ReadRequired(string repositoryRoot, string relativePath, ICollection<string> errors)
    {
        string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
        {
            errors.Add($"Required file is missing: '{relativePath}'.");
            return null;
        }

        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot(string startPath)
    {
        DirectoryInfo? current = new(startPath);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    ToolManifestRelativePath.Replace('/', Path.DirectorySeparatorChar))))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root containing '{ToolManifestRelativePath}'.");
    }
}
