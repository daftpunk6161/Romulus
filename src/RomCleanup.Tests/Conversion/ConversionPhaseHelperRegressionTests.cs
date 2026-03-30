using RomCleanup.Contracts;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Audit;
using RomCleanup.Infrastructure.FileSystem;
using RomCleanup.Infrastructure.Metrics;
using RomCleanup.Infrastructure.Orchestration;
using Xunit;

namespace RomCleanup.Tests.Conversion;

public sealed class ConversionPhaseHelperRegressionTests : IDisposable
{
    private readonly string _root;

    public ConversionPhaseHelperRegressionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "RomCleanup.ConversionPhaseHelper", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ConvertSingleFile_VerifyFailed_ReturnsErrorOutcomeAndIncrementsErrorCounter()
    {
        var sourcePath = Path.Combine(_root, "game.iso");
        File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);

        var targetPath = Path.Combine(_root, "game.chd");
        var converter = new VerifyFailingConverter(targetPath);
        var counters = new ConversionPhaseHelper.ConversionCounters();

        var result = ConversionPhaseHelper.ConvertSingleFile(
            sourcePath,
            "PS1",
            converter,
            new RunOptions
            {
                Roots = [_root],
                Mode = RunConstants.ModeMove,
                Extensions = [".iso"]
            },
            CreateContext(RunConstants.ModeMove),
            counters,
            trackSetMembers: false,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ConversionOutcome.Error, result!.Outcome);
        Assert.Equal(1, counters.Errors);
        Assert.Equal(0, counters.Converted);
    }

    [Fact]
    public void ConvertSingleFile_DryRun_SkipsConversion()
    {
        var sourcePath = Path.Combine(_root, "preview.iso");
        File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);

        var converter = new RecordingConverter();
        var counters = new ConversionPhaseHelper.ConversionCounters();

        var result = ConversionPhaseHelper.ConvertSingleFile(
            sourcePath,
            "PS1",
            converter,
            new RunOptions
            {
                Roots = [_root],
                Mode = RunConstants.ModeDryRun,
                Extensions = [".iso"]
            },
            CreateContext(RunConstants.ModeDryRun),
            counters,
            trackSetMembers: false,
            CancellationToken.None);

        Assert.Null(result);
        Assert.False(converter.ConvertCalled);
        Assert.Equal(0, counters.Converted);
        Assert.Equal(0, counters.Errors);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch
        {
        }
    }

    private PipelineContext CreateContext(string mode)
    {
        var metrics = new PhaseMetricsCollector();
        metrics.Initialize();

        return new PipelineContext
        {
            Options = new RunOptions
            {
                Roots = [_root],
                Mode = mode,
                Extensions = [".iso"]
            },
            FileSystem = new FileSystemAdapter(),
            AuditStore = new AuditCsvStore(),
            Metrics = metrics
        };
    }

    private sealed class VerifyFailingConverter(string targetPath) : IFormatConverter
    {
        public ConversionTarget? GetTargetFormat(string consoleKey, string sourceExtension)
            => new(".chd", "chdman", "createcd");

        public ConversionResult Convert(string sourcePath, ConversionTarget target, CancellationToken cancellationToken = default)
        {
            File.WriteAllBytes(targetPath, [7, 8, 9, 10]);
            return new ConversionResult(sourcePath, targetPath, ConversionOutcome.Success);
        }

        public bool Verify(string targetPath, ConversionTarget target) => false;
    }

    private sealed class RecordingConverter : IFormatConverter
    {
        public bool ConvertCalled { get; private set; }

        public ConversionTarget? GetTargetFormat(string consoleKey, string sourceExtension)
            => new(".chd", "chdman", "createcd");

        public ConversionResult Convert(string sourcePath, ConversionTarget target, CancellationToken cancellationToken = default)
        {
            ConvertCalled = true;
            return new ConversionResult(sourcePath, Path.ChangeExtension(sourcePath, ".chd"), ConversionOutcome.Success);
        }

        public bool Verify(string targetPath, ConversionTarget target) => true;
    }
}
