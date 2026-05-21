using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentTestCoverageGateTests
{
    [Fact]
    public void Evaluate_WhenCommandOutputReportsCoverageBelowMinimum_ReturnsFailedEvaluation()
    {
        var result = new AgentValidationCommandResult(
            Name: "test",
            CommandLine: "dotnet test",
            IsSuccess: true,
            ExitCode: 0,
            StdOut: "Line coverage: 72.49%",
            StdErr: string.Empty,
            Duration: TimeSpan.FromMilliseconds(5),
            IsTimedOut: false,
            IsCancelled: false);

        var evaluation = AgentTestCoverageGate.Evaluate(
            workspaceRootDirectory: Directory.GetCurrentDirectory(),
            validationResults: [result],
            minimumLineCoveragePercent: 80m);

        Assert.True(evaluation.WasCoverageDiscovered);
        Assert.False(evaluation.IsSatisfied);
        Assert.Equal(72.49m, evaluation.LineCoveragePercent);
        Assert.Equal(80m, evaluation.MinimumLineCoveragePercent);
        Assert.Contains("status=failed", evaluation.ToSummary());
    }

    [Fact]
    public void Evaluate_WhenCoberturaReportMeetsMinimum_ReturnsSatisfiedEvaluation()
    {
        var directoryPath = CreateTemporaryDirectory();
        try
        {
            var reportDirectory = Path.Combine(directoryPath, "TestResults", "run-a");
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(
                Path.Combine(reportDirectory, "coverage.cobertura.xml"),
                """
                <coverage line-rate="0.875" branch-rate="0.8" version="1.9">
                </coverage>
                """);

            var evaluation = AgentTestCoverageGate.Evaluate(
                workspaceRootDirectory: directoryPath,
                validationResults: [],
                minimumLineCoveragePercent: 80m);

            Assert.True(evaluation.WasCoverageDiscovered);
            Assert.True(evaluation.IsSatisfied);
            Assert.Equal(87.5m, evaluation.LineCoveragePercent);
            Assert.Contains("TestResults", evaluation.Source);
            Assert.Contains("status=passed", evaluation.ToSummary());
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void EvaluationImplicitOperator_NormalizesLineRateTuple()
    {
        AgentTestCoverageEvaluation evaluation = (0.8125m, 80m, "coverage.cobertura.xml");

        Assert.True(evaluation.IsSatisfied);
        Assert.Equal(81.25m, evaluation.LineCoveragePercent);
        Assert.Equal(80m, evaluation.MinimumLineCoveragePercent);
        Assert.Equal("coverage.cobertura.xml", evaluation.Source);
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"asxrun-coverage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
