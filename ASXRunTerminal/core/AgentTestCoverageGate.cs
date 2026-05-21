using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ASXRunTerminal.Core;

internal readonly record struct AgentTestCoverageEvaluation(
    bool WasCoverageDiscovered,
    bool IsSatisfied,
    decimal? LineCoveragePercent,
    decimal MinimumLineCoveragePercent,
    string Source)
{
    public static AgentTestCoverageEvaluation NotDiscovered(decimal minimumLineCoveragePercent)
    {
        return new AgentTestCoverageEvaluation(
            WasCoverageDiscovered: false,
            IsSatisfied: true,
            LineCoveragePercent: null,
            MinimumLineCoveragePercent: minimumLineCoveragePercent,
            Source: string.Empty);
    }

    public static implicit operator AgentTestCoverageEvaluation(
        (decimal LineCoveragePercent, decimal MinimumLineCoveragePercent, string Source) tuple)
    {
        return Create(
            tuple.LineCoveragePercent,
            tuple.MinimumLineCoveragePercent,
            tuple.Source);
    }

    public static AgentTestCoverageEvaluation Create(
        decimal lineCoveragePercent,
        decimal minimumLineCoveragePercent,
        string source)
    {
        var normalizedLineCoveragePercent = NormalizePercent(lineCoveragePercent);
        var normalizedMinimumLineCoveragePercent = NormalizePercent(minimumLineCoveragePercent);

        return new AgentTestCoverageEvaluation(
            WasCoverageDiscovered: true,
            IsSatisfied: normalizedLineCoveragePercent >= normalizedMinimumLineCoveragePercent,
            LineCoveragePercent: normalizedLineCoveragePercent,
            MinimumLineCoveragePercent: normalizedMinimumLineCoveragePercent,
            Source: string.IsNullOrWhiteSpace(source) ? "<desconhecida>" : source.Trim());
    }

    public string ToSummary()
    {
        if (!WasCoverageDiscovered || LineCoveragePercent is null)
        {
            return
                "cobertura_linear=<nao encontrada>; " +
                $"minimo={FormatPercent(MinimumLineCoveragePercent)}; status=not-found.";
        }

        return
            $"cobertura_linear={FormatPercent(LineCoveragePercent.Value)}; " +
            $"minimo={FormatPercent(MinimumLineCoveragePercent)}; " +
            $"fonte={Source}; " +
            $"status={(IsSatisfied ? "passed" : "failed")}.";
    }

    internal static decimal NormalizePercent(decimal value)
    {
        return value is >= 0m and <= 1m
            ? decimal.Round(value * 100m, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatPercent(decimal value)
    {
        return $"{value.ToString("0.00", CultureInfo.InvariantCulture)}%";
    }
}

internal static class AgentTestCoverageGate
{
    public const decimal DefaultMinimumLineCoveragePercent = 80m;

    private static readonly Regex[] TextCoveragePatterns =
    [
        new(
            @"(?im)\bline\s+coverage\s*[:=]\s*(?<value>\d+(?:[\.,]\d+)?)\s*%",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(
            @"(?im)\blines?\s*[:=]\s*(?<value>\d+(?:[\.,]\d+)?)\s*%",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(
            @"(?im)\bline-rate\s*=\s*[""'](?<value>\d+(?:[\.,]\d+)?)[""']",
            RegexOptions.Compiled | RegexOptions.CultureInvariant)
    ];

    public static AgentTestCoverageEvaluation Evaluate(
        string workspaceRootDirectory,
        IReadOnlyList<AgentValidationCommandResult> validationResults,
        decimal minimumLineCoveragePercent = DefaultMinimumLineCoveragePercent,
        DateTimeOffset? minimumReportLastWriteTimeUtc = null)
    {
        var normalizedMinimumLineCoveragePercent =
            AgentTestCoverageEvaluation.NormalizePercent(minimumLineCoveragePercent);
        var observations = new List<AgentTestCoverageObservation>();

        foreach (var result in validationResults ?? Array.Empty<AgentValidationCommandResult>())
        {
            AddTextCoverageObservations(result.StdOut, $"{result.Name} stdout", observations);
            AddTextCoverageObservations(result.StdErr, $"{result.Name} stderr", observations);
        }

        foreach (var reportPath in DiscoverCoverageReportFiles(
            workspaceRootDirectory,
            minimumReportLastWriteTimeUtc))
        {
            if (TryReadCoberturaLineCoveragePercent(reportPath, out var lineCoveragePercent))
            {
                observations.Add((lineCoveragePercent, Path.GetRelativePath(workspaceRootDirectory, reportPath)));
            }
        }

        if (observations.Count == 0)
        {
            return AgentTestCoverageEvaluation.NotDiscovered(normalizedMinimumLineCoveragePercent);
        }

        var lowestCoverage = observations
            .OrderBy(static observation => observation.LineCoveragePercent)
            .ThenBy(static observation => observation.Source, StringComparer.OrdinalIgnoreCase)
            .First();

        return AgentTestCoverageEvaluation.Create(
            lowestCoverage.LineCoveragePercent,
            normalizedMinimumLineCoveragePercent,
            lowestCoverage.Source);
    }

    private static void AddTextCoverageObservations(
        string text,
        string source,
        List<AgentTestCoverageObservation> observations)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var pattern in TextCoveragePatterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                if (TryParseDecimal(match.Groups["value"].Value, out var lineCoveragePercent))
                {
                    observations.Add((lineCoveragePercent, source));
                }
            }
        }
    }

    private static IReadOnlyList<string> DiscoverCoverageReportFiles(
        string workspaceRootDirectory,
        DateTimeOffset? minimumReportLastWriteTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory)
            || !Directory.Exists(workspaceRootDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory
                .EnumerateFiles(workspaceRootDirectory, "*.xml", SearchOption.AllDirectories)
                .Where(IsCoverageReportFile)
                .Where(static path => !IsIgnoredDirectoryPath(path))
                .Where(path => WasWrittenAfter(path, minimumReportLastWriteTimeUtc))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsCoverageReportFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals("coverage.cobertura.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("cobertura-coverage.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".cobertura.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool WasWrittenAfter(
        string path,
        DateTimeOffset? minimumReportLastWriteTimeUtc)
    {
        if (minimumReportLastWriteTimeUtc is null)
        {
            return true;
        }

        try
        {
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            return lastWriteTimeUtc >= minimumReportLastWriteTimeUtc.Value.UtcDateTime.AddSeconds(-2);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or FileNotFoundException)
        {
            return false;
        }
    }

    private static bool TryReadCoberturaLineCoveragePercent(
        string reportPath,
        out decimal lineCoveragePercent)
    {
        lineCoveragePercent = 0m;

        try
        {
            var document = XDocument.Load(reportPath);
            var root = document.Root;
            if (root is null
                || !string.Equals(root.Name.LocalName, "coverage", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (TryParseDecimal(root.Attribute("line-rate")?.Value, out var lineRate))
            {
                lineCoveragePercent = AgentTestCoverageEvaluation.NormalizePercent(lineRate);
                return true;
            }

            if (TryParseDecimal(root.Attribute("lines-covered")?.Value, out var linesCovered)
                && TryParseDecimal(root.Attribute("lines-valid")?.Value, out var linesValid)
                && linesValid > 0m)
            {
                lineCoveragePercent = decimal.Round(
                    linesCovered / linesValid * 100m,
                    2,
                    MidpointRounding.AwayFromZero);
                return true;
            }

            return false;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or FileNotFoundException
            or XmlException
            or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryParseDecimal(string? rawValue, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return decimal.TryParse(
            rawValue.Trim().Replace(",", ".", StringComparison.Ordinal),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool IsIgnoredDirectoryPath(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(static segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct AgentTestCoverageObservation(
        decimal LineCoveragePercent,
        string Source)
    {
        public static implicit operator AgentTestCoverageObservation(
            (decimal LineCoveragePercent, string Source) tuple)
        {
            return new AgentTestCoverageObservation(
                AgentTestCoverageEvaluation.NormalizePercent(tuple.LineCoveragePercent),
                tuple.Source);
        }
    }
}
