using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave 2 — T-W2-COVERAGE-GAP-CHECK pin tests.
/// Hardens the "Top-20 1:1"-claim of plan.yaml: every TOP-N must be covered
/// by at least one task, every covers-reference must point to an existing
/// TOP-N, and every task without covers must be either a prototype, a
/// BONUS feature, or a GOVERNANCE-TASK. Without this guard the plan can
/// silently drift and the strategic mapping erodes.
/// </summary>
public sealed class Wave2CoverageGapTests
{
    private const int TopCount = 20;

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

    private static string PlanText() =>
        File.ReadAllText(LocateRepoFile("docs/plan/strategic-reduction-2026/plan.yaml"));

    private static string TopTwentyText() =>
        File.ReadAllText(LocateRepoFile("docs/plan/strategic-reduction-2026/top-20.md"));

    [Fact]
    public void Top20File_Exists()
    {
        var path = LocateRepoFile("docs/plan/strategic-reduction-2026/top-20.md");
        Assert.True(File.Exists(path), "docs/plan/strategic-reduction-2026/top-20.md is missing.");
    }

    [Fact]
    public void Top20File_DefinesAllTwenty()
    {
        var text = TopTwentyText();
        for (int n = 1; n <= TopCount; n++)
        {
            // Marker: "TOP-N" must appear at least once. The format spec in
            // top-20.md uses "TOP-N:" (heading) but we accept any TOP-N occurrence.
            Assert.True(
                Regex.IsMatch(text, $@"\bTOP-{n}\b"),
                $"top-20.md is missing required marker TOP-{n}.");
        }
    }

    [Fact]
    public void EveryTopN_IsCoveredByAtLeastOneTask()
    {
        var planText = PlanText();
        // Match every "TOP-NN" (1..20) inside `covers:` arrays.
        var coversBlocks = Regex.Matches(planText, @"covers:\s*\[(?<list>[^\]]*)\]");
        var covered = new HashSet<int>();
        foreach (Match m in coversBlocks)
        {
            foreach (Match tm in Regex.Matches(m.Groups["list"].Value, @"TOP-(\d+)"))
            {
                if (int.TryParse(tm.Groups[1].Value, out var n))
                    covered.Add(n);
            }
        }

        for (int n = 1; n <= TopCount; n++)
        {
            Assert.True(covered.Contains(n),
                $"No tasks[].covers entry references TOP-{n}. "
                + "Either add a task that covers it, or strike it from top-20.md.");
        }
    }

    [Fact]
    public void EveryCoversReference_PointsToValidTopN()
    {
        var planText = PlanText();
        var coversBlocks = Regex.Matches(planText, @"covers:\s*\[(?<list>[^\]]*)\]");
        foreach (Match m in coversBlocks)
        {
            foreach (Match tm in Regex.Matches(m.Groups["list"].Value, @"TOP-(\d+)"))
            {
                var n = int.Parse(tm.Groups[1].Value);
                Assert.InRange(n, 1, TopCount);
            }
        }
    }

    [Fact]
    public void TasksWithoutCovers_AreEitherPrototypeBonusOrGovernance()
    {
        var planText = PlanText();

        // Split the plan.yaml into per-task blocks. Anchor: "  - id: T-".
        var taskBlocks = Regex.Matches(
            planText,
            @"^\s{2}-\s+id:\s+(?<id>T-[\w-]+).*?(?=^\s{2}-\s+id:\s+T-|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.NotEmpty(taskBlocks);

        var failures = new List<string>();
        foreach (Match block in taskBlocks)
        {
            var id = block.Groups["id"].Value;
            var body = block.Value;

            var coversMatch = Regex.Match(body, @"covers:\s*\[(?<list>[^\]]*)\]");
            if (!coversMatch.Success) continue;
            var coversList = coversMatch.Groups["list"].Value.Trim();
            if (!string.IsNullOrEmpty(coversList) && coversList != "")
                continue; // task has at least one cover entry

            // No covers: must declare exemption.
            bool isPrototype = Regex.IsMatch(body, @"^\s+prototype:\s*true\b", RegexOptions.Multiline);
            bool isBonusTitle = Regex.IsMatch(body, @"^\s+title:\s*""?BONUS", RegexOptions.Multiline);
            bool isBonusFeature = body.Contains("BONUS-FEATURE", StringComparison.Ordinal);
            bool isGovernance = body.Contains("GOVERNANCE-TASK", StringComparison.Ordinal);

            if (!(isPrototype || isBonusTitle || isBonusFeature || isGovernance))
            {
                failures.Add(id);
            }
        }

        Assert.True(
            failures.Count == 0,
            "Tasks without covers must declare an exemption "
            + "(prototype:true, title starts with 'BONUS', "
            + "BONUS-FEATURE in description, or GOVERNANCE-TASK in description). "
            + "Offenders: " + string.Join(", ", failures));
    }
}
