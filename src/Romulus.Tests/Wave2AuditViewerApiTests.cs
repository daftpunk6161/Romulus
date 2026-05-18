using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.Audit;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave 2 — T-W2-AUDIT-VIEWER-API pin tests.
/// Acceptance gates from plan.yaml:
///   * Read-only Port + Adapter existieren.
///   * Pagination & Filter (RunId, Datum, Outcome) sind implementiert.
///   * Adapter dupliziert keine Sidecar/CSV-Logik (nutzt AuditCsvParser + AuditSigningService).
///   * Konsumenten (GUI/CLI/API) koennten Audit lesen ohne Audit-Schreibwege zu kennen.
/// </summary>
public sealed class Wave2AuditViewerApiTests
{
    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rom-w2-auditviewer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteAuditCsv(string path, IEnumerable<string[]> rows)
    {
        var lines = new List<string> { "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp" };
        foreach (var r in rows) lines.Add(string.Join(",", r));
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
    }

    [Fact]
    public void Port_DoesNotExposeAnyWriteOperations()
    {
        var methods = typeof(IAuditViewerBackingService).GetMethods();
        foreach (var m in methods)
        {
            var name = m.Name;
            Assert.False(
                name.StartsWith("Write", StringComparison.Ordinal)
                || name.StartsWith("Append", StringComparison.Ordinal)
                || name.StartsWith("Delete", StringComparison.Ordinal)
                || name.StartsWith("Rollback", StringComparison.Ordinal),
                $"IAuditViewerBackingService is read-only; method {name} suggests a write operation.");
        }
    }

    [Fact]
    public void Adapter_ImplementsPort()
    {
        Assert.True(typeof(IAuditViewerBackingService).IsAssignableFrom(typeof(AuditViewerBackingService)));
    }

