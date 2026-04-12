using System.Text.Json;
using Romulus.Infrastructure.Orchestration;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// RED tests for audit tracker priority block 7-12:
/// 7) ERR-08 (sync-over-async hotspots)
/// 8) TH-01/10 (HeaderRepairService must avoid ReadAllBytes)
/// 9) TH-06 (tool hash placeholders and unverified ECM path)
/// 10) ORC-01 (fire-and-forget trigger path in ApiAutomationService)
/// 11) DATA-01/02/11 (extended startup schema validation coverage)
/// 12) I18N-01/03/04 (FR branding + localized phase detail strings)
/// </summary>
public sealed class TrackerBlock7To12RedTests
{
    [Fact]
    public void ERR_08_TargetFiles_MustNotUseSyncOverAsyncGetResult()
    {
        var runManager = File.ReadAllText(FindRepoFile("src", "Romulus.Api", "RunManager.cs"));
        var runService = File.ReadAllText(FindRepoFile("src", "Romulus.UI.Wpf", "Services", "RunService.cs"));
        var cliProgram = File.ReadAllText(FindRepoFile("src", "Romulus.CLI", "Program.cs"));

        Assert.DoesNotContain("GetAwaiter().GetResult()", runManager, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", runService, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", cliProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void TH_01_10_HeaderRepairService_MustNotUseReadAllBytes()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Romulus.Infrastructure", "Hashing", "HeaderRepairService.cs"));

        Assert.DoesNotContain("File.ReadAllBytes(path)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TH_06_ToolHashes_MustNotContainPendingMarkers_AndEcmCapabilityRemoved()
    {
        using var toolHashes = JsonDocument.Parse(File.ReadAllText(FindRepoFile("data", "tool-hashes.json")));
        var tools = toolHashes.RootElement.GetProperty("Tools");

        foreach (var tool in tools.EnumerateObject())
        {
            var hash = tool.Value.GetString() ?? string.Empty;
            Assert.DoesNotContain("PENDING-VERIFY", hash, StringComparison.OrdinalIgnoreCase);
        }

        using var conversionRegistry = JsonDocument.Parse(File.ReadAllText(FindRepoFile("data", "conversion-registry.json")));
        var hasUnecmCapability = conversionRegistry.RootElement
            .GetProperty("capabilities")
            .EnumerateArray()
            .Any(capability =>
            {
                if (!capability.TryGetProperty("tool", out var toolElement)
                    || !toolElement.TryGetProperty("toolName", out var toolNameElement))
                {
                    return false;
                }

                return string.Equals(toolNameElement.GetString(), "unecm", StringComparison.OrdinalIgnoreCase);
            });

        Assert.False(hasUnecmCapability, "ECM capability must be removed until an authenticated unecm tool hash is available.");
    }

    [Fact]
    public void ORC_01_ApiAutomationService_MustNotUseUnobservedFireAndForgetTrigger()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Romulus.Api", "ApiAutomationService.cs"));

        Assert.DoesNotContain("_ = TriggerRunAsync(\"watch\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = TriggerRunAsync(\"schedule\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DATA_01_02_11_StartupSchemaValidation_MustCoverExtendedDataFiles()
    {
        var validatorSource = File.ReadAllText(FindRepoFile("src", "Romulus.Infrastructure", "Configuration", "StartupDataSchemaValidator.cs"));

        Assert.Contains("console-maps.json", validatorSource, StringComparison.Ordinal);
        Assert.Contains("format-scores.json", validatorSource, StringComparison.Ordinal);
        Assert.Contains("tool-hashes.json", validatorSource, StringComparison.Ordinal);
        Assert.Contains("ui-lookups.json", validatorSource, StringComparison.Ordinal);

        Assert.True(File.Exists(FindRepoFile("data", "schemas", "format-scores.schema.json")));
        Assert.True(File.Exists(FindRepoFile("data", "schemas", "tool-hashes.schema.json")));
        Assert.True(File.Exists(FindRepoFile("data", "schemas", "ui-lookups.schema.json")));
    }

    [Fact]
    public void I18N_01_03_04_FrenchBrandingAndPhaseDetails_MustBeLocalized()
    {
        using var frDoc = JsonDocument.Parse(File.ReadAllText(FindRepoFile("data", "i18n", "fr.json")));
        var fr = frDoc.RootElement;

        Assert.Contains("Romulus", fr.GetProperty("App.TrayRunning").GetString() ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("ROM Cleanup", fr.GetProperty("App.TrayRunning").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Romulus", fr.GetProperty("Scheduler.EmailSubject").GetString() ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("ROM Cleanup", fr.GetProperty("Scheduler.EmailSubject").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Romulus", fr.GetProperty("Scheduler.WebhookSummary").GetString() ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("ROM Cleanup", fr.GetProperty("Scheduler.WebhookSummary").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var converterSource = File.ReadAllText(FindRepoFile("src", "Romulus.UI.Wpf", "Converters", "Converters.cs"));
        Assert.DoesNotContain("Ausstehend", converterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Abgeschlossen", converterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Konfiguration und Pfade prüfen", converterSource, StringComparison.Ordinal);

        AssertPhaseLocalizationKeysExist("de");
        AssertPhaseLocalizationKeysExist("en");
        AssertPhaseLocalizationKeysExist("fr");
    }

    private static void AssertPhaseLocalizationKeysExist(string locale)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FindRepoFile("data", "i18n", locale + ".json")));
        var root = doc.RootElement;

        for (var phase = 1; phase <= 7; phase++)
        {
            Assert.True(root.TryGetProperty($"Run.PhaseDetail.{phase}", out _),
                $"{locale}.json must define Run.PhaseDetail.{phase}");
        }

        Assert.True(root.TryGetProperty("Run.PhaseStatus.Pending", out _), $"{locale}.json must define Run.PhaseStatus.Pending");
        Assert.True(root.TryGetProperty("Run.PhaseStatus.Active", out _), $"{locale}.json must define Run.PhaseStatus.Active");
        Assert.True(root.TryGetProperty("Run.PhaseStatus.Completed", out _), $"{locale}.json must define Run.PhaseStatus.Completed");
    }

    private static string FindRepoFile(params string[] parts)
    {
        var dataDir = RunEnvironmentBuilder.ResolveDataDir();
        var repoRoot = Directory.GetParent(dataDir)?.FullName
            ?? throw new InvalidOperationException("Repository root could not be resolved from data directory.");
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }
}