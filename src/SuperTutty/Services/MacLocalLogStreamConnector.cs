using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SuperTutty.Services
{
    /// <summary>
    /// Local macOS/Linux log streaming connector.
    /// Uses a local shell process to follow a file and expose it as <see cref="IRemoteLogStream"/>.
    /// </summary>
    public sealed class MacLocalLogStreamConnector : ILogStreamConnector
    {
        private readonly ILogStreamConnector _sshConnector;

        public MacLocalLogStreamConnector(ILogStreamConnector sshConnector)
        {
            _sshConnector = sshConnector ?? throw new ArgumentNullException(nameof(sshConnector));
        }

        public Task<IRemoteLogStream> ConnectAsync(LogStreamOptions options, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.Platform == SessionPlatform.Linux && IsLocalHost(options.Host))
            {
                // Intentionally ignore SSH credentials/port for local.
                return Task.FromResult<IRemoteLogStream>(new UnixShellProcessLogStream());
            }

            return _sshConnector.ConnectAsync(options, cancellationToken);
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
    }

    internal sealed class UnixShellProcessLogStream : IRemoteLogStream
    {
        private readonly Process _process;
        private readonly Channel<string> _outputChannel;
        private readonly CancellationTokenSource _shutdown = new();

        private readonly SemaphoreSlim _writeLock = new(1, 1);

        private readonly Queue<char> _charQueue = new();
        private readonly object _gate = new();

        public UnixShellProcessLogStream()
        {
            _outputChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = true
            });

            // Non-interactive shell (no prompt). We only need to send one long-running command (tail -F).
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "--noprofile --norc",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Exited += OnExited;

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public async Task WriteAsync(string command, CancellationToken cancellationToken)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException("Shell process has exited.");
            }

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _process.StandardInput.WriteLineAsync(command ?? string.Empty).WaitAsync(cancellationToken).ConfigureAwait(false);
                await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            _outputChannel.Writer.TryWrite(e.Data + "\n");
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            _outputChannel.Writer.TryWrite(e.Data + "\n");
        }

        private void OnExited(object? sender, EventArgs e)
        {
            _outputChannel.Writer.TryComplete();
        }

        public Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            return ReadLineCoreAsync(cancellationToken);
        }

        private async Task<string?> ReadLineCoreAsync(CancellationToken cancellationToken)
        {
            while (await _outputChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_outputChannel.Reader.TryRead(out var line))
                {
                    return line;
                }
            }

            return null;
        }

        public Task<int> ReadAsync(char[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            return ReadCoreAsync(buffer, index, count, cancellationToken);
        }

        private async Task<int> ReadCoreAsync(char[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (_charQueue.Count > 0)
                {
                    var n = 0;
                    while (n < count && _charQueue.Count > 0)
                    {
                        buffer[index + n] = _charQueue.Dequeue();
                        n++;
                    }
                    return n;
                }
            }

            var line = await ReadLineCoreAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                return 0;
            }

            lock (_gate)
            {
                foreach (var ch in line)
                {
                    _charQueue.Enqueue(ch);
                }

                var n = 0;
                while (n < count && _charQueue.Count > 0)
                {
                    buffer[index + n] = _charQueue.Dequeue();
                    n++;
                }
                return n;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _shutdown.Cancel();
            }
            catch { }

            try
            {
                if (!_process.HasExited)
                {
                    // Best effort: try to ask the shell to exit.
                    try
                    {
                        await _writeLock.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            await _process.StandardInput.WriteLineAsync("exit").ConfigureAwait(false);
                            await _process.StandardInput.FlushAsync().ConfigureAwait(false);
                        }
                        finally
                        {
                            _writeLock.Release();
                        }
                    }
                    catch { }

                    // If a long-running command is active (e.g., tail -F), the shell won't process 'exit'.
                    // Kill the process to ensure we stop streaming.
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch { }
                }
            }
            catch { }

            try { _outputChannel.Writer.TryComplete(); } catch { }

            try { _process.Dispose(); } catch { }
            try { _shutdown.Dispose(); } catch { }
            try { _writeLock.Dispose(); } catch { }
        }
    }
}
