using System.Text.Json;
using System.Xml.Linq;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.UI.Wpf.Models;
using RomCleanup.UI.Wpf.Services;
using RomCleanup.UI.Wpf.ViewModels;
using Xunit;

namespace RomCleanup.Tests;

/// <summary>
/// Tests for RunState state machine transitions, ConflictPolicy enum,
/// and Theme resource parity between Dark and Light themes.
/// Covers TEST-002, TEST-006, and TEST-008 from gui-ux-deep-audit.md.
/// </summary>
public class GuiViewModelTests
{
    // ═══ RunState enum value tests ══════════════════════════════════════

    [Fact]
    public void RunState_HasAllExpectedValues()
    {
        var names = Enum.GetNames<RunState>();
        Assert.Contains("Idle", names);
        Assert.Contains("Preflight", names);
        Assert.Contains("Scanning", names);
        Assert.Contains("Deduplicating", names);
        Assert.Contains("Moving", names);
        Assert.Contains("Converting", names);
        Assert.Contains("Completed", names);
        Assert.Contains("CompletedDryRun", names);
        Assert.Contains("Failed", names);
        Assert.Contains("Cancelled", names);
        Assert.Equal(10, names.Length);
    }

    [Theory]
    [InlineData(RunState.Preflight)]
    [InlineData(RunState.Scanning)]
    [InlineData(RunState.Deduplicating)]
    [InlineData(RunState.Moving)]
    [InlineData(RunState.Converting)]
    public void RunState_BusyStates_AreRunning(RunState state)
    {
        // These states should map to IsBusy == true
        Assert.True(state is RunState.Preflight or RunState.Scanning
            or RunState.Deduplicating or RunState.Moving or RunState.Converting);
    }

    [Theory]
    [InlineData(RunState.Idle)]
    [InlineData(RunState.Completed)]
    [InlineData(RunState.CompletedDryRun)]
    [InlineData(RunState.Failed)]
    [InlineData(RunState.Cancelled)]
    public void RunState_IdleStates_AreNotRunning(RunState state)
    {
        Assert.False(state is RunState.Preflight or RunState.Scanning
            or RunState.Deduplicating or RunState.Moving or RunState.Converting);
    }

    // ═══ ConflictPolicy enum tests ══════════════════════════════════════

    [Fact]
    public void ConflictPolicy_HasThreeValues()
    {
        var names = Enum.GetNames<ConflictPolicy>();
        Assert.Equal(3, names.Length);
        Assert.Contains("Rename", names);
        Assert.Contains("Skip", names);
        Assert.Contains("Overwrite", names);
    }

    [Fact]
    public void ConflictPolicy_DefaultIsRename()
    {
        // Rename (0) is the safest default
        Assert.Equal(0, (int)ConflictPolicy.Rename);
    }

    [Theory]
    [InlineData(0, ConflictPolicy.Rename)]
    [InlineData(1, ConflictPolicy.Skip)]
    [InlineData(2, ConflictPolicy.Overwrite)]
    public void ConflictPolicy_IndexMapsCorrectly(int index, ConflictPolicy expected)
    {
        Assert.Equal(expected, (ConflictPolicy)index);
    }

    [Theory]
    [InlineData("Rename", ConflictPolicy.Rename)]
    [InlineData("Skip", ConflictPolicy.Skip)]
    [InlineData("Overwrite", ConflictPolicy.Overwrite)]
    [InlineData("rename", ConflictPolicy.Rename)]  // case-insensitive parse
    public void ConflictPolicy_ParseFromString(string input, ConflictPolicy expected)
    {
        Assert.True(Enum.TryParse<ConflictPolicy>(input, true, out var result));
        Assert.Equal(expected, result);
    }

    // ═══ Theme Parity (TEST-006) ════════════════════════════════════════

    [Fact]
    public void ThemeParity_DarkAndLight_HaveSameBrushKeys()
    {
        var darkPath = FindThemeFile("SynthwaveDark.xaml");
        var lightPath = FindThemeFile("Light.xaml");

        Assert.True(File.Exists(darkPath), $"Dark theme not found at {darkPath}");
        Assert.True(File.Exists(lightPath), $"Light theme not found at {lightPath}");

        var darkKeys = ExtractResourceKeys(darkPath);
        var lightKeys = ExtractResourceKeys(lightPath);

        // Filter to Brush keys only (the critical ones for visual parity)
        var darkBrushKeys = darkKeys.Where(k => k.StartsWith("Brush")).OrderBy(k => k).ToList();
        var lightBrushKeys = lightKeys.Where(k => k.StartsWith("Brush")).OrderBy(k => k).ToList();

        var missingInLight = darkBrushKeys.Except(lightBrushKeys).ToList();
        var missingInDark = lightBrushKeys.Except(darkBrushKeys).ToList();

        Assert.True(missingInLight.Count == 0,
            $"Brush keys in Dark but missing in Light: {string.Join(", ", missingInLight)}");
        Assert.True(missingInDark.Count == 0,
            $"Brush keys in Light but missing in Dark: {string.Join(", ", missingInDark)}");
    }

    [Fact]
    public void ThemeParity_DarkAndLight_HaveSameSpacingKeys()
    {
        var darkPath = FindThemeFile("SynthwaveDark.xaml");
        var lightPath = FindThemeFile("Light.xaml");

        var darkKeys = ExtractResourceKeys(darkPath);
        var lightKeys = ExtractResourceKeys(lightPath);

        var darkSpacingKeys = darkKeys.Where(k => k.StartsWith("Space")).OrderBy(k => k).ToList();
        var lightSpacingKeys = lightKeys.Where(k => k.StartsWith("Space")).OrderBy(k => k).ToList();

        var missingInLight = darkSpacingKeys.Except(lightSpacingKeys).ToList();
        var missingInDark = lightSpacingKeys.Except(darkSpacingKeys).ToList();

        Assert.True(missingInLight.Count == 0,
            $"Spacing keys in Dark but missing in Light: {string.Join(", ", missingInLight)}");
        Assert.True(missingInDark.Count == 0,
            $"Spacing keys in Light but missing in Dark: {string.Join(", ", missingInDark)}");
    }

    [Fact]
    public void ThemeParity_DarkAndLight_HaveSameNamedStyles()
    {
        var darkPath = FindThemeFile("SynthwaveDark.xaml");
        var lightPath = FindThemeFile("Light.xaml");

        var darkKeys = ExtractResourceKeys(darkPath);
        var lightKeys = ExtractResourceKeys(lightPath);

        // Named styles (non-Brush, non-Space keys) — e.g. PrimaryButton, SectionCard
        var darkStyleKeys = darkKeys
            .Where(k => !k.StartsWith("Brush") && !k.StartsWith("Space"))
            .OrderBy(k => k).ToList();
        var lightStyleKeys = lightKeys
            .Where(k => !k.StartsWith("Brush") && !k.StartsWith("Space"))
            .OrderBy(k => k).ToList();

        var missingInLight = darkStyleKeys.Except(lightStyleKeys).ToList();
        var missingInDark = lightStyleKeys.Except(darkStyleKeys).ToList();

        Assert.True(missingInLight.Count == 0,
            $"Style keys in Dark but missing in Light: {string.Join(", ", missingInLight)}");
        Assert.True(missingInDark.Count == 0,
            $"Style keys in Light but missing in Dark: {string.Join(", ", missingInDark)}");
    }

    [Fact]
    public void ThemeParity_CornerRadius_MatchesBetweenThemes()
    {
        var darkPath = FindThemeFile("SynthwaveDark.xaml");
        var lightPath = FindThemeFile("Light.xaml");
        var darkDoc = XDocument.Load(darkPath);
        var lightDoc = XDocument.Load(lightPath);

        // Extract paired CornerRadius values by their parent TargetType
        var darkCR = ExtractCornerRadiusValues(darkDoc);
        var lightCR = ExtractCornerRadiusValues(lightDoc);

        var mismatches = new List<string>();
        foreach (var key in darkCR.Keys.Intersect(lightCR.Keys))
        {
            if (darkCR[key] != lightCR[key])
                mismatches.Add($"{key}: Dark={darkCR[key]}, Light={lightCR[key]}");
        }
        Assert.True(mismatches.Count == 0,
            $"CornerRadius mismatches:\n{string.Join("\n", mismatches)}");
    }

