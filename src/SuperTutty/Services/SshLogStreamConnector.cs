using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace SuperTutty.Services
{
    public class SshLogStreamConnector : ILogStreamConnector
    {
        public SshLogStreamConnector()
        {
        }

        public Task<IRemoteLogStream> ConnectAsync(LogStreamOptions options, CancellationToken cancellationToken)
        {
            var client = new SshClient(options.Host, options.Port, options.Username, options.Password);

            // Enable KeepAlive to detect dropped connections and prevent timeouts
            client.KeepAliveInterval = TimeSpan.FromSeconds(10);

            return Task.Run<IRemoteLogStream>(() =>
            {
                client.Connect();
                var stream = client.CreateShellStream("vt100", 80, 24, 800, 600, 1024);
                if (stream == null)
                {
                    client.Dispose();
                    throw new IOException("Failed to open remote shell stream.");
                }

                return (IRemoteLogStream)new SshRemoteLogStream(client, stream);
            }, cancellationToken);
        }
    }

    public sealed class SshRemoteLogStream : IRemoteLogStream
    {
        private readonly SshClient _client;
        private readonly ShellStream _shellStream;
        private readonly StreamReader _reader;

        public SshRemoteLogStream(SshClient client, ShellStream shellStream)
        {
            _client = client;
            _shellStream = shellStream;
            _reader = new StreamReader(shellStream);
        }

        public Task WriteAsync(string command, CancellationToken cancellationToken)
        {
            return Task.Run(() => _shellStream.WriteLine(command), cancellationToken);
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            return await _reader.ReadLineAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ReadAsync(char[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            // StreamReader.ReadAsync(char[], int, int) does not support CancellationToken directly in base stream reader.
            // Using WaitAsync to respect cancellation token.
            var task = _reader.ReadAsync(buffer, index, count);
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                _shellStream.Dispose();
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                }

                _client.Dispose();
                _reader.Dispose();
            }
            catch (Exception)
            {
                // Ignore errors during disposal
            }
            return ValueTask.CompletedTask;
        }
    }
}
