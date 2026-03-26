using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.Infrastructure.Reporting;

namespace RomCleanup.UI.Wpf.Services;

public static partial class FeatureService
{

    // ═══ CONFIG DIFF ════════════════════════════════════════════════════
    // Port of ConfigMerge.ps1

    public static List<ConfigDiffEntry> GetConfigDiff(Dictionary<string, string> current, Dictionary<string, string> saved)
    {
        var result = new List<ConfigDiffEntry>();
        var allKeys = current.Keys.Union(saved.Keys).Distinct();
        foreach (var key in allKeys)
        {
            current.TryGetValue(key, out var curVal);
            saved.TryGetValue(key, out var savedVal);
            if (curVal != savedVal)
                result.Add(new ConfigDiffEntry(key, savedVal ?? "(fehlt)", curVal ?? "(fehlt)"));
        }
        return result;
    }


    // ═══ LOCALIZATION ═══════════════════════════════════════════════════
    // Port of Localization.ps1

    public static Dictionary<string, string> LoadLocale(string locale)
    {
        var dataDir = ResolveDataDirectory("i18n");
        if (dataDir is null)
            return new Dictionary<string, string>();

        var path = Path.Combine(dataDir, $"{locale}.json");
        if (!File.Exists(path))
            path = Path.Combine(dataDir, "de.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>();

        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path));
            if (json is null) return new Dictionary<string, string>();
            return json.Where(kv => kv.Value.ValueKind == JsonValueKind.String)
                       .ToDictionary(kv => kv.Key, kv => kv.Value.GetString() ?? "");
        }
        catch { return new Dictionary<string, string>(); }
    }


    /// <summary>Resolve the data/ subdirectory, probing from BaseDirectory upward (max 5 levels).</summary>
    internal static string? ResolveDataDirectory(string? subFolder = null)
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "data"),
            Path.Combine(AppContext.BaseDirectory, "data"),
        };

        // Also probe upward from BaseDirectory (for dev layouts)
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 5 && dir is not null; i++)
        {
            var probe = Path.Combine(dir, "data");
            if (Directory.Exists(probe))
                return subFolder is not null ? Path.Combine(probe, subFolder) : probe;
            dir = Path.GetDirectoryName(dir);
        }

        foreach (var c in candidates)
            if (Directory.Exists(c))
                return subFolder is not null ? Path.Combine(c, subFolder) : c;

        return null;
    }


    // ═══ PORTABLE MODE CHECK ════════════════════════════════════════════
    // Port of PortableMode.ps1
    // NOTE: The marker file ".portable" is checked relative to AppContext.BaseDirectory,
    // which is the directory containing the executable (e.g. bin/Debug/net10.0-windows/).
    // Place ".portable" next to the .exe, NOT the workspace root.

    public static bool IsPortableMode()
    {
        var marker = Path.Combine(AppContext.BaseDirectory, ".portable");
        return File.Exists(marker);
    }


    // ═══ DOCKER CONFIG ══════════════════════════════════════════════════
    // Port of DockerContainer.ps1

    public static string GenerateDockerfile()
    {
        return """
            FROM mcr.microsoft.com/dotnet/aspnet:10.0
            LABEL maintainer="RomCleanup" description="ROM Cleanup REST API"
            WORKDIR /app
            COPY publish/ .
            VOLUME ["/data/roms", "/data/config"]
            EXPOSE 5000 5001
            ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
            ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/cert.pfx
            ENTRYPOINT ["dotnet", "RomCleanup.Api.dll"]
            """;
    }


    public static string GenerateDockerCompose()
    {
        return """
            # HINWEIS: ROM_CLEANUP_API_KEY NICHT in docker-compose.yml hartcodieren!
            # Verwende eine .env-Datei oder Docker Secrets.
            services:
              romcleanup:
                build: .
                ports:
                  - "5000:5000"
                volumes:
                  - ./roms:/data/roms
                  - ./config:/data/config
                environment:
                  - ROM_CLEANUP_API_KEY=${ROM_CLEANUP_API_KEY}
                restart: unless-stopped
                healthcheck:
                  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
                  interval: 30s
                  retries: 3
            """;
    }


    // ═══ WINDOWS CONTEXT MENU ═══════════════════════════════════════════
    // Port of WindowsContextMenu.ps1

    public static string GetContextMenuRegistryScript()
    {
        var exePath = Environment.ProcessPath ?? "RomCleanup.CLI.exe";
        // .reg format requires backslashes doubled and paths with spaces quoted
        var escapedPath = "\\\"" + exePath.Replace("\\", "\\\\") + "\\\"";
        return $"""
            Windows Registry Editor Version 5.00

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_DryRun]
            @="ROM Cleanup – DryRun Scan"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_DryRun\command]
            @="{escapedPath} --roots \\\"%V\\\" --mode DryRun"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_Move]
            @="ROM Cleanup – Move Sort"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_Move\command]
            @="{escapedPath} --roots \\\"%V\\\" --mode Move"
            """;
    }


    // ═══ ACCESSIBILITY ══════════════════════════════════════════════════
    // Port of Accessibility.ps1

    public static bool IsHighContrastActive()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Accessibility\HighContrast");
            var flags = key?.GetValue("Flags") as string;
            return flags is not null && int.TryParse(flags, out var f) && (f & 1) != 0;
        }
        catch { return false; }
    }


    // ═══ MOBILE WEB UI ══════════════════════════════════════════════════

    /// <summary>Try to find the API project path.</summary>
    public static string? FindApiProjectPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RomCleanup.Api", "RomCleanup.Api.csproj"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "RomCleanup.Api", "RomCleanup.Api.csproj")
        };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }


    // ═══ RULE PACK SHARING ══════════════════════════════════════════════

    /// <summary>Export rules.json to a user-chosen path.</summary>
    public static bool ExportRulePack(string rulesPath, string savePath)
    {
        if (!File.Exists(rulesPath)) return false;
        File.Copy(rulesPath, savePath, overwrite: true);
        return true;
    }


    /// <summary>Import rules.json from an external file (validates JSON first).</summary>
    public static void ImportRulePack(string importPath, string rulesPath)
    {
        var json = File.ReadAllText(importPath);
        System.Text.Json.JsonDocument.Parse(json).Dispose(); // validate
        var dir = Path.GetDirectoryName(rulesPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.Copy(importPath, rulesPath, overwrite: true);
    }

}