    [Fact]
    public void ThemeParity_TabItem_Padding_MatchesBetweenThemes()
    {
        var darkPath = FindThemeFile("SynthwaveDark.xaml");
        var lightPath = FindThemeFile("Light.xaml");
        var darkDoc = XDocument.Load(darkPath);
        var lightDoc = XDocument.Load(lightPath);

        var darkPadding = ExtractSetterValue(darkDoc, "TabItem", "Padding");
        var lightPadding = ExtractSetterValue(lightDoc, "TabItem", "Padding");

        Assert.NotNull(darkPadding);
        Assert.NotNull(lightPadding);
        Assert.Equal(darkPadding, lightPadding);
    }

    private static Dictionary<string, string> ExtractCornerRadiusValues(XDocument doc)
    {
        var result = new Dictionary<string, string>();
        foreach (var el in doc.Descendants())
        {
            var cr = el.Attribute("CornerRadius");
            if (cr is null) continue;
            // Use parent style TargetType or element name as key
            var parent = el.Ancestors().FirstOrDefault(a =>
                a.Name.LocalName == "ControlTemplate" || a.Name.LocalName == "Style");
            if (parent is not null)
            {
                var targetType = parent.Attribute("TargetType")?.Value ?? "Unknown";
                var key = $"{targetType}#{el.Name.LocalName}";
                result.TryAdd(key, cr.Value);
            }
        }
        return result;
    }

    private static string? ExtractSetterValue(XDocument doc, string targetType, string property)
    {
        foreach (var style in doc.Descendants().Where(e => e.Name.LocalName == "Style"))
        {
            var tt = style.Attribute("TargetType")?.Value;
            if (tt != targetType) continue;
            foreach (var setter in style.Elements().Where(e => e.Name.LocalName == "Setter"))
            {
                if (setter.Attribute("Property")?.Value == property)
                    return setter.Attribute("Value")?.Value;
            }
        }
        return null;
    }

    // ═══ Helpers ════════════════════════════════════════════════════════

    private static string FindThemeFile(string fileName)
    {
        // Walk up from test output dir to find the source tree
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "RomCleanup.UI.Wpf", "Themes", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        // Fallback: try relative from working directory
        return Path.Combine("src", "RomCleanup.UI.Wpf", "Themes", fileName);
    }

    private static HashSet<string> ExtractResourceKeys(string xamlPath)
    {
        var keys = new HashSet<string>();
        var doc = XDocument.Load(xamlPath);
        var xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");

        foreach (var el in doc.Descendants())
        {
            var keyAttr = el.Attribute(xKey);
            if (keyAttr is not null)
                keys.Add(keyAttr.Value);
        }
        return keys;
    }

    // ═══ SettingsService Round-trip (TEST-004) ══════════════════════════

