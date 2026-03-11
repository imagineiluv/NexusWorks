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
    /// Local Windows log streaming connector.
    /// Uses a local PowerShell process to follow a file and expose it as <see cref="IRemoteLogStream"/>.
    /// </summary>
    public sealed class WindowsLocalLogStreamConnector : ILogStreamConnector
    {
        private readonly ILogStreamConnector _sshConnector;

        public WindowsLocalLogStreamConnector(ILogStreamConnector sshConnector)
        {
            _sshConnector = sshConnector ?? throw new ArgumentNullException(nameof(sshConnector));
        }

        public Task<IRemoteLogStream> ConnectAsync(LogStreamOptions options, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.Platform == SessionPlatform.Windows && IsLocalHost(options.Host))
            {
                // Intentionally ignore SSH credentials/port for local.
                return Task.FromResult<IRemoteLogStream>(new WindowsPowerShellProcessLogStream());
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

    internal sealed class WindowsPowerShellProcessLogStream : IRemoteLogStream
    {
        private readonly Process _process;
        private readonly Channel<string> _outputChannel;
        private readonly CancellationTokenSource _shutdown = new();

        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly Task _startupTask;

        private readonly Queue<char> _charQueue = new();
        private readonly object _gate = new();

        public async Task WriteAsync(string command, CancellationToken cancellationToken)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException("PowerShell process has exited.");
            }

            // Ensure startup writes are finished, then serialize all stdin operations.
            await _startupTask.WaitAsync(cancellationToken).ConfigureAwait(false);

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

        public WindowsPowerShellProcessLogStream()
        {
            _outputChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = true
            });

            // Persistent interactive session:
            // -NoExit keeps the process alive; -Command - reads commands from STDIN.
            // Do NOT use -NonInteractive here, because we want stdin/stdout piping.
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -NoExit -Command -",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.SystemDirectory,
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

            // Keep output stream clean: do not inject any host UI strings here.
            // Prompt is rendered in the xterm input line.

            // Improve prompt consistency and reduce noise.
            // IMPORTANT: StreamWriter on StandardInput does not allow concurrent async operations.
            // Serialize startup writes and user writes via _writeLock.
            _startupTask = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _writeLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                try
                {
                    await _process.StandardInput.WriteLineAsync("$global:ErrorActionPreference='Continue'").ConfigureAwait(false);
                    await _process.StandardInput.WriteLineAsync("Set-Location -LiteralPath $env:windir\\System32").ConfigureAwait(false);
                    await _process.StandardInput.FlushAsync().ConfigureAwait(false);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch
            {
                // best-effort; if initialization fails, the shell may still be usable.
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
            // Merge stderr into the same stream with a prefix.
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

            // First, drain any already buffered chars.
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

            // Wait for the next line from PowerShell and enqueue its characters.
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

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try { await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false); } catch { }
                }
            }
            finally
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch { }

                try { _process.OutputDataReceived -= OnOutputDataReceived; } catch { }
                try { _process.ErrorDataReceived -= OnErrorDataReceived; } catch { }
                try { _process.Exited -= OnExited; } catch { }
                try { _process.Dispose(); } catch { }
            }
        }
    }
}
