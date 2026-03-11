using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperTutty.Analyzers;
using SuperTutty.Models;
using SuperTutty.Parsers;
using SuperTutty.Services.Drain;

namespace SuperTutty.Services
{
    public interface ILogStreamService
    {
        event EventHandler<string>? LiveLogReceived;
        event EventHandler<Exception>? StreamError;

        Task StartStreamingAsync(string logFilePath, CancellationToken cancellationToken);

        Task StartStreamingAsync(string logFilePath, LogStreamFilterOptions? filter, CancellationToken cancellationToken);
    }

    public interface ILogStreamConnector
    {
        Task<IRemoteLogStream> ConnectAsync(LogStreamOptions options, CancellationToken cancellationToken);
    }

    public interface IRemoteLogStream : IAsyncDisposable
    {
        Task WriteAsync(string command, CancellationToken cancellationToken);

        Task<string?> ReadLineAsync(CancellationToken cancellationToken);

        Task<int> ReadAsync(char[] buffer, int index, int count, CancellationToken cancellationToken);
    }

    public class LogStreamService : ILogStreamService
    {
        private readonly ILogStreamConnector _connector;
        private readonly IOptions<LogStreamOptions> _options;
        private readonly ILogger<LogStreamService> _logger;
        private readonly TransactionAnalyzer _transactionAnalyzer;
        private readonly EquipmentAnalyzer _equipmentAnalyzer;
        private readonly ProcessLogParser _processParser;
        private readonly EquipmentLogParser _equipmentParser;
        private readonly ILogPersistence _logPersistence;
        private readonly DrainLogParser _drainLogParser;

        public event EventHandler<string>? LiveLogReceived;
        public event EventHandler<Exception>? StreamError;

        public LogStreamService(
            ILogStreamConnector connector,
            IOptions<LogStreamOptions> options,
            ILogger<LogStreamService> logger,
            TransactionAnalyzer transactionAnalyzer,
            EquipmentAnalyzer equipmentAnalyzer,
            ILogPersistence logPersistence,
            DrainLogParser drainLogParser)
        {
            _connector = connector;
            _options = options;
            _logger = logger;
            _transactionAnalyzer = transactionAnalyzer;
            _equipmentAnalyzer = equipmentAnalyzer;
            _logPersistence = logPersistence;
            _drainLogParser = drainLogParser;
            _processParser = new ProcessLogParser();
            _equipmentParser = new EquipmentLogParser();
        }

        public Task StartStreamingAsync(string logFilePath, CancellationToken cancellationToken)
            => StartStreamingAsync(logFilePath, filter: null, cancellationToken);

        public async Task StartStreamingAsync(string logFilePath, LogStreamFilterOptions? filter, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new ArgumentException("Log file path must be provided", nameof(logFilePath));
            }

            var options = _options.Value;
            EnsureValidOptions(options);

            var attempt = 0;
            while (!cancellationToken.IsCancellationRequested && attempt <= options.MaxReconnectAttempts)
            {
                try
                {
                    await StreamOnceAsync(logFilePath, options, filter, cancellationToken).ConfigureAwait(false);
                    attempt = 0; // reset after success
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    attempt++;
                    StreamError?.Invoke(this, ex);
                    _logger.LogError(ex, "Log streaming failed on attempt {Attempt}", attempt);

                    if (attempt > options.MaxReconnectAttempts)
                    {
                        _logger.LogWarning("Max reconnect attempts reached; stopping stream attempts.");
                        return;
                    }

                    var backoff = CalculateBackoff(options, attempt);
                    _logger.LogInformation("Retrying log stream connection in {Delay} (attempt {Attempt})", backoff, attempt);
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task StreamOnceAsync(string logFilePath, LogStreamOptions options, LogStreamFilterOptions? filter, CancellationToken cancellationToken)
        {
            await using var stream = await _connector.ConnectAsync(options, cancellationToken).ConfigureAwait(false);

            // Some connectors (notably SSH shell streams) may emit login banners / prompts immediately on connect.
            // Those lines can be buffered and then show up as the first reads in ReadLoopAsync.
            // Drain that initial noise before we send the follow command.
            await DrainPreCommandOutputAsync(stream, cancellationToken).ConfigureAwait(false);

            var followCommand = BuildFollowCommand(options, logFilePath);
            await stream.WriteAsync(followCommand, cancellationToken).ConfigureAwait(false);
            await ReadLoopAsync(stream, cancellationToken, followCommand, filter).ConfigureAwait(false);
        }

        private static async Task DrainPreCommandOutputAsync(IRemoteLogStream stream, CancellationToken cancellationToken)
        {
            // Best-effort: read any already-buffered lines with a short quiet timeout.
            // This avoids treating SSH banners/prompts as log lines.
            const int maxLinesToDrain = 50;
            var overallDeadline = DateTime.UtcNow.AddSeconds(2);
            var quietTimeout = TimeSpan.FromMilliseconds(150);

            for (var i = 0; i < maxLinesToDrain && DateTime.UtcNow < overallDeadline; i++)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(quietTimeout);

                try
                {
                    var line = await stream.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                    if (line == null)
                    {
                        return;
                    }

                    // Discard the line; it's pre-command noise.
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // No full line arrived within the quiet window; assume stream is clean.
                    return;
                }
            }
        }

        private static string BuildFollowCommand(LogStreamOptions options, string logFilePath)
        {
            // When running on Windows (Local), we stream via PowerShell, not via SSH.
            // Keep the command minimal and resilient.
            if (options.Platform == SessionPlatform.Windows && IsLocalHost(options.Host))
            {
                var escaped = logFilePath.Replace("'", "''");
                return $"Get-Content -LiteralPath '{escaped}' -Tail 0 -Wait";
            }

            return $"tail -n 0 -F {logFilePath}";
        }

        private static bool IsLocalHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            var normalized = host.Trim();
            return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "::1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, ".", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ReadLoopAsync(IRemoteLogStream stream, CancellationToken cancellationToken, string? commandToFilter, LogStreamFilterOptions? filter)
        {
            var predicate = CreateLinePredicate(filter);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                {
                    _logger.LogWarning("Remote log stream closed unexpectedly.");
                    return;
                }

                if (IsCommandEcho(line, commandToFilter))
                {
                    continue;
                }

                if (!predicate(line))
                {
                    continue;
                }

                LiveLogReceived?.Invoke(this, line);
                ProcessLine(line);
            }
        }

