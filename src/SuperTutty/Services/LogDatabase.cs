using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;
using SuperTutty.Models;

namespace SuperTutty.Services
{
    public interface ILogPersistence
    {
        void EnqueueProcessLog(ProcessLogEvent evt);

        void EnqueueEquipmentLog(EquipmentLogEvent evt);
    }

    public class LogDatabase : ILogPersistence, IAsyncDisposable
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly Channel<ProcessLogEventEntity> _processChannel;
        private readonly Channel<EquipmentLogEventEntity> _equipmentChannel;
        private readonly ILogger<LogDatabase> _logger;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task _processingTask;
        private readonly Task _initializationTask;
        private readonly int _batchSize;
        private readonly TimeSpan _idleDelay;
        private int _pendingProcessCount;
        private int _pendingEquipmentCount;

        public LogDatabase(
            ILogger<LogDatabase>? logger = null,
            string? databasePath = null,
            int channelCapacity = 500,
            int batchSize = 100,
            TimeSpan? idleDelay = null)
        {
            var dbPath = databasePath ?? GetDefaultDatabasePath();
            _database = new SQLiteAsyncConnection(dbPath);
            _logger = logger ?? NullLogger<LogDatabase>.Instance;
            _batchSize = batchSize;
            _idleDelay = idleDelay ?? TimeSpan.FromMilliseconds(250);

            var channelOptions = new BoundedChannelOptions(channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };

            _processChannel = Channel.CreateBounded<ProcessLogEventEntity>(channelOptions);
            _equipmentChannel = Channel.CreateBounded<EquipmentLogEventEntity>(channelOptions);
            _cancellationSource = new CancellationTokenSource();

            // Start initialization and processing
            _initializationTask = InitializeAsync();
            _processingTask = Task.Run(() => RunBatchProcessorAsync(_cancellationSource.Token));
        }

        private static string GetDefaultDatabasePath()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                baseDir = Environment.CurrentDirectory;
            }

            var dbDir = Path.Combine(baseDir, "SuperTutty");
            Directory.CreateDirectory(dbDir);

            return Path.Combine(dbDir, "logs.db3");
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _database.CreateTableAsync<ProcessLogEventEntity>();
                await _database.CreateTableAsync<EquipmentLogEventEntity>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database tables.");
                throw;
            }
        }

        public void EnqueueProcessLog(ProcessLogEvent evt)
        {
            var entity = new ProcessLogEventEntity
            {
                Timestamp = evt.Timestamp,
                Level = evt.Level,
                TransactionId = evt.TransactionId,
                Message = evt.Message,
                Step = evt.Step,
                RawLine = evt.RawLine
            };

            if (_processChannel.Writer.TryWrite(entity))
            {
                Interlocked.Increment(ref _pendingProcessCount);
            }
            else
            {
                _logger.LogWarning("Process log queue is full; dropping incoming entry.");
            }
        }

        public void EnqueueEquipmentLog(EquipmentLogEvent evt)
        {
            var entity = new EquipmentLogEventEntity
            {
                Timestamp = evt.Timestamp,
                EquipmentId = evt.EquipmentId,
                EventType = evt.EventType,
                Status = evt.Status,
                AlarmCode = evt.AlarmCode,
                Value = evt.Value,
                RawLine = evt.RawLine
            };

            if (_equipmentChannel.Writer.TryWrite(entity))
            {
                Interlocked.Increment(ref _pendingEquipmentCount);
            }
            else
            {
                _logger.LogWarning("Equipment log queue is full; dropping incoming entry.");
            }
        }

        private async Task RunBatchProcessorAsync(CancellationToken cancellationToken)
        {
            // Wait for initialization to complete before processing any batches
            try
            {
                await _initializationTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // If initialization fails, we cannot process logs.
                // We'll just exit the loop (or we could retry).
                _logger.LogCritical("Database initialization failed. Log persistence will be disabled.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchesAsync(cancellationToken).ConfigureAwait(false);

                    if (IsDrainedAndCompleted())
                    {
                        return;
                    }

                    // Adaptive delay: if we processed something, maybe check again soon?
                    // For now, keep simple delay.
                    await Task.Delay(_idleDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DB Batch Error");
                }
            }
        }

        private async Task ProcessBatchesAsync(CancellationToken cancellationToken)
        {
            var processBatch = new List<ProcessLogEventEntity>();
            while (processBatch.Count < _batchSize && _processChannel.Reader.TryRead(out var p))
            {
                processBatch.Add(p);
            }

            var equipmentBatch = new List<EquipmentLogEventEntity>();
            while (equipmentBatch.Count < _batchSize && _equipmentChannel.Reader.TryRead(out var e))
            {
                equipmentBatch.Add(e);
            }

            if (processBatch.Count > 0)
            {
                await _database.InsertAllAsync(processBatch).ConfigureAwait(false);
                Interlocked.Add(ref _pendingProcessCount, -processBatch.Count);
            }

            if (equipmentBatch.Count > 0)
            {
                await _database.InsertAllAsync(equipmentBatch).ConfigureAwait(false);
                Interlocked.Add(ref _pendingEquipmentCount, -equipmentBatch.Count);
            }
        }

        private bool IsDrainedAndCompleted()
        {
            return _processChannel.Reader.Completion.IsCompleted &&
                   _equipmentChannel.Reader.Completion.IsCompleted &&
                   Volatile.Read(ref _pendingProcessCount) == 0 &&
                   Volatile.Read(ref _pendingEquipmentCount) == 0;
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            // Wait for init first, in case flush is called very early
            try
            {
                await _initializationTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Flush aborted because initialization failed.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                if (Volatile.Read(ref _pendingProcessCount) == 0 && Volatile.Read(ref _pendingEquipmentCount) == 0)
                {
                    return;
                }

                if (_processingTask.IsCompleted)
                {
                    _logger.LogWarning("Flush ended early because background processing stopped.");
                    return;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> CountProcessLogsAsync()
        {
            await _initializationTask;
            return await _database.Table<ProcessLogEventEntity>().CountAsync();
        }

        public async Task<int> CountEquipmentLogsAsync()
        {
             await _initializationTask;
             return await _database.Table<EquipmentLogEventEntity>().CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _processChannel.Writer.TryComplete();
            _equipmentChannel.Writer.TryComplete();

            using var disposeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await FlushAsync(disposeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("LogDatabase flush cancelled during disposal.");
            }

            _cancellationSource.Cancel();
            try
            {
                await _processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during processing task shutdown");
            }

            _processChannel.Writer.TryComplete();
            _equipmentChannel.Writer.TryComplete();

            _cancellationSource.Dispose();
        }
    }
}
