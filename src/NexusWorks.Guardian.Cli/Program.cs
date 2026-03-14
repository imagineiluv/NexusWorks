using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Acquisition;
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
            var coordinator = CreateCoordinator(logger);

            var report = await coordinator.RunAsync(request, cts.Token);

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

    private static GuardianRunCoordinator CreateCoordinator(IGuardianLogger logger)
    {
        var comparer = new GuardianFileComparer(
            new JarComparer(), new XmlComparer(), new YamlComparer(), new StatusEvaluator(), logger);
        var comparisonEngine = new GuardianComparisonEngine(
            new ClosedXmlBaselineReader(),
            new BaselineValidator(),
            new FileSystemInventoryScanner(new Sha256HashProvider()),
            new BaselineRuleResolver(),
            comparer,
            logger);
        var reportService = new GuardianReportService(
            new ResultAggregator(),
            new StaticHtmlReportWriter(),
            new ClosedXmlExcelReportWriter());

        return new GuardianRunCoordinator(
            new InputPreparationService(new SftpDownloadService()),
            comparisonEngine,
            reportService);
    }

    private static GuardianRunRequest ResolveRequest(GuardianCliOptions options)
    {
        if (options.UseSample)
        {
            var sample = LocateSampleDataset();
            return GuardianRunRequest.CreateLocal(
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

        return new GuardianRunRequest(
            CreateInputSource(
                InputSide.Current,
                options.CurrentMode,
                options.CurrentRootPath!,
                options.CurrentSftpHost,
                options.CurrentSftpPort,
                options.CurrentSftpUsername,
                options.CurrentSftpAuthenticationMode,
                options.CurrentSftpPasswordEnv,
                options.CurrentSftpPrivateKeyPath,
                options.CurrentSftpPrivateKeyPassphraseEnv,
                options.CurrentSftpRemoteRoot,
                options.CurrentSftpFingerprint,
                options.CurrentSftpClearTarget),
            CreateInputSource(
                InputSide.Patch,
                options.PatchMode,
                options.PatchRootPath!,
                options.PatchSftpHost,
                options.PatchSftpPort,
                options.PatchSftpUsername,
                options.PatchSftpAuthenticationMode,
                options.PatchSftpPasswordEnv,
                options.PatchSftpPrivateKeyPath,
                options.PatchSftpPrivateKeyPassphraseEnv,
                options.PatchSftpRemoteRoot,
                options.PatchSftpFingerprint,
                options.PatchSftpClearTarget),
            options.BaselinePath!,
            options.OutputRootPath!,
            options.ReportTitle ?? "Guardian CLI Run");
    }

    private static InputSourceRequest CreateInputSource(
        InputSide side,
        InputSourceMode mode,
        string localRootPath,
        string? host,
        int port,
        string? username,
        SftpAuthenticationMode authenticationMode,
        string? passwordEnv,
        string? privateKeyPath,
        string? privateKeyPassphraseEnv,
        string? remoteRoot,
        string? fingerprint,
        bool clearTarget)
    {
        if (mode == InputSourceMode.Local)
        {
            return new InputSourceRequest(side, mode, localRootPath);
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException($"--{side.ToString().ToLowerInvariant()}-sftp-host is required when --{side.ToString().ToLowerInvariant()}-mode sftp is used.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException($"--{side.ToString().ToLowerInvariant()}-sftp-username is required when --{side.ToString().ToLowerInvariant()}-mode sftp is used.");
        }

        if (string.IsNullOrWhiteSpace(remoteRoot))
        {
            throw new ArgumentException($"--{side.ToString().ToLowerInvariant()}-sftp-remote-root is required when --{side.ToString().ToLowerInvariant()}-mode sftp is used.");
        }

        var optionPrefix = $"--{side.ToString().ToLowerInvariant()}-sftp";
        string? password = null;
        string? privateKeyPassphrase = null;

        switch (authenticationMode)
        {
            case SftpAuthenticationMode.Password:
                if (string.IsNullOrWhiteSpace(passwordEnv))
                {
                    throw new ArgumentException($"{optionPrefix}-password-env is required when {optionPrefix}-auth password is used.");
                }

                password = ReadRequiredEnvironmentVariable(passwordEnv);
                break;
            case SftpAuthenticationMode.PrivateKey:
                if (string.IsNullOrWhiteSpace(privateKeyPath))
                {
                    throw new ArgumentException($"{optionPrefix}-private-key is required when {optionPrefix}-auth private-key is used.");
                }

                privateKeyPassphrase = ReadOptionalEnvironmentVariable(privateKeyPassphraseEnv);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(authenticationMode), authenticationMode, null);
        }

        return new InputSourceRequest(
            side,
            mode,
            localRootPath,
            new SftpInputRequest(
                host!,
                port,
                username!,
                remoteRoot!,
                password,
                fingerprint,
                clearTarget,
                authenticationMode,
                privateKeyPath,
                privateKeyPassphrase));
    }

    private static string ReadRequiredEnvironmentVariable(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Environment variable '{variableName}' is not set or empty.");
        }

        return value;
    }

    private static string? ReadOptionalEnvironmentVariable(string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        return ReadRequiredEnvironmentVariable(variableName);
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
  dotnet run --project src/NexusWorks.Guardian.Cli -- --current-mode sftp --current-root <path> --current-sftp-host <host> --current-sftp-username <user> --current-sftp-auth password --current-sftp-password-env <env> --current-sftp-remote-root <path> --patch-root <path> --baseline <path> --output-root <path>
  dotnet run --project src/NexusWorks.Guardian.Cli -- --current-mode sftp --current-root <path> --current-sftp-host <host> --current-sftp-username <user> --current-sftp-auth private-key --current-sftp-private-key <path> --current-sftp-private-key-passphrase-env <env> --current-sftp-remote-root <path> --patch-root <path> --baseline <path> --output-root <path>

Options:
  --sample                 Use sample/guardian/current, patch, baseline.xlsx, and output.
  --current-root <path>    Current local root or SFTP download target.
  --patch-root <path>      Patch local root or SFTP download target.
  --current-mode <mode>    local or sftp. Default: local.
  --patch-mode <mode>      local or sftp. Default: local.
  --current-sftp-host <h>  Current-side SFTP host.
  --current-sftp-port <n>  Current-side SFTP port. Default: 22.
  --current-sftp-username <u> Current-side SFTP username.
  --current-sftp-auth <mode> password or private-key. Default: password.
  --current-sftp-password-env <name> Env var containing current-side SFTP password.
  --current-sftp-private-key <path> Path to current-side private key file.
  --current-sftp-private-key-passphrase-env <name> Env var containing current-side private key passphrase.
  --current-sftp-remote-root <path> Current-side remote root path.
  --current-sftp-fingerprint <fp> Expected current-side host fingerprint (optional).
  --current-sftp-clear-target Clear the current local target before download.
  --patch-sftp-host <h>    Patch-side SFTP host.
  --patch-sftp-port <n>    Patch-side SFTP port. Default: 22.
  --patch-sftp-username <u> Patch-side SFTP username.
  --patch-sftp-auth <mode> password or private-key. Default: password.
  --patch-sftp-password-env <name> Env var containing patch-side SFTP password.
  --patch-sftp-private-key <path> Path to patch-side private key file.
  --patch-sftp-private-key-passphrase-env <name> Env var containing patch-side private key passphrase.
  --patch-sftp-remote-root <path> Patch-side remote root path.
  --patch-sftp-fingerprint <fp> Expected patch-side host fingerprint (optional).
  --patch-sftp-clear-target Clear the patch local target before download.
  --baseline <path>        Baseline workbook path.
  --output-root <path>     Output root directory.
  --report-title <title>   Report title override.
  --help                   Show this help text.
""");
    }

    private sealed record SampleDatasetInfo(
        string RootPath,
        string CurrentRootPath,
        string PatchRootPath,
        string BaselinePath,
        string OutputRootPath);

    private sealed record GuardianCliOptions(
        bool UseSample,
        bool ShowHelp,
        InputSourceMode CurrentMode,
        InputSourceMode PatchMode,
        string? CurrentRootPath,
        string? PatchRootPath,
        string? CurrentSftpHost,
        int CurrentSftpPort,
        string? CurrentSftpUsername,
        SftpAuthenticationMode CurrentSftpAuthenticationMode,
        string? CurrentSftpPasswordEnv,
        string? CurrentSftpPrivateKeyPath,
        string? CurrentSftpPrivateKeyPassphraseEnv,
        string? CurrentSftpRemoteRoot,
        string? CurrentSftpFingerprint,
        bool CurrentSftpClearTarget,
        string? PatchSftpHost,
        int PatchSftpPort,
        string? PatchSftpUsername,
        SftpAuthenticationMode PatchSftpAuthenticationMode,
        string? PatchSftpPasswordEnv,
        string? PatchSftpPrivateKeyPath,
        string? PatchSftpPrivateKeyPassphraseEnv,
        string? PatchSftpRemoteRoot,
        string? PatchSftpFingerprint,
        bool PatchSftpClearTarget,
        string? BaselinePath,
        string? OutputRootPath,
        string? ReportTitle)
    {
        public static GuardianCliOptions Parse(IReadOnlyList<string> args)
        {
            var useSample = false;
            var showHelp = false;
            var currentMode = InputSourceMode.Local;
            var patchMode = InputSourceMode.Local;
            string? currentRoot = null;
            string? patchRoot = null;
            string? currentSftpHost = null;
            var currentSftpPort = 22;
            string? currentSftpUsername = null;
            var currentSftpAuthenticationMode = SftpAuthenticationMode.Password;
            string? currentSftpPasswordEnv = null;
            string? currentSftpPrivateKeyPath = null;
            string? currentSftpPrivateKeyPassphraseEnv = null;
            string? currentSftpRemoteRoot = null;
            string? currentSftpFingerprint = null;
            var currentSftpClearTarget = false;
            string? patchSftpHost = null;
            var patchSftpPort = 22;
            string? patchSftpUsername = null;
            var patchSftpAuthenticationMode = SftpAuthenticationMode.Password;
            string? patchSftpPasswordEnv = null;
            string? patchSftpPrivateKeyPath = null;
            string? patchSftpPrivateKeyPassphraseEnv = null;
            string? patchSftpRemoteRoot = null;
            string? patchSftpFingerprint = null;
            var patchSftpClearTarget = false;
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
                    case "--current-mode":
                        currentMode = ParseMode(ReadValue(args, ref index, arg), arg);
                        break;
                    case "--current-sftp-host":
                        currentSftpHost = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-port":
                        currentSftpPort = ParsePort(ReadValue(args, ref index, arg), arg);
                        break;
                    case "--current-sftp-username":
                        currentSftpUsername = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-auth":
                        currentSftpAuthenticationMode = ParseAuthenticationMode(ReadValue(args, ref index, arg), arg);
                        break;
                    case "--current-sftp-password-env":
                        currentSftpPasswordEnv = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-private-key":
                        currentSftpPrivateKeyPath = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-private-key-passphrase-env":
                        currentSftpPrivateKeyPassphraseEnv = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-remote-root":
                        currentSftpRemoteRoot = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-fingerprint":
                        currentSftpFingerprint = ReadValue(args, ref index, arg);
                        break;
                    case "--current-sftp-clear-target":
                        currentSftpClearTarget = true;
                        break;
                    case "--patch-root":
                        patchRoot = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-mode":
                        patchMode = ParseMode(ReadValue(args, ref index, arg), arg);
                        break;
                    case "--patch-sftp-host":
                        patchSftpHost = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-port":
                        patchSftpPort = ParsePort(ReadValue(args, ref index, arg), arg);
                        break;
                    case "--patch-sftp-username":
                        patchSftpUsername = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-auth":
                        patchSftpAuthenticationMode = ParseAuthenticationMode(ReadValue(args, ref index, arg), arg);
                        break;
                    case "--patch-sftp-password-env":
                        patchSftpPasswordEnv = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-private-key":
                        patchSftpPrivateKeyPath = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-private-key-passphrase-env":
                        patchSftpPrivateKeyPassphraseEnv = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-remote-root":
                        patchSftpRemoteRoot = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-fingerprint":
                        patchSftpFingerprint = ReadValue(args, ref index, arg);
                        break;
                    case "--patch-sftp-clear-target":
                        patchSftpClearTarget = true;
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

            return new GuardianCliOptions(
                useSample,
                showHelp,
                currentMode,
                patchMode,
                currentRoot,
                patchRoot,
                currentSftpHost,
                currentSftpPort,
                currentSftpUsername,
                currentSftpAuthenticationMode,
                currentSftpPasswordEnv,
                currentSftpPrivateKeyPath,
                currentSftpPrivateKeyPassphraseEnv,
                currentSftpRemoteRoot,
                currentSftpFingerprint,
                currentSftpClearTarget,
                patchSftpHost,
                patchSftpPort,
                patchSftpUsername,
                patchSftpAuthenticationMode,
                patchSftpPasswordEnv,
                patchSftpPrivateKeyPath,
                patchSftpPrivateKeyPassphraseEnv,
                patchSftpRemoteRoot,
                patchSftpFingerprint,
                patchSftpClearTarget,
                baseline,
                outputRoot,
                reportTitle);
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

        private static InputSourceMode ParseMode(string rawValue, string optionName)
            => rawValue.Trim().ToLowerInvariant() switch
            {
                "local" => InputSourceMode.Local,
                "sftp" => InputSourceMode.Sftp,
                _ => throw new ArgumentException($"Unsupported value '{rawValue}' for '{optionName}'. Use local or sftp."),
            };

        private static SftpAuthenticationMode ParseAuthenticationMode(string rawValue, string optionName)
            => rawValue.Trim().ToLowerInvariant() switch
            {
                "password" => SftpAuthenticationMode.Password,
                "private-key" or "privatekey" => SftpAuthenticationMode.PrivateKey,
                _ => throw new ArgumentException($"Unsupported value '{rawValue}' for '{optionName}'. Use password or private-key."),
            };

        private static int ParsePort(string rawValue, string optionName)
        {
            if (!int.TryParse(rawValue, out var port) || port <= 0)
            {
                throw new ArgumentException($"Invalid port '{rawValue}' for '{optionName}'.");
            }

            return port;
        }
    }
}
