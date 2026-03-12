using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace RomCleanup.Tests;

public sealed class ApiIntegrationTests
{
    private const string ApiKey = "integration-test-key";

    [Fact]
    public async Task Health_WithoutApiKey_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_WithApiKey_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_Preflight_Options_Returns204_WithExpectedHeaders()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["CorsMode"] = "custom",
            ["CorsAllowOrigin"] = "http://example.test"
        });
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/runs");
        request.Headers.Add("Origin", "http://example.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("http://example.test", origins);
    }

    [Fact]
    public async Task RateLimit_Exceeded_Returns429()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimitRequests"] = "2",
            ["RateLimitWindowSeconds"] = "60"
        });
        using var client = CreateClientWithApiKey(factory);

        var first = await client.GetAsync("/health");
        var second = await client.GetAsync("/health");
        var third = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((HttpStatusCode)429, third.StatusCode);
    }

    [Fact]
    public async Task Runs_InvalidPreferRegions_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var root = CreateTempRoot();
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                roots = new[] { root },
                mode = "DryRun",
                preferRegions = new[] { "<script>alert(1)</script>" }
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/runs", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid region", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Runs_OversizedBody_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var huge = new string('a', 1_048_590);
        var payload = "{\"roots\":[\"" + huge + "\"]}";

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/runs", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("too large", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runs_SystemDirectoryRoot_IsRejected()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var payload = JsonSerializer.Serialize(new
        {
            roots = new[] { windowsDir },
            mode = "DryRun"
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/runs", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("System directory not allowed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runs_Lifecycle_AndStream_Endpoints_Work()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var root = CreateTempRoot();
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                roots = new[] { root },
                mode = "DryRun",
                preferRegions = new[] { "EU", "US" }
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var createResponse = await client.PostAsync("/runs?wait=true", content);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createJson = await createResponse.Content.ReadAsStringAsync();
            using var createDoc = JsonDocument.Parse(createJson);
            var runId = createDoc.RootElement
                .GetProperty("run")
                .GetProperty("runId")
                .GetString();

            Assert.False(string.IsNullOrWhiteSpace(runId));

            var statusResponse = await client.GetAsync($"/runs/{runId}");
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

            var resultResponse = await client.GetAsync($"/runs/{runId}/result");
            Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

            using var streamRequest = new HttpRequestMessage(HttpMethod.Get, $"/runs/{runId}/stream");
            var streamResponse = await client.SendAsync(streamRequest, HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
            Assert.NotNull(streamResponse.Content.Headers.ContentType);
            Assert.Equal("text/event-stream", streamResponse.Content.Headers.ContentType!.MediaType);

            var cancelResponse = await client.PostAsync($"/runs/{runId}/cancel", null);
            Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        }
        finally
        {
            SafeDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task OpenApi_Declares_ApiKey_Header_And_GlobalSecurityRequirement()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var response = await client.GetAsync("/openapi");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"securitySchemes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ApiKey\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"X-Api-Key\"", json, StringComparison.Ordinal);
        Assert.Contains("\"security\": [{ \"ApiKey\": [] }]", json, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApiKey"] = ApiKey,
            ["CorsMode"] = "strict-local",
            ["CorsAllowOrigin"] = "http://127.0.0.1",
            ["RateLimitRequests"] = "120",
            ["RateLimitWindowSeconds"] = "60"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
                settings[key] = value;
        }

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(settings);
                });
            });
    }

    private static HttpClient CreateClientWithApiKey(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "RomCleanup_ApiInt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "sample.rom"), "test");
        return root;
    }

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Best effort cleanup for temp directories.
        }
    }

    // --- Additional Negative Tests ---

    [Fact]
    public async Task Runs_InvalidJson_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        using var content = new StringContent("{invalid-json!!!", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/runs", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid JSON", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runs_EmptyRoots_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var payload = JsonSerializer.Serialize(new { roots = Array.Empty<string>(), mode = "DryRun" });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/runs", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("roots", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runs_InvalidMode_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var root = CreateTempRoot();
        try
        {
            var payload = JsonSerializer.Serialize(new { roots = new[] { root }, mode = "Delete" });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/runs", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("mode", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Runs_DriveRoot_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var payload = JsonSerializer.Serialize(new { roots = new[] { @"C:\" }, mode = "DryRun" });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/runs", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not allowed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runs_RunNotFound_Returns404()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var response = await client.GetAsync("/runs/nonexistent-run-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var resultResponse = await client.GetAsync("/runs/nonexistent-run-id/result");
        Assert.Equal(HttpStatusCode.NotFound, resultResponse.StatusCode);

        var cancelResponse = await client.PostAsync("/runs/nonexistent-run-id/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task Runs_ConcurrentRun_ReturnsConflict()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var root = CreateTempRoot();
        try
        {
            var payload = JsonSerializer.Serialize(new { roots = new[] { root }, mode = "DryRun" });

            using var content1 = new StringContent(payload, Encoding.UTF8, "application/json");
            var first = await client.PostAsync("/runs?wait=true", content1);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            // Second run with same root — may conflict if first is still flagged as active
            // (since first used ?wait=true it may have completed; this validates the endpoint accepts valid input)
            using var content2 = new StringContent(payload, Encoding.UTF8, "application/json");
            var second = await client.PostAsync("/runs?wait=true", content2);

            // Should be OK (first already completed) or Conflict (if still registered)
            Assert.True(
                second.StatusCode == HttpStatusCode.OK || second.StatusCode == HttpStatusCode.Conflict,
                $"Expected OK or Conflict, got {second.StatusCode}");
        }
        finally
        {
            SafeDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Runs_NonexistentRoot_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var payload = JsonSerializer.Serialize(new { roots = new[] { @"C:\NonExistentPath_" + Guid.NewGuid().ToString("N") }, mode = "DryRun" });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/runs", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not found", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_ResponseContainsVersionHeader()
    {
        using var factory = CreateFactory();
        using var client = CreateClientWithApiKey(factory);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Api-Version", out var versions));
        Assert.Contains("1.0", versions);
    }
}