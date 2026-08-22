using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

return DocFxTemplateBaselineValidator.Run();

static class DocFxTemplateBaselineValidator
{
    private const string ToolManifestRelativePath = ".config/dotnet-tools.json";
    private const string BaselineRelativePath = "docs/templates/docfx-template-baseline.json";
    private const string DocFxConfigRelativePath = "docs/docfx.json";

    private static readonly Regex TemplateVersionRegex = new(
        @"DocFX modern template v(?<version>\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?)",
        RegexOptions.Compiled);

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

        var errors = new List<string>();

        string toolManifestPath = Path.Combine(repositoryRoot, ToolManifestRelativePath);
        string baselinePath = Path.Combine(repositoryRoot, BaselineRelativePath);
        string docFxConfigPath = Path.Combine(repositoryRoot, DocFxConfigRelativePath);

        string pinnedVersion;
        TemplateBaseline baseline;

        try
        {
            pinnedVersion = ReadPinnedDocFxVersion(toolManifestPath);
            baseline = ReadBaseline(baselinePath);
        }
        catch (Exception exception) when (
            exception is IOException or
            JsonException or
            InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        if (!string.Equals(
                pinnedVersion,
                baseline.DocFxVersion,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Pinned DocFX version '{pinnedVersion}' does not match template baseline version '{baseline.DocFxVersion}'.");
        }

        string expectedRelease = $"v{baseline.DocFxVersion}";

        if (!string.Equals(
                expectedRelease,
                baseline.UpstreamRelease,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Template baseline upstream release '{baseline.UpstreamRelease}' should be '{expectedRelease}'.");
        }

        if (!string.Equals(
                baseline.Template,
                "modern",
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Template baseline declares '{baseline.Template}' instead of the expected 'modern' template.");
        }

        string localOverridePath = Path.Combine(
            repositoryRoot,
            baseline.LocalOverride.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(localOverridePath))
        {
            errors.Add(
                $"Declared local template override '{baseline.LocalOverride}' does not exist.");
        }
        else
        {
            ValidateTemplateComment(
                localOverridePath,
                baseline.DocFxVersion,
                errors);
        }

        try
        {
            ValidateDocFxConfiguration(
                docFxConfigPath,
                baseline.Template,
                errors);
        }
        catch (Exception exception) when (
            exception is IOException or
            JsonException or
            InvalidOperationException)
        {
            errors.Add(exception.Message);
        }

        if (errors.Count > 0)
        {
            Console.Error.WriteLine("DocFX custom-template baseline validation failed:");

            foreach (string error in errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "When changing DocFX, review the corresponding upstream modern template against docs/templates/layout/_master.tmpl, reapply only intentional customizations, then update the baseline metadata and template comment.");

            return 1;
        }

        Console.WriteLine(
            $"DocFX custom-template baseline is aligned with pinned DocFX {pinnedVersion} ({baseline.UpstreamRelease}).");

        return 0;
    }

    private static string ReadPinnedDocFxVersion(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("tools", out JsonElement tools) ||
            !tools.TryGetProperty("docfx", out JsonElement docFx) ||
            !docFx.TryGetProperty("version", out JsonElement versionElement))
        {
            throw new InvalidOperationException(
                $"Could not find tools.docfx.version in '{ToolManifestRelativePath}'.");
        }

        string? version = versionElement.GetString();

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException(
                $"DocFX version in '{ToolManifestRelativePath}' is empty.");
        }

        return version;
    }

    private static TemplateBaseline ReadBaseline(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Missing DocFX template baseline metadata at '{BaselineRelativePath}'.");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        if (!TryReadNonEmptyString(root, "template", out string? template) ||
            !TryReadNonEmptyString(root, "docfxVersion", out string? docFxVersion) ||
            !TryReadNonEmptyString(root, "upstreamRelease", out string? upstreamRelease) ||
            !TryReadNonEmptyString(root, "localOverride", out string? localOverride))
        {
            throw new InvalidOperationException(
                $"DocFX template baseline metadata in '{BaselineRelativePath}' is incomplete.");
        }

        return new TemplateBaseline(
            template!,
            docFxVersion!,
            upstreamRelease!,
            localOverride!);
    }

    private static bool TryReadNonEmptyString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void ValidateTemplateComment(
        string path,
        string baselineVersion,
        ICollection<string> errors)
    {
        string template = File.ReadAllText(path);
        Match match = TemplateVersionRegex.Match(template);

        if (!match.Success)
        {
            errors.Add(
                "The custom _master.tmpl does not declare its DocFX modern-template baseline version.");
            return;
        }

        string declaredVersion = match.Groups["version"].Value;

        if (!string.Equals(
                declaredVersion,
                baselineVersion,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Custom _master.tmpl declares DocFX modern template v{declaredVersion}, but baseline metadata declares v{baselineVersion}.");
        }
    }

    private static void ValidateDocFxConfiguration(
        string path,
        string upstreamTemplate,
        ICollection<string> errors)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("build", out JsonElement build) ||
            !build.TryGetProperty("template", out JsonElement templates) ||
            templates.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Could not find build.template array in '{DocFxConfigRelativePath}'.");
        }

        string[] configuredTemplates = templates
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString()!)
            .ToArray();

        if (!configuredTemplates.Contains(upstreamTemplate, StringComparer.Ordinal))
        {
            errors.Add(
                $"'{DocFxConfigRelativePath}' does not include the '{upstreamTemplate}' upstream template declared by the baseline.");
        }

        if (!configuredTemplates.Contains("templates", StringComparer.Ordinal))
        {
            errors.Add(
                $"'{DocFxConfigRelativePath}' does not include the local 'templates' override directory.");
        }
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

    private sealed record TemplateBaseline(
        string Template,
        string DocFxVersion,
        string UpstreamRelease,
        string LocalOverride);
}
