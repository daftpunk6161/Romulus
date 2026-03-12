using RomCleanup.Infrastructure.Dat;
using Xunit;
using System.Net;
using System.Net.Http;

namespace RomCleanup.Tests;

public class DatSourceServiceTests : IDisposable
{
    private readonly string _tempDir;

    public DatSourceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RomCleanup_DatSrc_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task VerifyDatSignature_CorrectSha256_ReturnsTrue()
    {
        var path = Path.Combine(_tempDir, "test.dat");
        File.WriteAllText(path, "test content");

        // Compute actual SHA256
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();

        using var svc = new DatSourceService(_tempDir);
        Assert.True(await svc.VerifyDatSignatureAsync(path, "", hash));
    }

    [Fact]
    public async Task VerifyDatSignature_WrongSha256_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "test.dat");
        File.WriteAllText(path, "test content");

        using var svc = new DatSourceService(_tempDir);
        Assert.False(await svc.VerifyDatSignatureAsync(path, "", "0000000000000000000000000000000000000000000000000000000000000000"));
    }

    [Fact]
    public async Task VerifyDatSignature_NonExistentFile_ReturnsFalse()
    {
        using var svc = new DatSourceService(_tempDir);
        Assert.False(await svc.VerifyDatSignatureAsync(
            Path.Combine(_tempDir, "nope.dat"), "", "abc123"));
    }

    [Fact]
    public async Task VerifyDatSignature_NoHashNoUrl_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "test.dat");
        File.WriteAllText(path, "data");

        using var svc = new DatSourceService(_tempDir);
        // No expected hash, empty URL → fail-closed
        Assert.False(await svc.VerifyDatSignatureAsync(path, "", null));
    }

    [Fact]
    public async Task VerifyDatSignature_ParallelRequests_CompleteWithoutDeadlock()
    {
        var path = Path.Combine(_tempDir, "parallel-test.dat");
        File.WriteAllText(path, "parallel-content");

        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();

        using var svc = new DatSourceService(_tempDir);

        var tasks = Enumerable.Range(0, 64)
            .Select(_ => svc.VerifyDatSignatureAsync(path, "", hash))
            .ToArray();

        var all = Task.WhenAll(tasks);
        var completed = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(10))) == all;

        Assert.True(completed, "Parallel signature verification timed out or deadlocked");
        Assert.All(all.Result, r => Assert.True(r));
    }

    [Fact]
    public async Task VerifyDatSignature_SidecarRequest_Cancellation_ReturnsFalse()
    {
        var path = Path.Combine(_tempDir, "cancel-test.dat");
        File.WriteAllText(path, "cancel-content");

        var handler = new DelayedOkHandler(TimeSpan.FromSeconds(5));
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var svc = new DatSourceService(_tempDir, httpClient: httpClient);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.VerifyDatSignatureAsync(path, "https://example.invalid/test.dat", null, cts.Token);
        sw.Stop();

        Assert.False(result);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), "Cancellation was not observed promptly");
    }

    [Fact]
    public void LoadCatalog_ValidJson_ParsesEntries()
    {
        var json = @"[
            { ""id"": ""redump-ps1"", ""group"": ""Redump"", ""system"": ""Sony - PS1"",
              ""url"": ""https://example.com/ps1.dat"", ""format"": ""zip-dat"", ""consoleKey"": ""PSX"" },
            { ""id"": ""nointro-nes"", ""group"": ""No-Intro"", ""system"": ""Nintendo NES"",
              ""url"": """", ""format"": ""nointro-pack"", ""consoleKey"": ""NES"", ""packMatch"": ""Nintendo*"" }
        ]";
        var path = Path.Combine(_tempDir, "catalog.json");
        File.WriteAllText(path, json);

        var entries = DatSourceService.LoadCatalog(path);
        Assert.Equal(2, entries.Count);
        Assert.Equal("redump-ps1", entries[0].Id);
        Assert.Equal("PSX", entries[0].ConsoleKey);
        Assert.Equal("Nintendo*", entries[1].PackMatch);
    }

    [Fact]
    public void LoadCatalog_NonExistent_ReturnsEmpty()
    {
        var entries = DatSourceService.LoadCatalog(Path.Combine(_tempDir, "nope.json"));
        Assert.Empty(entries);
    }

    [Fact]
    public void LoadCatalog_MalformedJson_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(path, "not json");

        var entries = DatSourceService.LoadCatalog(path);
        Assert.Empty(entries);
    }

    private sealed class DelayedOkHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("0000000000000000000000000000000000000000000000000000000000000000")
            };
        }
    }
}
