namespace RomCleanup.Tests.Benchmark.Infrastructure;

/// <summary>
/// Resolves absolute paths to benchmark data files relative to the repository root.
/// Uses the same repo-root detection pattern as existing test infrastructure.
/// </summary>
internal static class BenchmarkPaths
{
    private static string? _repoRoot;

    public static string RepoRoot => _repoRoot ??= ResolveRepoRoot();

    public static string BenchmarkDir => Path.Combine(RepoRoot, "benchmark");
    public static string GroundTruthDir => Path.Combine(BenchmarkDir, "ground-truth");
    public static string DatsDir => Path.Combine(BenchmarkDir, "dats");
    public static string BaselinesDir => Path.Combine(BenchmarkDir, "baselines");
    public static string ReportsDir => Path.Combine(BenchmarkDir, "reports");
    public static string GatesJsonPath => Path.Combine(BenchmarkDir, "gates.json");
    public static string ManifestJsonPath => Path.Combine(BenchmarkDir, "manifest.json");
    public static string LatestBaselinePath => Path.Combine(BaselinesDir, "latest-baseline.json");
    public static string VersionedBaselinePath => Path.Combine(BaselinesDir, "v0.1.0-baseline.json");
    public static string CurrentBenchmarkReportPath => Path.Combine(ReportsDir, "benchmark-results.json");
    public static string SchemaPath => Path.Combine(GroundTruthDir, "ground-truth.schema.json");
    public static string DataDir => Path.Combine(RepoRoot, "data");
    public static string ConsolesJsonPath => Path.Combine(DataDir, "consoles.json");

    public static string[] AllJsonlFiles =>
        Directory.Exists(GroundTruthDir)
            ? Directory.GetFiles(GroundTruthDir, "*.jsonl")
            : [];

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "RomCleanup.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Repository root not found. Ensure tests run from a directory below the repo root.");
    }
}