        private static Func<string, bool> CreateLinePredicate(LogStreamFilterOptions? filter)
        {
            if (filter == null || !filter.HasAnyFilter() || filter.Kind == LogStreamFilterKind.None)
            {
                return static _ => true;
            }

            var includePatterns = LogStreamFilterPatternParser.Split(filter.Include);
            var excludePatterns = LogStreamFilterPatternParser.Split(filter.Exclude);

            var comparison = filter.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            Func<string, bool> includeMatch = static _ => true;
            Func<string, bool> excludeMatch = static _ => false;

            if (includePatterns.Count > 0)
            {
                includeMatch = filter.Kind switch
                {
                    LogStreamFilterKind.Regex => BuildRegexAnyPredicate(includePatterns, filter.IgnoreCase),
                    _ => BuildFixedAnyPredicate(includePatterns, comparison)
                };
            }

            if (excludePatterns.Count > 0)
            {
                excludeMatch = filter.Kind switch
                {
                    LogStreamFilterKind.Regex => BuildRegexAnyPredicate(excludePatterns, filter.IgnoreCase),
                    _ => BuildFixedAnyPredicate(excludePatterns, comparison)
                };
            }

            var invert = filter.InvertMatch && includePatterns.Count > 0;

            return (line) =>
            {
                var included = includeMatch(line);
                if (invert)
                {
                    included = !included;
                }

                if (!included)
                {
                    return false;
                }

                return !excludeMatch(line);
            };
        }

        private static Func<string, bool> BuildRegexAnyPredicate(IReadOnlyList<string> patterns, bool ignoreCase)
        {
            var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
            if (ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            // Guard against catastrophic regex backtracking.
            var regexes = patterns
                .Select(p => new Regex(p, options, matchTimeout: TimeSpan.FromMilliseconds(100)))
                .ToArray();

            return (line) =>
            {
                var value = line ?? string.Empty;
                for (var i = 0; i < regexes.Length; i++)
                {
                    if (regexes[i].IsMatch(value))
                    {
                        return true;
                    }
                }
                return false;
            };
        }

        private static Func<string, bool> BuildFixedAnyPredicate(IReadOnlyList<string> patterns, StringComparison comparison)
        {
            return (line) =>
            {
                var value = line ?? string.Empty;
                for (var i = 0; i < patterns.Count; i++)
                {
                    if (value.IndexOf(patterns[i], comparison) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            };
        }

        private static bool IsCommandEcho(string line, string? commandToFilter)
        {
            if (string.IsNullOrWhiteSpace(commandToFilter))
            {
                return false;
            }

            var trimmedLine = (line ?? string.Empty).Trim();
            if (trimmedLine.Length == 0)
            {
                return false;
            }

            // Avoid accidentally dropping log lines that follow the common "[timestamp]" pattern.
            if (trimmedLine.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            var cmd = commandToFilter.Trim();
            if (cmd.Length == 0)
            {
                return false;
            }

            // Typical echoes:
            // - exact command
            // - command preceded by a prompt (e.g., "PS C:\\> <cmd>", "$ <cmd>")
            if (string.Equals(trimmedLine, cmd, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmedLine.EndsWith(cmd, StringComparison.OrdinalIgnoreCase))
            {
                // Heuristic: if it ends with the command and has only a short prompt prefix,
                // treat it as an echo rather than a real log line.
                var prefixLen = trimmedLine.Length - cmd.Length;
                return prefixLen <= 120;
            }

            return false;
        }

        internal void ProcessLine(string line)
        {
            _drainLogParser.ParseLog(line);

            var procEvt = _processParser.Parse(line);
            if (procEvt != null)
            {
                _transactionAnalyzer.OnProcessLog(procEvt);
                _logPersistence.EnqueueProcessLog(procEvt);
                return;
            }

            var eqEvt = _equipmentParser.Parse(line);
            if (eqEvt != null)
            {
                _equipmentAnalyzer.OnEquipmentLog(eqEvt);
                _logPersistence.EnqueueEquipmentLog(eqEvt);
            }
        }

        private static void EnsureValidOptions(LogStreamOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Host))
            {
                throw new InvalidOperationException("Log stream host is not configured.");
            }

            // Local connectors intentionally ignore SSH credentials.
            if (IsLocalHost(options.Host))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(options.Username))
            {
                throw new InvalidOperationException("Log stream username is not configured.");
            }

            if (string.IsNullOrWhiteSpace(options.Password))
            {
                throw new InvalidOperationException("Log stream password is not configured.");
            }
        }

        private static TimeSpan CalculateBackoff(LogStreamOptions options, int attempt)
        {
            var exponential = TimeSpan.FromMilliseconds(options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
            return exponential < options.MaxRetryDelay ? exponential : options.MaxRetryDelay;
        }
    }
}
