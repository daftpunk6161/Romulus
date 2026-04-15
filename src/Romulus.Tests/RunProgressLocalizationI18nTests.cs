using System.Text.RegularExpressions;
using Xunit;

namespace Romulus.Tests;

public sealed class RunProgressLocalizationI18nTests
{
    [Fact]
    public void DB031_FrenchDictionary_ContainsAllGermanAndEnglishKeys()
    {
        var source = ReadInfrastructureSource("Orchestration/RunProgressLocalization.cs");

        var deKeys = ExtractDictionaryKeys(source, "De");
        var enKeys = ExtractDictionaryKeys(source, "En");
        var frKeys = ExtractDictionaryKeys(source, "Fr");

        var requiredKeys = deKeys
            .Union(enKeys, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var missing = requiredKeys
            .Except(frKeys, StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            "French localization is missing keys: " + string.Join(", ", missing));
    }

    private static HashSet<string> ExtractDictionaryKeys(string source, string dictionaryName)
    {
        var pattern = $@"private\s+static\s+readonly\s+IReadOnlyDictionary<string,\s*string>\s+{dictionaryName}\s*=\s*new\s+Dictionary<string,\s*string>\([^)]*\)\s*\{{(?<content>.*?)\n\s*\}};";
        var blockMatch = Regex.Match(source, pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(blockMatch.Success, $"Could not locate dictionary block '{dictionaryName}'.");

        var content = blockMatch.Groups["content"].Value;
        var keys = Regex.Matches(content, "\\[\\\"(?<key>[^\\\"]+)\\\"\\]\\s*=", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);

        return keys;
    }

    private static string ReadInfrastructureSource(string relativePath)
    {
        var root = FindRepositoryRoot();
        var fullPath = Path.Combine(root, "src", "Romulus.Infrastructure", relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not resolve repository root from test context.");
    }
}
