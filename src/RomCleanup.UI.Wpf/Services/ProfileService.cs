using System.IO;
using System.Text.Json;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Manages profile CRUD: delete, import, export, config-diff.
/// Extracted from MainWindow.xaml.cs (RF-008).
/// </summary>
public sealed class ProfileService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    /// <summary>Delete the saved profile. Returns true if deleted.</summary>
    public static bool Delete()
    {
        if (!File.Exists(SettingsPath)) return false;
        File.Delete(SettingsPath);
        return true;
    }

    /// <summary>Import a JSON profile from the given path. Creates backup of existing.</summary>
    public static void Import(string sourcePath)
    {
        var json = File.ReadAllText(sourcePath);
        JsonDocument.Parse(json).Dispose(); // validate

        Directory.CreateDirectory(SettingsDir);
        if (File.Exists(SettingsPath))
        {
            var backupPath = SettingsPath + $".{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Copy(SettingsPath, backupPath, overwrite: false);
        }

        File.Copy(sourcePath, SettingsPath, overwrite: true);
    }

    /// <summary>Export current config map to a JSON file.</summary>
    public static void Export(string targetPath, Dictionary<string, string> configMap)
    {
        File.WriteAllText(targetPath, JsonSerializer.Serialize(configMap, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Get the saved config as a flattened key-value dictionary. Returns null if not found.</summary>
    public static Dictionary<string, string>? LoadSavedConfigFlat()
    {
        if (!File.Exists(SettingsPath)) return null;
        var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        var result = new Dictionary<string, string>();
        FlattenJson(doc.RootElement, "", result);
        return result;
    }

    private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    FlattenJson(prop.Value, string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}", result);
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (var item in element.EnumerateArray())
                    FlattenJson(item, $"{prefix}[{i++}]", result);
                break;
            default:
                result[prefix] = element.ToString();
                break;
        }
    }
}
