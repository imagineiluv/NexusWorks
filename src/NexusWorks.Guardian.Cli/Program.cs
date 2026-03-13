using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Comparison;
using NexusWorks.Guardian.Evaluation;
using NexusWorks.Guardian.Infrastructure;
using NexusWorks.Guardian.Inventory;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Orchestration;
using NexusWorks.Guardian.Reporting;
using NexusWorks.Guardian.RuleResolution;

namespace NexusWorks.Guardian.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Console.Error.WriteLine("\nCancellation requested. Shutting down gracefully...");
            cts.Cancel();
        };

        try
        {
            var options = GuardianCliOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var request = ResolveRequest(options);
            var logger = new ConsoleGuardianLogger();
            var runner = CreateRunner(logger);

            var report = await Task.Run(() => runner.ExecuteAndWriteReports(
                new ComparisonExecutionRequest(
                    request.CurrentRootPath,
                    request.PatchRootPath,
                    request.BaselinePath),
                request.OutputRootPath,
                request.ReportTitle,
                cts.Token), cts.Token);

            PrintSummary(report);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Guardian CLI was cancelled.");
            return 130;
        }
        catch (TimeoutException ex)
        {
            Console.Error.WriteLine($"Guardian CLI timed out: {ex.Message}");
            return 124;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Guardian CLI failed: {ex.Message}");
            return 1;
        }
    }

    private static GuardianExecutionRunner CreateRunner(IGuardianLogger logger)
    {
        var comparer = new GuardianFileComparer(
            new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator(), logger);

        return new GuardianExecutionRunner(
            new GuardianComparisonEngine(
                new ClosedXmlBaselineReader(),
                new BaselineValidator(),
                new FileSystemInventoryScanner(new Sha256HashProvider()),
                new BaselineRuleResolver(),
                comparer,
                logger),
            new GuardianReportService(
                new ResultAggregator(),
                new StaticHtmlReportWriter(),
                new ClosedXmlExcelReportWriter()));
    }

    private static GuardianCliRequest ResolveRequest(GuardianCliOptions options)
    {
        if (options.UseSample)
        {
            var sample = LocateSampleDataset();
            return new GuardianCliRequest(
                sample.CurrentRootPath,
                sample.PatchRootPath,
                sample.BaselinePath,
                options.OutputRootPath ?? sample.OutputRootPath,
                options.ReportTitle ?? "Guardian Sample Dataset");
        }

        if (string.IsNullOrWhiteSpace(options.CurrentRootPath))
        {
            throw new ArgumentException("--current-root is required unless --sample is used.");
        }

        if (string.IsNullOrWhiteSpace(options.PatchRootPath))
        {
            throw new ArgumentException("--patch-root is required unless --sample is used.");
        }

        if (string.IsNullOrWhiteSpace(options.BaselinePath))
        {
            throw new ArgumentException("--baseline is required unless --sample is used.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputRootPath))
        {
            throw new ArgumentException("--output-root is required unless --sample is used.");
        }

        return new GuardianCliRequest(
            options.CurrentRootPath!,
            options.PatchRootPath!,
            options.BaselinePath!,
            options.OutputRootPath!,
            options.ReportTitle ?? "Guardian CLI Run");
    }

    private static SampleDatasetInfo LocateSampleDataset()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var sampleRoot = Path.Combine(directory.FullName, "sample", "guardian");
            var currentRoot = Path.Combine(sampleRoot, "current");
            var patchRoot = Path.Combine(sampleRoot, "patch");
            var baselinePath = Path.Combine(sampleRoot, "baseline.xlsx");
            var outputRoot = Path.Combine(sampleRoot, "output");

            if (Directory.Exists(currentRoot)
                && Directory.Exists(patchRoot)
                && Directory.Exists(outputRoot)
                && File.Exists(baselinePath))
            {
                return new SampleDatasetInfo(sampleRoot, currentRoot, patchRoot, baselinePath, outputRoot);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Failed to locate sample/guardian from the current CLI output path.");
    }

    private static void PrintSummary(ExecutionReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"Execution ID: {report.Summary.ExecutionId}");
        Console.WriteLine($"Report Title: {report.ReportTitle}");
        Console.WriteLine($"Output Directory: {report.Artifacts.OutputDirectory}");
        Console.WriteLine($"HTML Report: {report.Artifacts.HtmlReportPath}");
        Console.WriteLine($"Excel Report: {report.Artifacts.ExcelReportPath}");
        Console.WriteLine($"JSON Result: {report.Artifacts.JsonResultPath}");
        Console.WriteLine($"Log File: {report.Artifacts.LogPath}");
        Console.WriteLine($"Total Items: {report.Summary.TotalFileCount}");

        foreach (var pair in report.Summary.StatusCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Status[{pair.Key}]={pair.Value}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
Guardian CLI

Usage:
  dotnet run --project src/NexusWorks.Guardian.Cli -- --sample
  dotnet run --project src/NexusWorks.Guardian.Cli -- --current-root <path> --patch-root <path> --baseline <path> --output-root <path> [--report-title <title>]

Options:
  --sample                 Use sample/guardian/current, patch, baseline.xlsx, and output.
  --current-root <path>    Current deployment root.
  --patch-root <path>      Patch deployment root.
  --baseline <path>        Baseline workbook path.
  --output-root <path>     Output root directory.
  --report-title <title>   Report title override.
  --help                   Show this help text.
""");
    }

    private sealed record GuardianCliRequest(
        string CurrentRootPath,
        string PatchRootPath,
        string BaselinePath,
        string OutputRootPath,
        string ReportTitle);

    private sealed record SampleDatasetInfo(
        string RootPath,
        string CurrentRootPath,
        string PatchRootPath,
        string BaselinePath,
        string OutputRootPath);

    private sealed record GuardianCliOptions(
        bool UseSample,
        bool ShowHelp,
        string? CurrentRootPath,
        string? PatchRootPath,
        string? BaselinePath,
        string? OutputRootPath,
        string? ReportTitle)
    {
        public static GuardianCliOptions Parse(IReadOnlyList<string> args)
        {
            var useSample = false;
            var showHelp = false;
            string? currentRoot = null;
            string? patchRoot = null;
            string? baseline = null;
            string? outputRoot = null;
            string? reportTitle = null;

            for (var index = 0; index < args.Count; index++)
            {
                var arg = args[index];

                switch (arg)
                {
                    case "--sample":
                        useSample = true;
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    case "--current-root":
                        currentRoot = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-root":
                        patchRoot = ReadValue(args, ref index, arg);
                        break;
                    case "--baseline":
                        baseline = ReadValue(args, ref index, arg);
                        break;
                    case "--output-root":
                        outputRoot = ReadValue(args, ref index, arg);
                        break;
                    case "--report-title":
                        reportTitle = ReadValue(args, ref index, arg);
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{arg}'. Use --help to view supported options.");
                }
            }

            return new GuardianCliOptions(useSample, showHelp, currentRoot, patchRoot, baseline, outputRoot, reportTitle);
        }

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
        {
            if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new ArgumentException($"Missing value for '{optionName}'.");
            }

            index++;
            return args[index];
        }
    }
}