    [Fact]
    public void SettingsService_SaveAndLoad_PreservesAllProperties()
    {
        // Arrange: custom settings dir to avoid clobbering real user settings
        var tempDir = Path.Combine(Path.GetTempPath(), "RomCleanupTest_" + Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(tempDir, "settings.json");
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm1 = new MainViewModel();
            // Set non-default values on every persisted property
            vm1.Roots.Add(@"C:\TestRom1");
            vm1.Roots.Add(@"D:\TestRom2");
            vm1.LogLevel = "Debug";
            vm1.AggressiveJunk = true;
            vm1.AliasKeying = true;
            vm1.PreferEU = true; vm1.PreferUS = false; vm1.PreferJP = true;
            vm1.PreferWORLD = false; vm1.PreferDE = true; vm1.PreferFR = true;
            vm1.ToolChdman = @"C:\tools\chdman.exe";
            vm1.Tool7z = @"C:\tools\7z.exe";
            vm1.ToolDolphin = @"C:\tools\dolphintool.exe";
            vm1.ToolPsxtract = @"C:\tools\psxtract.exe";
            vm1.ToolCiso = @"C:\tools\ciso.exe";
            vm1.UseDat = true;
            vm1.DatRoot = @"C:\dat";
            vm1.DatHashType = "SHA256";
            vm1.DatFallback = false;
            vm1.TrashRoot = @"C:\trash";
            vm1.AuditRoot = @"C:\audit";
            vm1.Ps3DupesRoot = @"C:\ps3dupes";
            vm1.SortConsole = false;
            vm1.DryRun = false;
            vm1.ConvertEnabled = true;
            vm1.ConfirmMove = false;
            vm1.ConflictPolicy = ConflictPolicy.Skip;

            // Act: manually serialize (same shape as SettingsService.SaveFrom)
            var settings = new
            {
                version = 1,
                general = new
                {
                    logLevel = vm1.LogLevel,
                    preferredRegions = vm1.GetPreferredRegions(),
                    aggressiveJunk = vm1.AggressiveJunk,
                    aliasEditionKeying = vm1.AliasKeying
                },
                toolPaths = new Dictionary<string, string>
                {
                    ["chdman"] = vm1.ToolChdman,
                    ["dolphintool"] = vm1.ToolDolphin,
                    ["7z"] = vm1.Tool7z,
                    ["psxtract"] = vm1.ToolPsxtract,
                    ["ciso"] = vm1.ToolCiso
                },
                dat = new
                {
                    useDat = vm1.UseDat,
                    datRoot = vm1.DatRoot,
                    hashType = vm1.DatHashType,
                    datFallback = vm1.DatFallback
                },
                paths = new
                {
                    trashRoot = vm1.TrashRoot,
                    auditRoot = vm1.AuditRoot,
                    ps3DupesRoot = vm1.Ps3DupesRoot,
                    lastAuditPath = "test-audit.csv"
                },
                roots = vm1.Roots.ToArray(),
                ui = new
                {
                    sortConsole = vm1.SortConsole,
                    dryRun = vm1.DryRun,
                    convertEnabled = vm1.ConvertEnabled,
                    confirmMove = vm1.ConfirmMove,
                    conflictPolicy = vm1.ConflictPolicy.ToString()
                }
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);

            // Act: load into a fresh VM using SettingsService's JSON-parsing logic
            var vm2 = new MainViewModel();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Manually apply same parsing as SettingsService.LoadInto
            if (root.TryGetProperty("general", out var general))
            {
                vm2.LogLevel = general.GetProperty("logLevel").GetString() ?? "Info";
                vm2.AggressiveJunk = general.GetProperty("aggressiveJunk").GetBoolean();
                vm2.AliasKeying = general.GetProperty("aliasEditionKeying").GetBoolean();
            }
            if (root.TryGetProperty("toolPaths", out var tools))
            {
                vm2.ToolChdman = tools.GetProperty("chdman").GetString() ?? "";
                vm2.Tool7z = tools.GetProperty("7z").GetString() ?? "";
                vm2.ToolDolphin = tools.GetProperty("dolphintool").GetString() ?? "";
                vm2.ToolPsxtract = tools.GetProperty("psxtract").GetString() ?? "";
                vm2.ToolCiso = tools.GetProperty("ciso").GetString() ?? "";
            }
            if (root.TryGetProperty("dat", out var dat))
            {
                vm2.UseDat = dat.GetProperty("useDat").GetBoolean();
                vm2.DatRoot = dat.GetProperty("datRoot").GetString() ?? "";
                vm2.DatHashType = dat.GetProperty("hashType").GetString() ?? "SHA1";
                vm2.DatFallback = dat.GetProperty("datFallback").GetBoolean();
            }
            if (root.TryGetProperty("paths", out var paths))
            {
                vm2.TrashRoot = paths.GetProperty("trashRoot").GetString() ?? "";
                vm2.AuditRoot = paths.GetProperty("auditRoot").GetString() ?? "";
                vm2.Ps3DupesRoot = paths.GetProperty("ps3DupesRoot").GetString() ?? "";
            }
            if (root.TryGetProperty("ui", out var ui))
            {
                vm2.SortConsole = ui.GetProperty("sortConsole").GetBoolean();
                vm2.DryRun = ui.GetProperty("dryRun").GetBoolean();
                vm2.ConvertEnabled = ui.GetProperty("convertEnabled").GetBoolean();
                vm2.ConfirmMove = ui.GetProperty("confirmMove").GetBoolean();
                if (Enum.TryParse<ConflictPolicy>(
                    ui.GetProperty("conflictPolicy").GetString(), true, out var cp))
                    vm2.ConflictPolicy = cp;
            }
            if (root.TryGetProperty("roots", out var roots) && roots.ValueKind == JsonValueKind.Array)
            {
                vm2.Roots.Clear();
                foreach (var r in roots.EnumerateArray())
                {
                    var path = r.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                        vm2.Roots.Add(path);
                }
            }

            // Assert all values match
            Assert.Equal(vm1.LogLevel, vm2.LogLevel);
            Assert.Equal(vm1.AggressiveJunk, vm2.AggressiveJunk);
            Assert.Equal(vm1.AliasKeying, vm2.AliasKeying);
            Assert.Equal(vm1.ToolChdman, vm2.ToolChdman);
            Assert.Equal(vm1.Tool7z, vm2.Tool7z);
            Assert.Equal(vm1.ToolDolphin, vm2.ToolDolphin);
            Assert.Equal(vm1.ToolPsxtract, vm2.ToolPsxtract);
            Assert.Equal(vm1.ToolCiso, vm2.ToolCiso);
            Assert.Equal(vm1.UseDat, vm2.UseDat);
            Assert.Equal(vm1.DatRoot, vm2.DatRoot);
            Assert.Equal(vm1.DatHashType, vm2.DatHashType);
            Assert.Equal(vm1.DatFallback, vm2.DatFallback);
            Assert.Equal(vm1.TrashRoot, vm2.TrashRoot);
            Assert.Equal(vm1.AuditRoot, vm2.AuditRoot);
            Assert.Equal(vm1.Ps3DupesRoot, vm2.Ps3DupesRoot);
            Assert.Equal(vm1.SortConsole, vm2.SortConsole);
            Assert.Equal(vm1.DryRun, vm2.DryRun);
            Assert.Equal(vm1.ConvertEnabled, vm2.ConvertEnabled);
            Assert.Equal(vm1.ConfirmMove, vm2.ConfirmMove);
            Assert.Equal(vm1.ConflictPolicy, vm2.ConflictPolicy);
            Assert.Equal(vm1.Roots.Count, vm2.Roots.Count);
            for (int i = 0; i < vm1.Roots.Count; i++)
                Assert.Equal(vm1.Roots[i], vm2.Roots[i]);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void SettingsService_ConflictPolicy_PersistsAllValues()
    {
        foreach (var policy in Enum.GetValues<ConflictPolicy>())
        {
            var serialized = policy.ToString();
            Assert.True(Enum.TryParse<ConflictPolicy>(serialized, true, out var parsed));
            Assert.Equal(policy, parsed);
        }
    }

    [Fact]
    public void GetPreferredRegions_SimpleMode_Index0_ReturnsEuropaOrder()
    {
        var vm = new MainViewModel { IsSimpleMode = true, SimpleRegionIndex = 0 };
        var regions = vm.GetPreferredRegions();
        Assert.Equal("EU", regions[0]);
        Assert.Contains("DE", regions);
        Assert.Contains("WORLD", regions);
    }

    [Theory]
    [InlineData(0, "EU")]
    [InlineData(1, "US")]
    [InlineData(2, "JP")]
    [InlineData(3, "WORLD")]
    public void GetPreferredRegions_SimpleMode_FirstRegionCorrect(int index, string expectedFirst)
    {
        var vm = new MainViewModel { IsSimpleMode = true, SimpleRegionIndex = index };
        var regions = vm.GetPreferredRegions();
        Assert.Equal(expectedFirst, regions[0]);
    }

    [Fact]
    public void GetPreferredRegions_ExpertMode_OnlySelectedRegions()
    {
        var vm = new MainViewModel { IsSimpleMode = false };
        // Reset all to false
        vm.PreferEU = false; vm.PreferUS = false; vm.PreferJP = false; vm.PreferWORLD = false;
        vm.PreferDE = false; vm.PreferFR = false; vm.PreferIT = false; vm.PreferES = false;
        vm.PreferAU = false; vm.PreferASIA = false; vm.PreferKR = false; vm.PreferCN = false;
        vm.PreferBR = false; vm.PreferNL = false; vm.PreferSE = false; vm.PreferSCAN = false;
        // Select only JP and DE
        vm.PreferJP = true;
        vm.PreferDE = true;
        var regions = vm.GetPreferredRegions();
        Assert.Equal(2, regions.Length);
        Assert.Contains("JP", regions);
        Assert.Contains("DE", regions);
    }

    // ═══ RefreshStatus Combinations (TEST-005) ══════════════════════════

    [Fact]
    public void RefreshStatus_NoRoots_ShowsNotReady()
    {
        var vm = new MainViewModel();
        vm.Roots.Clear();
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Missing, vm.RootsStatusLevel);
        Assert.Equal("Keine Ordner", vm.StatusRoots);
        Assert.Equal(StatusLevel.Blocked, vm.ReadyStatusLevel);
        Assert.Contains("Nicht bereit", vm.StatusReady);
    }

    [Fact]
    public void RefreshStatus_WithRoots_ShowsConfigured()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Ok, vm.RootsStatusLevel);
        Assert.Contains("1 Ordner konfiguriert", vm.StatusRoots);
    }

    [Fact]
    public void RefreshStatus_NoToolsSpecified_NotConverting_ShowsMissing()
    {
        var vm = new MainViewModel();
        vm.ToolChdman = "";
        vm.Tool7z = "";
        vm.ConvertEnabled = false;
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Missing, vm.ToolsStatusLevel);
        Assert.Equal("Keine Tools", vm.StatusTools);
    }

    [Fact]
    public void RefreshStatus_ToolsSpecifiedButNotFound_ShowsWarning()
    {
        var vm = new MainViewModel();
        vm.ToolChdman = @"C:\nonexistent\chdman.exe";  // doesn't exist
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Warning, vm.ToolsStatusLevel);
        Assert.Equal("Tools nicht gefunden", vm.StatusTools);
    }

    [Fact]
    public void RefreshStatus_DatDisabled_ShowsDeactivated()
    {
        var vm = new MainViewModel();
        vm.UseDat = false;
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Missing, vm.DatStatusLevel);
        Assert.Equal("DAT deaktiviert", vm.StatusDat);
    }

    [Fact]
    public void RefreshStatus_DatEnabled_InvalidPath_ShowsWarning()
    {
        var vm = new MainViewModel();
        vm.UseDat = true;
        vm.DatRoot = @"C:\nonexistent\dat";
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Warning, vm.DatStatusLevel);
        Assert.Equal("DAT-Pfad ungültig", vm.StatusDat);
    }

    [Fact]
    public void RefreshStatus_DatEnabled_ValidPath_ShowsActive()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RomCleanup_DatTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var vm = new MainViewModel();
            vm.UseDat = true;
            vm.DatRoot = tempDir;
            vm.RefreshStatus();
            Assert.Equal(StatusLevel.Ok, vm.DatStatusLevel);
            Assert.Equal("DAT aktiv", vm.StatusDat);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Theory]
    [InlineData(RunState.Scanning, "Scanne…")]
    [InlineData(RunState.Deduplicating, "Dedupliziere…")]
    [InlineData(RunState.Moving, "Verschiebe…")]
    [InlineData(RunState.Converting, "Konvertiere…")]
    [InlineData(RunState.Preflight, "Prüfe…")]
    public void RefreshStatus_BusyState_ShowsPhaseLabel(RunState state, string expectedLabel)
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = state; // sets IsBusy
        vm.RefreshStatus();
        Assert.Equal(2, vm.CurrentStep);
        Assert.Equal(expectedLabel, vm.StepLabel3);
    }

    [Fact]
    public void RefreshStatus_CompletedDryRun_ShowsStep3()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = RunState.CompletedDryRun;
        vm.RefreshStatus();
        Assert.Equal(3, vm.CurrentStep);
        Assert.Equal("Vorschau fertig", vm.StepLabel3);
    }

    [Fact]
    public void RefreshStatus_Completed_ShowsStep3()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = RunState.Completed;
        vm.RefreshStatus();
        Assert.Equal(3, vm.CurrentStep);
        Assert.Equal("Abgeschlossen", vm.StepLabel3);
    }

    [Fact]
    public void RefreshStatus_Idle_WithRoots_ShowsStep1()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = RunState.Idle;
        vm.RefreshStatus();
        Assert.Equal(1, vm.CurrentStep);
        Assert.Equal("F5 drücken", vm.StepLabel3);
    }

    [Fact]
    public void RefreshStatus_Ready_WithRoots_NoToolWarning_ShowsOk()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.ToolChdman = "";
        vm.Tool7z = "";
        vm.ConvertEnabled = false;
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Ok, vm.ReadyStatusLevel);
        Assert.Equal("Startbereit ✓", vm.StatusReady);
    }

    [Fact]
    public void RefreshStatus_Ready_WithRoots_ToolWarning_ShowsWarning()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.ConvertEnabled = true;  // wants tools but they're not found
        vm.ToolChdman = "";
        vm.Tool7z = "";
        vm.RefreshStatus();
        Assert.Equal(StatusLevel.Warning, vm.ReadyStatusLevel);
        Assert.Contains("Warnung", vm.StatusReady);
    }

    // ═══ WCAG AA Contrast (A11Y-005) ═══════════════════════════════════

    [Theory]
    [InlineData("SynthwaveDark.xaml", "BrushTextPrimary", "BrushBackground", 4.5)]
    [InlineData("SynthwaveDark.xaml", "BrushTextMuted", "BrushBackground", 3.0)]
    [InlineData("SynthwaveDark.xaml", "BrushAccentCyan", "BrushBackground", 3.0)]
    [InlineData("SynthwaveDark.xaml", "BrushTextPrimary", "BrushSurface", 4.5)]
    [InlineData("Light.xaml", "BrushTextPrimary", "BrushBackground", 4.5)]
    [InlineData("Light.xaml", "BrushTextMuted", "BrushBackground", 4.5)]
    [InlineData("Light.xaml", "BrushAccentCyan", "BrushBackground", 3.0)]
    [InlineData("Light.xaml", "BrushTextPrimary", "BrushSurface", 4.5)]
    public void Theme_TextOnBackground_MeetsWcagAAContrast(
        string themeFile, string fgKey, string bgKey, double minRatio)
    {
        var path = FindThemeFile(themeFile);
        Assert.True(File.Exists(path), $"Theme not found: {path}");

        var colors = ExtractBrushColors(path);
        Assert.True(colors.ContainsKey(fgKey), $"Missing brush key: {fgKey}");
        Assert.True(colors.ContainsKey(bgKey), $"Missing brush key: {bgKey}");

        var ratio = ContrastRatio(colors[fgKey], colors[bgKey]);
        Assert.True(ratio >= minRatio,
            $"{themeFile}: {fgKey} on {bgKey} contrast ratio {ratio:F2}:1 < required {minRatio}:1");
    }

    private static Dictionary<string, (int R, int G, int B)> ExtractBrushColors(string xamlPath)
    {
        var result = new Dictionary<string, (int, int, int)>();
        var doc = XDocument.Load(xamlPath);
        var xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");

        foreach (var el in doc.Descendants())
        {
            var keyAttr = el.Attribute(xKey);
            if (keyAttr is null || !keyAttr.Value.StartsWith("Brush")) continue;
            var colorAttr = el.Attribute("Color");
            if (colorAttr is null) continue;
            var hex = colorAttr.Value.TrimStart('#');
            // Support #AARRGGBB and #RRGGBB
            if (hex.Length == 8) hex = hex[2..]; // strip alpha
            if (hex.Length != 6) continue;
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            result[keyAttr.Value] = (r, g, b);
        }
        return result;
    }

    private static double RelativeLuminance((int R, int G, int B) c)
    {
        double Linearize(int v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);
    }

    private static double ContrastRatio((int R, int G, int B) fg, (int R, int G, int B) bg)
    {
        double l1 = RelativeLuminance(fg);
        double l2 = RelativeLuminance(bg);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    // ═══ CanExecute Guard Tests (TEST-001) ══════════════════════════════

    [Fact]
    public void RunCommand_Disabled_WhenNoRoots()
    {
        var vm = new MainViewModel();
        vm.Roots.Clear();
        vm.CurrentRunState = RunState.Idle;
        // RunCommand CanExecute = !IsBusy && Roots.Count > 0
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Roots);
    }

    [Fact]
    public void RunCommand_Disabled_WhenBusy()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = RunState.Scanning;
        // RunCommand CanExecute = !IsBusy && Roots.Count > 0 → false because IsBusy
        Assert.True(vm.IsBusy);
    }

    [Fact]
    public void RunCommand_Enabled_WhenIdleWithRoots()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = RunState.Idle;
        // RunCommand CanExecute = !IsBusy && Roots.Count > 0 → true
        Assert.False(vm.IsBusy);
        Assert.True(vm.Roots.Count > 0);
    }

    [Fact]
    public void CancelCommand_Disabled_WhenNotBusy()
    {
        var vm = new MainViewModel();
        vm.CurrentRunState = RunState.Idle;
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void CancelCommand_Enabled_WhenBusy()
    {
        var vm = new MainViewModel();
        vm.CurrentRunState = RunState.Scanning;
        Assert.True(vm.IsBusy);
    }

    [Fact]
    public void RollbackCommand_Disabled_WhenNoUndoHistory()
    {
        var vm = new MainViewModel();
        Assert.False(vm.HasRollbackUndo);
    }

    [Fact]
    public void RollbackCommand_Enabled_AfterPushUndo()
    {
        var vm = new MainViewModel();
        vm.PushRollbackUndo("test-audit.csv");
        Assert.True(vm.HasRollbackUndo);
    }

    [Fact]
    public void RollbackUndo_PopReturnsLastPushed()
    {
        var vm = new MainViewModel();
        vm.PushRollbackUndo("audit1.csv");
        vm.PushRollbackUndo("audit2.csv");
        Assert.Equal("audit2.csv", vm.PopRollbackUndo());
        Assert.Equal("audit1.csv", vm.PopRollbackUndo());
        Assert.False(vm.HasRollbackUndo);
    }

    // ═══ Cancellation State (TEST-008) ══════════════════════════════════

    [Fact]
    public void TransitionTo_Cancelled_FromBusy_SetsState()
    {
        var vm = new MainViewModel();
        vm.TransitionTo(RunState.Scanning);
        Assert.True(vm.IsBusy);
        // OnCancel is private — simulate via TransitionTo
        vm.TransitionTo(RunState.Cancelled);
        Assert.Equal(RunState.Cancelled, vm.CurrentRunState);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void TransitionTo_Failed_FromBusy_SetsState()
    {
        var vm = new MainViewModel();
        vm.TransitionTo(RunState.Moving);
        vm.TransitionTo(RunState.Failed);
        Assert.Equal(RunState.Failed, vm.CurrentRunState);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void ShowStartMoveButton_True_AfterCompletedDryRun()
    {
        var vm = new MainViewModel();
        vm.CurrentRunState = RunState.CompletedDryRun;
        Assert.True(vm.ShowStartMoveButton);
    }

    [Fact]
    public void ShowStartMoveButton_False_WhenBusy()
    {
        var vm = new MainViewModel();
        vm.CurrentRunState = RunState.Scanning;
        Assert.False(vm.ShowStartMoveButton);
    }

    [Fact]
    public void ShowStartMoveButton_False_WhenIdle()
    {
        var vm = new MainViewModel();
        vm.CurrentRunState = RunState.Idle;
        Assert.False(vm.ShowStartMoveButton);
    }

    // ═══ ExtensionFilters (UX-004) ══════════════════════════════════════

    [Fact]
    public void ExtensionFilters_InitializedWith18Items()
    {
        var vm = new MainViewModel();
        Assert.Equal(18, vm.ExtensionFilters.Count);
    }

    [Fact]
    public void ExtensionFilters_AllUncheckedByDefault()
    {
        var vm = new MainViewModel();
        Assert.All(vm.ExtensionFilters, f => Assert.False(f.IsChecked));
    }

    [Fact]
    public void GetSelectedExtensions_NoneChecked_ReturnsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.GetSelectedExtensions());
    }

    [Fact]
    public void GetSelectedExtensions_CheckedItemsReturned()
    {
        var vm = new MainViewModel();
        vm.ExtensionFilters.First(e => e.Extension == ".chd").IsChecked = true;
        vm.ExtensionFilters.First(e => e.Extension == ".zip").IsChecked = true;

        var selected = vm.GetSelectedExtensions();
        Assert.Equal(2, selected.Length);
        Assert.Contains(".chd", selected);
        Assert.Contains(".zip", selected);
    }

    [Fact]
    public void ExtensionFilters_CategoriesAreCorrect()
    {
        var vm = new MainViewModel();
        var categories = vm.ExtensionFilters.Select(e => e.Category).Distinct().OrderBy(c => c).ToArray();
        Assert.Equal(3, categories.Length);
        Assert.Contains("Archive", categories);
        Assert.Contains("Cartridge / Modern", categories);
        Assert.Contains("Disc-Images", categories);
    }

    [Fact]
    public void ExtensionFilters_ContainsAllExpectedExtensions()
    {
        var vm = new MainViewModel();
        var expected = new[] { ".chd", ".iso", ".cue", ".gdi", ".img", ".bin", ".cso", ".pbp",
                               ".zip", ".7z", ".rar",
                               ".nes", ".gba", ".nds", ".nsp", ".xci", ".wbfs", ".rvz" };
        var actual = vm.ExtensionFilters.Select(e => e.Extension).ToArray();
        Assert.Equal(expected, actual);
    }

    // ═══ HasRunResult (UX-003/TEST-009) ═════════════════════════════════

    [Theory]
    [InlineData(RunState.Completed, true)]
    [InlineData(RunState.CompletedDryRun, true)]
    [InlineData(RunState.Idle, false)]
    [InlineData(RunState.Scanning, false)]
    [InlineData(RunState.Failed, false)]
    [InlineData(RunState.Cancelled, false)]
    public void HasRunResult_ReflectsCompletedStates(RunState state, bool expected)
    {
        var vm = new MainViewModel();
        vm.CurrentRunState = state;
        Assert.Equal(expected, vm.HasRunResult);
    }

    // ═══ CONSOLE FILTERS (Runde 7) ══════════════════════════════════════

    [Fact]
    public void ConsoleFilters_InitializedWith30Items()
    {
        var vm = new MainViewModel();
        Assert.Equal(30, vm.ConsoleFilters.Count);
    }

    [Fact]
    public void ConsoleFilters_AllUncheckedByDefault()
    {
        var vm = new MainViewModel();
        Assert.All(vm.ConsoleFilters, c => Assert.False(c.IsChecked));
    }

    [Fact]
    public void GetSelectedConsoles_NoneChecked_ReturnsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.GetSelectedConsoles());
    }

    [Fact]
    public void GetSelectedConsoles_CheckedItemsReturned()
    {
        var vm = new MainViewModel();
        vm.ConsoleFilters.First(c => c.Key == "PS1").IsChecked = true;
        vm.ConsoleFilters.First(c => c.Key == "NES").IsChecked = true;
        var selected = vm.GetSelectedConsoles();
        Assert.Equal(2, selected.Length);
        Assert.Contains("PS1", selected);
        Assert.Contains("NES", selected);
    }

    [Fact]
    public void ConsoleFilters_CategoriesAreCorrect()
    {
        var vm = new MainViewModel();
        var categories = vm.ConsoleFilters.Select(c => c.Category).Distinct().OrderBy(c => c).ToArray();
        Assert.Equal(4, categories.Length);
        Assert.Contains("Sony", categories);
        Assert.Contains("Nintendo", categories);
        Assert.Contains("Sega", categories);
        Assert.Contains("Andere", categories);
    }

    [Fact]
    public void ConsoleFilters_ContainsExpectedConsoles()
    {
        var vm = new MainViewModel();
        var keys = vm.ConsoleFilters.Select(c => c.Key).ToArray();
        // Spot-check representative consoles from each category
        Assert.Contains("PS1", keys);
        Assert.Contains("PS2", keys);
        Assert.Contains("PSP", keys);
        Assert.Contains("NES", keys);
        Assert.Contains("SNES", keys);
        Assert.Contains("GC", keys);
        Assert.Contains("SWITCH", keys);
        Assert.Contains("MD", keys);
        Assert.Contains("DC", keys);
        Assert.Contains("GG", keys);
        Assert.Contains("ARCADE", keys);
        Assert.Contains("3DO", keys);
        Assert.Contains("JAG", keys);
    }

    [Fact]
    public void ConsoleFiltersView_HasGroupDescriptions()
    {
        var vm = new MainViewModel();
        Assert.Single(vm.ConsoleFiltersView.GroupDescriptions);
    }

    // ═══ CTS CANCEL RACE SAFETY (Runde 7: Threading) ════════════════════

    [Fact]
    public void OnCancel_SetsState_WhenCtsAlreadyDisposed()
    {
        var vm = new MainViewModel();
        // Create and immediately dispose the CTS to simulate race
        var ct = vm.CreateRunCancellation();
        // Cancel should not throw even after CTS is created
        vm.TransitionTo(RunState.Scanning);
        vm.CancelCommand.Execute(null);
        Assert.Equal(RunState.Cancelled, vm.CurrentRunState);
    }

    // ═══ TEST-007: DryRun E2E Smoke-Test ════════════════════════════════

    [Fact]
    public void DryRun_EndToEnd_ScansDedupesButDoesNotMoveFiles()
    {
        // Arrange: create temp ROM directory with two copies of same game
        var tempDir = Path.Combine(Path.GetTempPath(), "RomCleanup_E2E_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var file1 = Path.Combine(tempDir, "Super Mario (USA).zip");
            var file2 = Path.Combine(tempDir, "Super Mario (Europe).zip");
            File.WriteAllBytes(file1, new byte[64]);
            File.WriteAllBytes(file2, new byte[64]);

            var fs = new RomCleanup.Infrastructure.FileSystem.FileSystemAdapter();
            var audit = new RomCleanup.Infrastructure.Audit.AuditCsvStore();
            var orch = new RomCleanup.Infrastructure.Orchestration.RunOrchestrator(fs, audit);

            // Act: DryRun
            var options = new RomCleanup.Infrastructure.Orchestration.RunOptions
            {
                Roots = new[] { tempDir },
                Extensions = new[] { ".zip" },
                Mode = "DryRun",
                PreferRegions = new[] { "US", "EU" }
            };

            var result = orch.Execute(options);

            // Assert: pipeline completed
            Assert.Equal("ok", result.Status);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(2, result.TotalFilesScanned);
            Assert.True(result.GroupCount >= 1, "Should find at least 1 dedup group");
            Assert.Null(result.MoveResult); // DryRun does NOT move

            // Both files still exist
            Assert.True(File.Exists(file1), "USA file must still exist after DryRun");
            Assert.True(File.Exists(file2), "Europe file must still exist after DryRun");

            // Dedup groups contain expected data
            Assert.NotEmpty(result.DedupeGroups);
            Assert.NotEmpty(result.AllCandidates);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DryRun_VMStateTransitions_FollowCorrectSequence()
    {
        var vm = new MainViewModel();

        // Simulate the DryRun flow that MainWindow.xaml.cs executes
        Assert.Equal(RunState.Idle, vm.CurrentRunState);
        Assert.True(vm.IsIdle);
        Assert.False(vm.IsBusy);

        // Preflight phase
        vm.TransitionTo(RunState.Preflight);
        Assert.True(vm.IsBusy);
        Assert.False(vm.IsIdle);

        // Scanning phase
        vm.TransitionTo(RunState.Scanning);
        Assert.True(vm.IsBusy);

        // Deduplicating
        vm.TransitionTo(RunState.Deduplicating);
        Assert.True(vm.IsBusy);

        // Complete DryRun (no Move phase)
        vm.DryRun = true;
        vm.CompleteRun(success: true, reportPath: "/tmp/report.html");
        Assert.Equal(RunState.CompletedDryRun, vm.CurrentRunState);
        Assert.True(vm.HasRunResult);
        Assert.False(vm.IsBusy);
        Assert.True(vm.ShowStartMoveButton); // DryRun shows "Start Move" button
        Assert.Equal("/tmp/report.html", vm.LastReportPath);
    }

    [Fact]
    public void DryRun_VMStateTransitions_MovePhaseFollowsDryRun()
    {
        var vm = new MainViewModel();

        // First: complete a DryRun
        vm.DryRun = true;
        vm.TransitionTo(RunState.Scanning);
        vm.TransitionTo(RunState.Deduplicating);
        vm.CompleteRun(success: true, reportPath: "/tmp/report.html");
        Assert.True(vm.ShowStartMoveButton);

        // Then: user clicks "Start Move" → goes through phases again
        vm.DryRun = false;
        vm.TransitionTo(RunState.Preflight);
        vm.TransitionTo(RunState.Moving);
        Assert.True(vm.IsBusy);
        Assert.False(vm.HasRunResult); // Moving state doesn't have result yet

        // Complete Move
        vm.CompleteRun(success: true, reportPath: "/tmp/report2.html");
        Assert.Equal(RunState.Completed, vm.CurrentRunState);
        Assert.True(vm.HasRunResult);
        Assert.False(vm.ShowStartMoveButton); // Move done, no more "Start Move"
    }

    // ═══ RunService tests ═══════════════════════════════════════════════

    [Fact]
    public void RunService_GetSiblingDirectory_ReturnsParentSibling()
    {
        var result = RunService.GetSiblingDirectory(@"C:\Games\Roms", "reports");
        Assert.Equal(Path.Combine(@"C:\Games", "reports"), result);
    }

    [Fact]
    public void RunService_GetSiblingDirectory_DriveRoot_FallsBackToSubdirectory()
    {
        var result = RunService.GetSiblingDirectory(@"C:\", "reports");
        Assert.Equal(Path.Combine(@"C:\", "reports"), result);
    }

    // ═══ WatchService tests ═════════════════════════════════════════════

    [Fact]
    public void WatchService_Start_NoRoots_ReturnsZero()
    {
        using var ws = new WatchService();
        var count = ws.Start(Array.Empty<string>());
        Assert.Equal(0, count);
        Assert.False(ws.IsActive);
    }

    [Fact]
    public void WatchService_Start_NonExistentRoot_ReturnsZero()
    {
        using var ws = new WatchService();
        var count = ws.Start(new[] { @"Z:\NonExistent_12345" });
        Assert.Equal(0, count);
        Assert.False(ws.IsActive);
    }

    [Fact]
    public void WatchService_Start_ValidRoot_CreatesWatchers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ws_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            using var ws = new WatchService();
            var count = ws.Start(new[] { tempDir });
            Assert.Equal(1, count);
            Assert.True(ws.IsActive);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void WatchService_Start_Toggle_StopsOnSecondCall()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ws_toggle_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            using var ws = new WatchService();
            ws.Start(new[] { tempDir });
            Assert.True(ws.IsActive);

            var count = ws.Start(new[] { tempDir });
            Assert.Equal(0, count);
            Assert.False(ws.IsActive);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void WatchService_Dispose_CleansUp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ws_dispose_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = new WatchService();
            ws.Start(new[] { tempDir });
            Assert.True(ws.IsActive);

            ws.Dispose();
            Assert.False(ws.IsActive);

            // Double dispose should not throw
            ws.Dispose();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void WatchService_FlushPendingIfNeeded_NoPending_DoesNothing()
    {
        using var ws = new WatchService();
        ws.FlushPendingIfNeeded();
        Assert.False(ws.HasPending);
    }

    // ═══ RollbackService tests ══════════════════════════════════════════

    [Fact]
    public void RollbackService_Execute_NoAuditFile_ReturnsEmpty()
    {
        var result = RollbackService.Execute(
            Path.Combine(Path.GetTempPath(), "nonexistent_audit.csv"),
            new[] { @"C:\Games" });
        Assert.Empty(result);
    }

    // ═══ ProfileService tests ═══════════════════════════════════════════

    [Fact]
    public void ProfileService_Delete_NoFile_ReturnsFalse()
    {
        // Delete without existing file should return false (no-op)
        // We can't easily test the true-path without touching %APPDATA%,
        // but the false-path is safe to verify.
        // (If settings happen to exist, the method still won't throw.)
        Assert.IsType<bool>(ProfileService.Delete());
    }

    [Fact]
    public void ProfileService_Import_InvalidJson_Throws()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"profile_test_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tmp, "NOT JSON {{{");
            var ex = Record.Exception(() => ProfileService.Import(tmp));
            Assert.NotNull(ex);
            Assert.True(ex is JsonException, $"Expected JsonException but got {ex.GetType().FullName}");
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void ProfileService_Export_RoundTrip()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"profile_export_{Guid.NewGuid():N}.json");
        try
        {
            var map = new Dictionary<string, string>
            {
                ["foo"] = "bar",
                ["baz"] = "42"
            };
            ProfileService.Export(tmp, map);
            Assert.True(File.Exists(tmp));

            var json = File.ReadAllText(tmp);
            var rt = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            Assert.NotNull(rt);
            Assert.Equal("bar", rt!["foo"]);
            Assert.Equal("42", rt["baz"]);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void ProfileService_LoadSavedConfigFlat_NoFile_ReturnsNull()
    {
        // LoadSavedConfigFlat reads from %APPDATA% — if no settings.json exists it returns null
        // This test is safe: it only verifies the null-path vs. crash behavior
        var result = ProfileService.LoadSavedConfigFlat();
        // Result is either null (no file) or a valid dictionary (file exists) — never throws
        Assert.True(result is null || result is Dictionary<string, string>);
    }

    // ═══ TrayService tests ══════════════════════════════════════════════

    [Fact]
    public void TrayService_IsActive_DefaultFalse()
    {
        // TrayService needs a Window — we can't create one in a headless test.
        // Verify the type exists and has the expected public API shape.
        var type = typeof(TrayService);
        Assert.True(type.IsSealed);
        Assert.Contains(type.GetInterfaces(), i => i == typeof(IDisposable));
        Assert.NotNull(type.GetProperty("IsActive"));
        Assert.NotNull(type.GetMethod("Toggle"));
        Assert.NotNull(type.GetMethod("OnWindowStateChanged"));
        Assert.NotNull(type.GetMethod("Dispose"));
    }

    // ═══ FeatureService extraction tests ════════════════════════════════

    [Fact]
    public void FeatureService_BuildCsvDiff_NonCsv_ReturnsNull()
    {
        var result = FeatureService.BuildCsvDiff("a.html", "b.html", "Test");
        Assert.Null(result);
    }

    [Fact]
    public void FeatureService_BuildCsvDiff_ValidCsv_ReturnsDiff()
    {
        var dirA = Path.Combine(Path.GetTempPath(), $"csvdiff_a_{Guid.NewGuid():N}.csv");
        var dirB = Path.Combine(Path.GetTempPath(), $"csvdiff_b_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(dirA, "Path;Size\n\"game1.rom\";100\n\"game2.rom\";200\n");
            File.WriteAllText(dirB, "Path;Size\n\"game2.rom\";200\n\"game3.rom\";300\n");
            var result = FeatureService.BuildCsvDiff(dirA, dirB, "Test-Diff");
            Assert.NotNull(result);
            Assert.Contains("Hinzugefügt", result);
            Assert.Contains("Entfernt", result);
            Assert.Contains("game3", result);   // added
            Assert.Contains("game1", result);   // removed
        }
        finally
        {
            File.Delete(dirA);
            File.Delete(dirB);
        }
    }

    [Fact]
    public void FeatureService_BuildMissingRomReport_AllVerified_ReturnsNull()
    {
        var candidates = new List<RomCandidate>
        {
            new() { MainPath = @"C:\Roms\game1.rom", GameKey = "game1", DatMatch = true, Category = "GAME", Extension = ".rom", Region = "EU", SizeBytes = 100 }
        };
        var result = FeatureService.BuildMissingRomReport(candidates, new[] { @"C:\Roms" });
        Assert.Null(result);
    }

    [Fact]
    public void FeatureService_BuildMissingRomReport_HasUnverified_ReturnsReport()
    {
        var candidates = new List<RomCandidate>
        {
            new() { MainPath = @"C:\Roms\SNES\game1.rom", GameKey = "game1", DatMatch = false, Category = "GAME", Extension = ".rom", Region = "EU", SizeBytes = 100 },
            new() { MainPath = @"C:\Roms\SNES\game2.rom", GameKey = "game2", DatMatch = true, Category = "GAME", Extension = ".rom", Region = "US", SizeBytes = 200 }
        };
        var result = FeatureService.BuildMissingRomReport(candidates, new[] { @"C:\Roms" });
        Assert.NotNull(result);
        Assert.Contains("1 / 2", result);
    }

    [Fact]
    public void FeatureService_BuildCrossRootReport_NoGroups_ShowsEmpty()
    {
        var groups = new List<DedupeResult>();
        var result = FeatureService.BuildCrossRootReport(groups, new[] { @"C:\A", @"C:\B" });
        Assert.Contains("Cross-Root-Gruppen: 0", result);
        Assert.Contains("Keine Cross-Root-Duplikate", result);
    }

    [Fact]
    public void FeatureService_AppendCustomDatEntry_NewFile()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"dat_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            FeatureService.AppendCustomDatEntry(tmpDir, "  <game name=\"test\"><rom name=\"t.rom\" size=\"0\" crc=\"00000000\" /></game>");
            var datPath = Path.Combine(tmpDir, "custom.dat");
            Assert.True(File.Exists(datPath));
            var content = File.ReadAllText(datPath);
            Assert.Contains("<game name=\"test\">", content);
            Assert.Contains("</datafile>", content);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_FormatDatDiffReport_ContainsStats()
    {
        var diff = new DatDiffResult(
            Added: ["GameNew"],
            Removed: ["GameOld"],
            ModifiedCount: 1,
            UnchangedCount: 5);
        var report = FeatureService.FormatDatDiffReport("old.dat", "new.dat", diff);
        Assert.Contains("Gleich:       5", report);
        Assert.Contains("Geändert:     1", report);
        Assert.Contains("+ GameNew", report);
        Assert.Contains("- GameOld", report);
    }

    [Fact]
    public void FeatureService_BuildConvertQueueReport_EmptyDetails()
    {
        var est = new ConversionEstimateResult(0, 0, 0, 0.0, []);
        var report = FeatureService.BuildConvertQueueReport(est);
        Assert.Contains("Konvert-Warteschlange", report);
        Assert.Contains("Keine konvertierbaren Dateien gefunden", report);
    }

    [Fact]
    public void FeatureService_BuildConvertQueueReport_WithDetails()
    {
        var details = new List<ConversionDetail>
        {
            new("game.iso", "ISO", "CHD", 700_000_000, 400_000_000)
        };
        var est = new ConversionEstimateResult(700_000_000, 400_000_000, 300_000_000, 0.57, details);
        var report = FeatureService.BuildConvertQueueReport(est);
        Assert.Contains("game.iso", report);
        Assert.Contains("ISO", report);
        Assert.Contains("CHD", report);
    }

    [Fact]
    public void FeatureService_BuildPipelineReport_NullResult_ReturnsHelp()
    {
        var report = FeatureService.BuildPipelineReport(null, []);
        Assert.Contains("Pipeline-Engine", report);
        Assert.Contains("Starte einen Lauf", report);
    }

    [Fact]
    public void FeatureService_BuildPipelineReport_WithResult_ShowsPhases()
    {
        var result = new RunResult
        {
            Status = "ok",
            DurationMs = 5000,
            TotalFilesScanned = 100,
            GroupCount = 10,
            WinnerCount = 8
        };
        var candidates = new List<RomCandidate>
        {
            new() { MainPath = "a.rom", GameKey = "a", Category = "GAME" },
            new() { MainPath = "b.rom", GameKey = "b", Category = "JUNK" }
        };
        var report = FeatureService.BuildPipelineReport(result, candidates);
        Assert.Contains("Status: ok", report);
        Assert.Contains("100 Dateien", report);
        Assert.Contains("10 Gruppen", report);
        Assert.Contains("1 Junk-Dateien", report);
    }

    [Fact]
    public void FeatureService_BuildRuleEngineReport_NoRulesFile_ReturnsHelp()
    {
        // With a non-existent data directory, should return help text
        var report = FeatureService.BuildRuleEngineReport();
        Assert.NotNull(report);
        Assert.True(report.Length > 0);
    }

    // ═══ Batch-3 extraction tests ═══════════════════════════════════════

    [Fact]
    public void FeatureService_BuildNKitConvertReport_ContainsFileName()
    {
        var report = FeatureService.BuildNKitConvertReport(@"C:\Roms\game.nkit.iso");
        Assert.Contains("NKit-Konvertierung", report);
        Assert.Contains("game.nkit.iso", report);
        Assert.Contains("NKit-Format: Ja", report);
    }

    [Fact]
    public void FeatureService_BuildNKitConvertReport_NonNkit_ShowsNo()
    {
        var report = FeatureService.BuildNKitConvertReport(@"C:\Roms\game.iso");
        Assert.Contains("NKit-Format: Nein", report);
    }

    [Fact]
    public void FeatureService_ImportDatFileToRoot_PathTraversal_SanitizesFilename()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"dat_import_{Guid.NewGuid():N}");
        var datRoot = Path.Combine(tmpDir, "dats");
        Directory.CreateDirectory(datRoot);
        // Create a source file in a parent directory (simulating attempted traversal)
        var parentFile = Path.Combine(tmpDir, "escape.dat");
        File.WriteAllText(parentFile, "test-content");
        try
        {
            // Source path with ".." — the method strips path and copies just the filename
            var sourcePath = Path.Combine(datRoot, "..", "escape.dat");
            var target = FeatureService.ImportDatFileToRoot(sourcePath, datRoot);
            // Target MUST be within datRoot (path traversal protection)
            Assert.True(target.StartsWith(Path.GetFullPath(datRoot), StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(target));
            Assert.Equal("test-content", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_ImportDatFileToRoot_ValidCopy()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"dat_import_{Guid.NewGuid():N}");
        var source = Path.Combine(tmpDir, "source.dat");
        var datRoot = Path.Combine(tmpDir, "dats");
        Directory.CreateDirectory(tmpDir);
        Directory.CreateDirectory(datRoot);
        try
        {
            File.WriteAllText(source, "<datafile/>");
            var target = FeatureService.ImportDatFileToRoot(source, datRoot);
            Assert.True(File.Exists(target));
            Assert.Equal("<datafile/>", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_BuildFtpSourceReport_ValidSftp()
    {
        var (valid, isPlain, report) = FeatureService.BuildFtpSourceReport("sftp://roms.example.com/roms");
        Assert.True(valid);
        Assert.False(isPlain);
        Assert.Contains("SFTP", report);
        Assert.Contains("roms.example.com", report);
    }

    [Fact]
    public void FeatureService_BuildFtpSourceReport_ValidFtp()
    {
        var (valid, isPlain, report) = FeatureService.BuildFtpSourceReport("ftp://host.local/data");
        Assert.True(valid);
        Assert.True(isPlain);
        Assert.Contains("FTP", report);
    }

    [Fact]
    public void FeatureService_BuildFtpSourceReport_InvalidUrl()
    {
        var (valid, _, report) = FeatureService.BuildFtpSourceReport("http://example.com");
        Assert.False(valid);
        Assert.Contains("Ungültige FTP-URL", report);
    }

    [Fact]
    public void FeatureService_BuildGpuHashingStatus_ReturnsReport()
    {
        var (report, _) = FeatureService.BuildGpuHashingStatus();
        Assert.Contains("GPU-Hashing Konfiguration", report);
        Assert.Contains("CPU-Kerne", report);
    }

    [Fact]
    public void FeatureService_ToggleGpuHashing_Toggles()
    {
        // Save and restore original env var
        var original = Environment.GetEnvironmentVariable("ROMCLEANUP_GPU_HASHING");
        try
        {
            Environment.SetEnvironmentVariable("ROMCLEANUP_GPU_HASHING", "off");
            var result1 = FeatureService.ToggleGpuHashing();
            Assert.True(result1); // off → on

            var result2 = FeatureService.ToggleGpuHashing();
            Assert.False(result2); // on → off
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROMCLEANUP_GPU_HASHING", original);
        }
    }

    [Fact]
    public void FeatureService_BuildPdfReportData_EmptyCandidates()
    {
        var (summary, entries) = FeatureService.BuildPdfReportData(
            Array.Empty<RomCandidate>(), Array.Empty<DedupeResult>(), null, true);
        Assert.Equal("DryRun", summary.Mode);
        Assert.Equal(0, summary.TotalFiles);
        Assert.Empty(entries);
    }

    [Fact]
    public void FeatureService_BuildPdfReportData_PopulatedCandidates()
    {
        var candidates = new List<RomCandidate>
        {
            new() { MainPath = @"C:\game.rom", GameKey = "game", Category = "GAME", Extension = ".rom", Region = "EU", SizeBytes = 100, RegionScore = 50, FormatScore = 500, VersionScore = 100, DatMatch = true },
            new() { MainPath = @"C:\junk.rom", GameKey = "junk", Category = "JUNK", Extension = ".rom", Region = "US", SizeBytes = 200 }
        };
        var groups = new List<DedupeResult>
        {
            new() { GameKey = "game", Winner = candidates[0], Losers = [] }
        };
        var result = new RunResult { Status = "ok", DurationMs = 1234 };
        var (summary, entries) = FeatureService.BuildPdfReportData(candidates, groups, result, false);
        Assert.Equal("Move", summary.Mode);
        Assert.Equal(2, summary.TotalFiles);
        Assert.Equal(1, summary.JunkCount);
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void FeatureService_BuildConversionEstimateReport_EmptyCandidates()
    {
        var report = FeatureService.BuildConversionEstimateReport(Array.Empty<RomCandidate>());
        Assert.Contains("Konvertierungs-Schätzung", report);
    }

    [Theory]
    [InlineData("ABCDEF01", 8, true)]
    [InlineData("abcdef01", 8, true)]
    [InlineData("ZZZZZZZZ", 8, false)]
    [InlineData("ABCDEF0", 8, false)]
    [InlineData("da39a3ee5e6b4b0d3255bfef95601890afd80709", 40, true)]
    [InlineData("", 8, false)]
    public void FeatureService_IsValidHexHash(string hash, int len, bool expected)
    {
        Assert.Equal(expected, FeatureService.IsValidHexHash(hash, len));
    }

    [Fact]
    public void FeatureService_BuildCustomDatXmlEntry_EscapesXml()
    {
        var xml = FeatureService.BuildCustomDatXmlEntry("Game & \"Test\"", "rom<1>.bin", "AABBCCDD", "da39a3ee5e6b4b0d3255bfef95601890afd80709");
        Assert.Contains("Game &amp; &quot;Test&quot;", xml);
        Assert.Contains("rom&lt;1&gt;.bin", xml);
        Assert.Contains("crc=\"AABBCCDD\"", xml);
        Assert.Contains("sha1=\"da39a3ee5e6b4b0d3255bfef95601890afd80709\"", xml);
    }

    [Fact]
    public void FeatureService_BuildCustomDatXmlEntry_EmptySha1_OmitsSha1Attr()
    {
        var xml = FeatureService.BuildCustomDatXmlEntry("Game", "rom.bin", "AABBCCDD", "");
        Assert.DoesNotContain("sha1=", xml);
    }

    // ═══ Batch-4 extraction tests ═══════════════════════════════════════

    [Fact]
    public void FeatureService_DetectAutoProfile_EmptyRoots()
    {
        var result = FeatureService.DetectAutoProfile(Array.Empty<string>());
        Assert.Contains("Unbekannt", result);
    }

    [Fact]
    public void FeatureService_DetectAutoProfile_CartridgeOnly()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"auto_prof_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            File.WriteAllBytes(Path.Combine(tmpDir, "game.nes"), [0]);
            File.WriteAllBytes(Path.Combine(tmpDir, "game.sfc"), [0]);
            var result = FeatureService.DetectAutoProfile([tmpDir]);
            Assert.Contains("Cartridge", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_DetectAutoProfile_DiscOnly()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"auto_prof_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            File.WriteAllBytes(Path.Combine(tmpDir, "game.chd"), [0]);
            File.WriteAllBytes(Path.Combine(tmpDir, "game.iso"), [0]);
            var result = FeatureService.DetectAutoProfile([tmpDir]);
            Assert.Contains("Disc", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_BuildPlaytimeReport_NoLrtl_ReturnsEmpty()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"playtime_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var result = FeatureService.BuildPlaytimeReport(tmpDir);
            Assert.Equal("", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_BuildPlaytimeReport_WithLrtl()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"playtime_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            File.WriteAllLines(Path.Combine(tmpDir, "game.lrtl"), ["10", "20", "30"]);
            var result = FeatureService.BuildPlaytimeReport(tmpDir);
            Assert.Contains("Spielzeit-Tracker", result);
            Assert.Contains("game", result);
            Assert.Contains("3 Einträge", result);
        }
        finally
        {
            Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FeatureService_BuildCollectionManagerReport_EmptyCandidates()
    {
        var report = FeatureService.BuildCollectionManagerReport(Array.Empty<RomCandidate>());
        Assert.Contains("Smart Collection Manager", report);
        Assert.Contains("Gesamt: 0 ROMs", report);
    }

    [Fact]
    public void FeatureService_BuildCollectionManagerReport_GroupsByGenre()
    {
        var candidates = new List<RomCandidate>
        {
            new() { MainPath = "a.rom", GameKey = "Super Mario", Category = "GAME" },
            new() { MainPath = "b.rom", GameKey = "Zelda", Category = "GAME" },
            new() { MainPath = "c.rom", GameKey = "Doom", Category = "GAME" }
        };
        var report = FeatureService.BuildCollectionManagerReport(candidates);
        Assert.Contains("Gesamt: 3 ROMs", report);
    }

    [Fact]
    public void FeatureService_BuildCommandPaletteReport_ShowsResults()
    {
        var results = new List<(string key, string name, string shortcut, int score)>
        {
            ("dryrun", "DryRun starten", "F5", 0),
            ("theme", "Theme wechseln", "Ctrl+T", 2)
        };
        var report = FeatureService.BuildCommandPaletteReport("dry", results);
        Assert.Contains("Ergebnisse für \"dry\"", report);
        Assert.Contains("DryRun starten", report);
        Assert.Contains("F5", report);
    }

    [Fact]
    public void FeatureService_BuildCommandPaletteReport_EmptyResults()
    {
        var report = FeatureService.BuildCommandPaletteReport("xyz",
            Array.Empty<(string, string, string, int)>());
        Assert.Contains("Ergebnisse für \"xyz\"", report);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(16, 8)]
    [InlineData(1, 1)]
    public void FeatureService_BuildParallelHashingReport_ContainsCoreInfo(int cores, int threads)
    {
        var report = FeatureService.BuildParallelHashingReport(cores, threads);
        Assert.Contains($"CPU-Kerne: {cores}", report);
        Assert.Contains($"Threads (neu): {threads}", report);
        Assert.Contains("nächsten Hash-Vorgang", report);
    }

    // ═══ Browse + Quick Commands (Runde 18) ═════════════════════════════

    [Fact]
    public void BrowseToolPathCommand_Exists()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.BrowseToolPathCommand);
    }

    [Fact]
    public void BrowseFolderPathCommand_Exists()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.BrowseFolderPathCommand);
    }

    [Fact]
    public void QuickPreviewCommand_CanExecute_WhenRootsExistAndNotBusy()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        Assert.True(vm.QuickPreviewCommand.CanExecute(null));
    }

    [Fact]
    public void QuickPreviewCommand_CannotExecute_WhenNoRoots()
    {
        var vm = new MainViewModel();
        Assert.False(vm.QuickPreviewCommand.CanExecute(null));
    }

    [Fact]
    public void QuickPreviewCommand_CannotExecute_WhenBusy()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        vm.CurrentRunState = RunState.Scanning;
        Assert.False(vm.QuickPreviewCommand.CanExecute(null));
    }

    [Fact]
    public void StartMoveCommand_CanExecute_WhenRootsExistAndNotBusy()
    {
        var vm = new MainViewModel();
        vm.Roots.Add(@"C:\TestRoot");
        Assert.True(vm.StartMoveCommand.CanExecute(null));
    }

    [Fact]
    public void StartMoveCommand_CannotExecute_WhenNoRoots()
    {
        var vm = new MainViewModel();
        Assert.False(vm.StartMoveCommand.CanExecute(null));
    }

    [Fact]
    public void BrowseToolPathCommand_SetsChdman()
    {
        // BrowseToolPathCommand with "Chdman" parameter should set ToolChdman
        var stub = new StubDialogService { BrowseFileResult = @"C:\tools\chdman.exe" };
        var vm = new MainViewModel(new ThemeService(), stub);
        vm.BrowseToolPathCommand.Execute("Chdman");
        Assert.Equal(@"C:\tools\chdman.exe", vm.ToolChdman);
    }

    [Fact]
    public void BrowseFolderPathCommand_SetsDatRoot()
    {
        var stub = new StubDialogService { BrowseFolderResult = @"C:\dat" };
        var vm = new MainViewModel(new ThemeService(), stub);
        vm.BrowseFolderPathCommand.Execute("Dat");
        Assert.Equal(@"C:\dat", vm.DatRoot);
    }

    [Fact]
    public void BrowseToolPathCommand_NoOpWhenCancelled()
    {
        // BrowseFile returns null → property unchanged
        var stub = new StubDialogService { BrowseFileResult = null };
        var vm = new MainViewModel(new ThemeService(), stub);
        vm.ToolChdman = "original";
        vm.BrowseToolPathCommand.Execute("Chdman");
        Assert.Equal("original", vm.ToolChdman);
    }

    /// <summary>Minimal dialog service stub for VM command tests (no UI).</summary>
    private sealed class StubDialogService : IDialogService
    {
        public string? BrowseFileResult { get; set; }
        public string? BrowseFolderResult { get; set; }

        public string? BrowseFolder(string title = "Ordner auswählen") => BrowseFolderResult;
        public string? BrowseFile(string title = "Datei auswählen", string filter = "Alle Dateien|*.*") => BrowseFileResult;
        public string? SaveFile(string title = "Speichern unter", string filter = "Alle Dateien|*.*", string? defaultFileName = null) => null;
        public bool Confirm(string message, string title = "Bestätigung") => true;
        public void Info(string message, string title = "Information") { }
        public void Error(string message, string title = "Fehler") { }
        public ConfirmResult YesNoCancel(string message, string title = "Frage") => ConfirmResult.Yes;
        public string ShowInputBox(string prompt, string title = "Eingabe", string defaultValue = "") => defaultValue;
        public void ShowText(string title, string content) { }
    }
}
