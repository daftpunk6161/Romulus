using System.Reflection;
using System.Text.Json;
using Romulus.Core.Scoring;
using Xunit;

namespace Romulus.Tests;

public sealed class FormatScoreProfileInvariantTests
{
    [Fact]
    public void F014_FormatScoreJson_CoversAllFallbackFormatScores_WithSameValues()
    {
        var root = FindRepositoryRoot();
        var jsonPath = Path.Combine(root, "data", "format-scores.json");
        Assert.True(File.Exists(jsonPath), $"Missing file: {jsonPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var formatScores = ParseScoreMap(doc.RootElement, "formatScores");

        var fallbackFormatScores = GetPrivateReadonlyField<IReadOnlyDictionary<string, int>>(
            typeof(FormatScorer),
            "FallbackFormatScores");

        foreach (var (extension, fallbackScore) in fallbackFormatScores)
        {
            Assert.True(formatScores.TryGetValue(extension, out var jsonScore),
                $"format-scores.json is missing fallback extension '{extension}'.");
            Assert.Equal(fallbackScore, jsonScore);
        }
    }

    [Fact]
    public void F014_FormatScoreJson_CoversAllFallbackSetTypeScores_WithSameValues()
    {
        var root = FindRepositoryRoot();
        var jsonPath = Path.Combine(root, "data", "format-scores.json");
        Assert.True(File.Exists(jsonPath), $"Missing file: {jsonPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var setTypeScores = ParseScoreMap(doc.RootElement, "setTypeScores");

        var fallbackSetTypeScores = GetPrivateReadonlyField<IReadOnlyDictionary<string, int>>(
            typeof(FormatScorer),
            "FallbackSetTypeScores");

        foreach (var (setType, fallbackScore) in fallbackSetTypeScores)
        {
            Assert.True(setTypeScores.TryGetValue(setType, out var jsonScore),
                $"format-scores.json is missing fallback set type '{setType}'.");
            Assert.Equal(fallbackScore, jsonScore);
        }
    }

    [Fact]
    public void F014_FormatScoreJson_CoversAllFallbackDiscExtensions()
    {
        var root = FindRepositoryRoot();
        var jsonPath = Path.Combine(root, "data", "format-scores.json");
        Assert.True(File.Exists(jsonPath), $"Missing file: {jsonPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var discExtensions = ParseStringSet(doc.RootElement, "discExtensions");

        var fallbackDiscExtensions = GetPrivateReadonlyField<IReadOnlySet<string>>(
            typeof(FormatScorer),
            "FallbackDiscExtensions");

        foreach (var extension in fallbackDiscExtensions)
        {
            Assert.Contains(extension, discExtensions);
        }
    }

    private static T GetPrivateReadonlyField<T>(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var value = field!.GetValue(null);
        Assert.NotNull(value);

        return (T)value!;
    }

    private static Dictionary<string, int> ParseScoreMap(JsonElement root, string propertyName)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var entry in property.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var score))
                map[entry.Name] = score;
        }

        return map;
    }

    private static HashSet<string> ParseStringSet(JsonElement root, string propertyName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            return set;

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                set.Add(value);
        }

        return set;
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
