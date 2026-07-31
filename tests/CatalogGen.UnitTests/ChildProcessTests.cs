using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// The budget a spawned process gets, and what happens when it outruns it.
/// </summary>
/// <remarks>
/// This engine spawns two things — the descriptor worker, which constructs third-party analyzers,
/// and MSBuild — and both used to be awaited with an unbounded <c>WaitForExit</c>. A child that
/// wedges then takes the tool with it: no output, no exit code, and a pipeline that runs until the
/// runner's own timeout kills it with nothing to read. Refusing late is this repository's rule;
/// refusing never is not a variant of it.
/// </remarks>
public sealed class ChildProcessTests : IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("childprocess");

    public void Dispose() => _work.Delete(recursive: true);

    [Fact]
    public void A_child_that_outruns_its_budget_is_stopped_rather_than_awaited_forever()
    {
        using Process process = StartAnMSBuildEvaluation();

        // A millisecond against work that takes hundreds of them. The point is not the number: it is
        // that the wait returns at all, which is what an unbounded WaitForExit does not guarantee.
        Assert.False(ChildProcess.WaitOrKill(process, TimeSpan.FromMilliseconds(1), "the fixture"));

        // Gone rather than merely asked to go. WaitOrKill waits on the kill, so a caller can rely on
        // this — the descriptor worker's caller goes on to delete the files it was reading.
        Assert.True(process.HasExited);
    }

    [Fact]
    public void A_child_that_finishes_inside_its_budget_is_left_alone()
    {
        // The control. Without it the test above would pass against an implementation that killed
        // every child it was given, which is a worse tool than one that waits forever.
        using Process process = StartAnMSBuildEvaluation();

        Assert.True(ChildProcess.WaitOrKill(process, TimeSpan.FromMinutes(2), "the fixture"));
        Assert.Equal(0, process.ExitCode);
    }

    [Theory]
    [InlineData(null, 600)]           // unset
    [InlineData("", 600)]             // set to nothing, which is how a shell spells "unset"
    [InlineData("30", 30)]
    [InlineData("1", 1)]
    [InlineData("0", 600)]            // a budget of zero would kill every child instantly
    [InlineData("-5", 600)]
    [InlineData("thirty", 600)]
    [InlineData("30s", 600)]          // the unit is in the name; the value is a number
    [InlineData("1.5", 600)]
    public void The_budget_can_be_overridden_but_only_with_a_positive_whole_number_of_seconds(
        string? declared, int expectedSeconds)
    {
        // Read through the injected value rather than the environment: a test that set a real
        // environment variable would set it for every test running beside it.
        TimeSpan budget = ChildProcess.Budget(TimeSpan.FromMinutes(10), declared);

        Assert.Equal(expectedSeconds, (int)budget.TotalSeconds);
    }

    [Fact]
    public void A_budget_too_large_for_milliseconds_is_still_a_long_wait()
    {
        // The override accepts any positive int of SECONDS, so 68 years is expressible where
        // WaitForExit takes an int of MILLISECONDS — about 24 days of them. This asserts the
        // outcome, not the arithmetic: whatever the conversion does, the wait must behave like a
        // long one. A negative would not be a short wait; WaitForExit throws on one.
        using Process process = StartAnMSBuildEvaluation();

        Assert.True(ChildProcess.WaitOrKill(process, TimeSpan.FromDays(365), "the fixture"));
    }

    // Real work rather than a sleep: the platforms this runs on spell "wait a bit" differently, and
    // MSBuild evaluating a project is something both this tool and this test project already have.
    // It takes hundreds of milliseconds, which is a comfortable distance from the millisecond budget
    // above and well inside the generous one.
    private Process StartAnMSBuildEvaluation()
    {
        string project = Path.Combine(_work.FullName, "Fixture.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        ProcessStartInfo start = new()
        {
            FileName = DotnetCli.Host(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add("-getProperty:TargetPath");
        start.ArgumentList.Add("-getProperty:TargetFramework");

        return Process.Start(start)!;
    }
}