    [Fact]
    public void ListRuns_EmptyDirectory_ReturnsEmpty()
    {
        var root = TempRoot();
        try
        {
            var svc = new AuditViewerBackingService();
            var runs = svc.ListRuns(root);
            Assert.Empty(runs);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ListRuns_InvalidOrMissingRoot_FailsClosed()
    {
        var svc = new AuditViewerBackingService();

        Assert.Throws<ArgumentException>(() => svc.ListRuns(" "));
        Assert.Empty(svc.ListRuns(Path.Combine(Path.GetTempPath(), "missing-audit-root-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void ListRuns_OrdersMostRecentFirst()
    {
        var root = TempRoot();
        try
        {
            var older = Path.Combine(root, "audit-old.csv");
            var newer = Path.Combine(root, "audit-new.csv");
            WriteAuditCsv(older, new[] { new[] { "C:\\r", "a", "b", "Move", "", "", "" , "2026-01-01T00:00:00Z" } });
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            WriteAuditCsv(newer, new[] { new[] { "C:\\r", "a", "b", "Move", "", "", "", "2026-04-29T00:00:00Z" } });
            File.SetLastWriteTimeUtc(newer, new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc));

            var svc = new AuditViewerBackingService();
            var runs = svc.ListRuns(root);

            Assert.Equal(2, runs.Count);
            Assert.Equal("audit-new.csv", runs[0].FileName);
            Assert.Equal("audit-old.csv", runs[1].FileName);
            Assert.Equal("new", runs[0].RunId);
            Assert.Equal("old", runs[1].RunId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ListRuns_DateFiltersAndAuditSuffixRunIds_Work()
    {
        var root = TempRoot();
        try
        {
            var older = Path.Combine(root, "alpha.audit.csv");
            var inside = Path.Combine(root, "beta.audit.csv");
            var newer = Path.Combine(root, "gamma.audit.csv");
            WriteAuditCsv(older, Array.Empty<string[]>());
            WriteAuditCsv(inside, Array.Empty<string[]>());
            WriteAuditCsv(newer, Array.Empty<string[]>());
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(inside, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newer, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

            var svc = new AuditViewerBackingService();
            var filtered = svc.ListRuns(
                root,
                new AuditRunFilter(
                    FromUtc: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
                    ToUtc: new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero)));

            Assert.Single(filtered);
            Assert.Equal("beta", filtered[0].RunId);
            Assert.Equal("beta.audit.csv", filtered[0].FileName);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ListRuns_InvalidSidecar_DoesNotMarkRunAsVerified()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-invalid-sidecar.csv");
            WriteAuditCsv(path, new[] { new[] { "C:\\r", "a", "b", "Move", "", "", "", "2026-04-01T00:00:00Z" } });
            File.WriteAllText(path + ".meta.json", "{not-json");

            var svc = new AuditViewerBackingService();
            var run = Assert.Single(svc.ListRuns(root));

            Assert.True(run.HasSidecar);
            Assert.False(run.IsSidecarValid);
            Assert.Equal(1, run.RowCount);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ListRuns_FilterByRunId_Works()
    {
        var root = TempRoot();
        try
        {
            WriteAuditCsv(Path.Combine(root, "audit-alpha.csv"), Array.Empty<string[]>());
            WriteAuditCsv(Path.Combine(root, "audit-beta.csv"), Array.Empty<string[]>());

            var svc = new AuditViewerBackingService();
            var runs = svc.ListRuns(root, new AuditRunFilter(RunId: "alpha"));

            Assert.Single(runs);
            Assert.Equal("alpha", runs[0].RunId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ListRuns_Pagination_Works()
    {
        var root = TempRoot();
        try
        {
            for (int i = 0; i < 5; i++)
                WriteAuditCsv(Path.Combine(root, $"audit-{i:00}.csv"), Array.Empty<string[]>());

            var svc = new AuditViewerBackingService();
            var page0 = svc.ListRuns(root, page: new AuditPage(0, 2));
            var page1 = svc.ListRuns(root, page: new AuditPage(1, 2));
            var page2 = svc.ListRuns(root, page: new AuditPage(2, 2));

            Assert.Equal(2, page0.Count);
            Assert.Equal(2, page1.Count);
            Assert.Single(page2);
            Assert.NotEqual(page0[0].FileName, page1[0].FileName);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadRunRows_ReturnsParsedRows()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-one.csv");
            WriteAuditCsv(path, new[]
            {
                new[] { "C:\\r", "a.rom", "b.rom", "Move", "Game", "h1", "winner", "2026-04-01T00:00:00Z" },
                new[] { "C:\\r", "x.rom", "y.rom", "Skip", "Game", "h2", "noop", "2026-04-02T00:00:00Z" },
            });

            var svc = new AuditViewerBackingService();
            var page = svc.ReadRunRows(path);

            Assert.Equal(2, page.TotalRowCount);
            Assert.Equal(2, page.FilteredRowCount);
            Assert.Equal(2, page.Rows.Count);
            Assert.Equal("Move", page.Rows[0].Action);
            Assert.Equal("Skip", page.Rows[1].Action);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadRunRows_InvalidOrMissingPath_FailsClosed()
    {
        var svc = new AuditViewerBackingService();

        Assert.Throws<ArgumentException>(() => svc.ReadRunRows(""));

        var page = svc.ReadRunRows(
            Path.Combine(Path.GetTempPath(), "missing-audit-" + Guid.NewGuid().ToString("N") + ".csv"),
            page: new AuditPage(2, 25));

        Assert.Empty(page.Rows);
        Assert.Equal(0, page.TotalRowCount);
        Assert.Equal(2, page.PageIndex);
        Assert.Equal(25, page.PageSize);
    }

    [Fact]
    public void ReadRunRows_SkipsMalformedCsvAndFiltersByTimestampRange()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-range.csv");
            File.WriteAllLines(path, new[]
            {
                "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
                "C:\\r,a,b,Move,Game,h1,old,2026-04-01T00:00:00Z",
                "\"unterminated",
                "C:\\r,c,d,Move,Game,h2,inside,2026-04-10T00:00:00Z",
                "C:\\r,e,f,Move,Game,h3,new,2026-05-01T00:00:00Z",
                "C:\\r,g,h,Move,Game,h4,bad-date,not-a-date"
            });

            var svc = new AuditViewerBackingService();
            var page = svc.ReadRunRows(
                path,
                new AuditRunFilter(
                    FromUtc: new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero),
                    ToUtc: new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero)));

            Assert.Equal(4, page.TotalRowCount);
            Assert.Single(page.Rows);
            Assert.Equal("inside", page.Rows[0].Reason);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadRunRows_FilterByOutcome_Works()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-two.csv");
            WriteAuditCsv(path, new[]
            {
                new[] { "C:\\r", "a", "b", "Move", "", "", "", "2026-04-01T00:00:00Z" },
                new[] { "C:\\r", "c", "d", "Skip", "", "", "", "2026-04-02T00:00:00Z" },
                new[] { "C:\\r", "e", "f", "Move", "", "", "", "2026-04-03T00:00:00Z" },
            });

            var svc = new AuditViewerBackingService();
            var page = svc.ReadRunRows(path, new AuditRunFilter(Outcome: "Move"));

            Assert.Equal(3, page.TotalRowCount);
            Assert.Equal(2, page.FilteredRowCount);
            Assert.All(page.Rows, r => Assert.Equal("Move", r.Action));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadRunRows_Pagination_Works()
    {
        var root = TempRoot();
        try
        {
            var rows = Enumerable.Range(0, 7)
                .Select(i => new[] { "C:\\r", $"a{i}", $"b{i}", "Move", "", "", "", "2026-04-01T00:00:00Z" })
                .ToArray();
            var path = Path.Combine(root, "audit-page.csv");
            WriteAuditCsv(path, rows);

            var svc = new AuditViewerBackingService();
            var p0 = svc.ReadRunRows(path, page: new AuditPage(0, 3));
            var p1 = svc.ReadRunRows(path, page: new AuditPage(1, 3));
            var p2 = svc.ReadRunRows(path, page: new AuditPage(2, 3));

            Assert.Equal(7, p0.TotalRowCount);
            Assert.Equal(3, p0.Rows.Count);
            Assert.Equal(3, p1.Rows.Count);
            Assert.Single(p2.Rows);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadSidecar_ReturnsNullWhenAbsent()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-x.csv");
            WriteAuditCsv(path, Array.Empty<string[]>());

            var svc = new AuditViewerBackingService();
            Assert.Null(svc.ReadSidecar(path));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadSidecar_InvalidOrMalformedSidecar_FailsClosed()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-sidecar-invalid.csv");
            WriteAuditCsv(path, Array.Empty<string[]>());
            var svc = new AuditViewerBackingService();

            Assert.Throws<ArgumentException>(() => svc.ReadSidecar(" "));

            File.WriteAllText(path + ".meta.json", "{not-json");
            var info = svc.ReadSidecar(path);

            Assert.NotNull(info);
            Assert.Equal(0, info!.DeclaredRowCount);
            Assert.False(info.IsSignatureValid);
            Assert.Empty(info.Metadata);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ReadSidecar_ParsesMetadataTypesAndUnverifiedSignature()
    {
        var root = TempRoot();
        try
        {
            var path = Path.Combine(root, "audit-sidecar-types.csv");
            WriteAuditCsv(path, Array.Empty<string[]>());
            File.WriteAllText(path + ".meta.json", """
            {
              "rowCount": 2,
              "status": "completed",
              "verified": true,
              "optional": null,
              "details": { "phase": "move" }
            }
            """);

            var svc = new AuditViewerBackingService();
            var info = svc.ReadSidecar(path);

            Assert.NotNull(info);
            Assert.Equal(2, info!.DeclaredRowCount);
            Assert.False(info.IsSignatureValid);
            Assert.Equal("completed", info.Metadata["status"]);
            Assert.Equal("True", info.Metadata["verified"]);
            Assert.Equal(string.Empty, info.Metadata["optional"]);
            Assert.Contains("phase", info.Metadata["details"], StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Adapter_DoesNotDuplicateAuditCsvParsingOrSigning()
    {
        // Source-inspection pin: the adapter must use AuditCsvParser + AuditSigningService.
        // It must not implement its own quote/HMAC parsing.
        var src = File.ReadAllText(LocateRepoFile("src/Romulus.Infrastructure/Audit/AuditViewerBackingService.cs"));

        Assert.Contains("AuditCsvParser.ParseCsvLine", src, StringComparison.Ordinal);
        Assert.Contains("AuditSigningService", src, StringComparison.Ordinal);
        // Negative checks: must not roll its own primitives.
        Assert.DoesNotContain("HMACSHA256", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new HMAC", src, StringComparison.Ordinal);
    }

    private static string LocateRepoFile(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "src", "Romulus.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
