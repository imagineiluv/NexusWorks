using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SQLite;
using SuperTutty.Analyzers;
using SuperTutty.Models;
using SuperTutty.Services;
using SuperTutty.Services.Drain;
using Xunit;

namespace SuperTutty.Tests;

public class LogPipelineTests
{
    [Fact]
    public async Task LogStreamService_RetriesAndBindsLiveLogs()
    {
        var lines = new List<string>
        {
            "2024-01-01 00:00:00 INFO [TX=TX1] Step=INIT Starting process",
            "2024-01-01 00:00:01 EQUIP [EQ=EQ1] Status=OK Value=5"
        };

        var connector = new FlakyConnector(lines);
        var options = Options.Create(new LogStreamOptions
        {
            Host = "localhost",
            Username = "user",
            Password = "pass",
            BaseRetryDelay = TimeSpan.FromMilliseconds(5),
            MaxRetryDelay = TimeSpan.FromMilliseconds(10),
            MaxReconnectAttempts = 2
        });

        var persistence = new InMemoryPersistence();
        var service = new LogStreamService(
            connector,
            options,
            NullLogger<LogStreamService>.Instance,
            new TransactionAnalyzer(),
            new EquipmentAnalyzer(),
            persistence,
            new DrainLogParser());

        var liveLines = new List<string>();
        service.LiveLogReceived += (_, line) => liveLines.Add(line);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartStreamingAsync("/var/log/app.log", cts.Token);

        Assert.True(connector.Attempts >= 2); // retried after first failure
        Assert.Equal(lines, liveLines);
        Assert.Single(persistence.ProcessLogs);
        Assert.Single(persistence.EquipmentLogs);
    }

    [Fact]
    public void DrainLogParser_RespectsSimilarityThreshold()
    {
        var strictParser = new DrainLogParser(depth: 4, similarityThreshold: 0.8);
        var relaxedParser = new DrainLogParser(depth: 4, similarityThreshold: 0.4);

        var first = "API call user=123 status=ok";
        var second = "API call user=124 status=ok";

        var strictFirst = strictParser.ParseLog(first);
        var strictSecond = strictParser.ParseLog(second);

        Assert.NotEqual(strictFirst.Id, strictSecond.Id);

        var relaxedFirst = relaxedParser.ParseLog(first);
        var relaxedSecond = relaxedParser.ParseLog(second);

        Assert.Equal(relaxedFirst.Id, relaxedSecond.Id);
        Assert.Contains("*", relaxedSecond.Template);
    }

    [Fact]
    public async Task LogDatabase_SupportsFlushAndShutdown()
    {
        var persistence = new LogDatabase(
            NullLogger<LogDatabase>.Instance,
            databasePath: Path.Combine(Path.GetTempPath(), $"logs_{Guid.NewGuid():N}.db3"),
            channelCapacity: 5,
            batchSize: 2,
            idleDelay: TimeSpan.FromMilliseconds(10));

        Task? disposeTask = null;
        try
        {
            for (var i = 0; i < 4; i++)
            {
                persistence.EnqueueProcessLog(new ProcessLogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "INFO",
                    TransactionId = $"TX{i}",
                    Message = "Step=RUN"
                });
            }

            for (var i = 0; i < 3; i++)
            {
                persistence.EnqueueEquipmentLog(new EquipmentLogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EquipmentId = $"EQ{i}",
                    EventType = "Status",
                    Status = "OK"
                });
            }

            using var flushCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await persistence.FlushAsync(flushCts.Token);

            Assert.Equal(4, await persistence.CountProcessLogsAsync());
            Assert.Equal(3, await persistence.CountEquipmentLogsAsync());

            disposeTask = persistence.DisposeAsync().AsTask();
            var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(disposeTask, completed);
        }
        finally
        {
            disposeTask ??= persistence.DisposeAsync().AsTask();
            if (!disposeTask.IsCompleted)
            {
                await disposeTask.ConfigureAwait(false);
            }
        }
    }

    [Fact]
    public async Task LogDatabase_DisposePersistsQueuedEntries()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"dispose_test_{Guid.NewGuid():N}.db3");
        var persistence = new LogDatabase(
            NullLogger<LogDatabase>.Instance,
            databasePath: databasePath,
            channelCapacity: 10,
            batchSize: 5,
            idleDelay: TimeSpan.FromMilliseconds(5));

        persistence.EnqueueProcessLog(new ProcessLogEvent
        {
            Timestamp = DateTime.UtcNow,
            Level = "INFO",
            TransactionId = "TX-DISPOSE",
            Message = "Step=COMMIT"
        });

        persistence.EnqueueEquipmentLog(new EquipmentLogEvent
        {
            Timestamp = DateTime.UtcNow,
            EquipmentId = "EQ-DISPOSE",
            EventType = "Status",
            Status = "OK"
        });

        await persistence.DisposeAsync();

        var verification = new SQLiteAsyncConnection(databasePath);
        try
        {
            var processCount = await verification.Table<ProcessLogEventEntity>().CountAsync();
            var equipmentCount = await verification.Table<EquipmentLogEventEntity>().CountAsync();

            Assert.Equal(1, processCount);
            Assert.Equal(1, equipmentCount);
        }
        finally
        {
            await verification.CloseAsync();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private sealed class InMemoryPersistence : ILogPersistence
    {
        public List<ProcessLogEvent> ProcessLogs { get; } = new();
        public List<EquipmentLogEvent> EquipmentLogs { get; } = new();

        public void EnqueueProcessLog(ProcessLogEvent evt)
        {
            ProcessLogs.Add(evt);
        }

        public void EnqueueEquipmentLog(EquipmentLogEvent evt)
        {
            EquipmentLogs.Add(evt);
        }
    }

    private sealed class FlakyConnector : ILogStreamConnector
    {
        private readonly Queue<IRemoteLogStream> _streams;
        private bool _shouldThrow = true;

        public FlakyConnector(IEnumerable<string> firstStreamLines)
        {
            _streams = new Queue<IRemoteLogStream>();
            _streams.Enqueue(new FakeRemoteLogStream(firstStreamLines));
        }

        public int Attempts { get; private set; }

        public Task<IRemoteLogStream> ConnectAsync(LogStreamOptions options, CancellationToken cancellationToken)
        {
            Attempts++;
            if (_shouldThrow)
            {
                _shouldThrow = false;
                throw new InvalidOperationException("Simulated connection failure");
            }

            if (_streams.Count == 0)
            {
                throw new InvalidOperationException("No configured streams");
            }

            return Task.FromResult(_streams.Dequeue());
        }
    }

    public sealed class FakeRemoteLogStream : IRemoteLogStream
    {
        private readonly Queue<string?> _lines;

        public FakeRemoteLogStream(IEnumerable<string> lines)
        {
            _lines = new Queue<string?>(lines);
            _lines.Enqueue(null); // terminate stream
        }

        public Task WriteAsync(string command, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_lines.Count > 0 ? _lines.Dequeue() : null);
        }

        public Task<int> ReadAsync(char[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            // Simple mock implementation: if we have lines, return them one by one.
            if (_lines.Count == 0) return Task.FromResult(0);

            var line = _lines.Dequeue();
            if (line == null) return Task.FromResult(0);

            // Append newline as ReadLineAsync implies lines were stripped or we are simulating file read
            line += "\n";

            var length = Math.Min(count, line.Length);
            // Copy to buffer
            for (int i = 0; i < length; i++)
            {
                buffer[index + i] = line[i];
            }

            // If line is longer than buffer, we lose data in this simple mock, but tests use short lines.
            return Task.FromResult(length);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
