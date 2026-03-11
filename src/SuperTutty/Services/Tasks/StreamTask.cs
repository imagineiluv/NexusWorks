using System;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.Services.Tasks
{
    /// <summary>
    /// Task 상태를 나타내는 열거형
    /// </summary>
    public enum StreamTaskStatus
    {
        /// <summary>대기 중 (아직 시작되지 않음)</summary>
        Pending,
        /// <summary>연결 중</summary>
        Connecting,
        /// <summary>실행 중</summary>
        Running,
        /// <summary>일시 정지됨</summary>
        Paused,
        /// <summary>오류 발생</summary>
        Error,
        /// <summary>완료됨</summary>
        Completed,
        /// <summary>취소됨</summary>
        Cancelled
    }

    /// <summary>
    /// Log Streaming Task - LogStreamService를 래핑하여 관리
    /// </summary>
    public class StreamTask : IAsyncDisposable
    {
        private readonly ILogStreamService _logStreamService;
        private readonly SshSession _session;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _streamingTask;

        /// <summary>
        /// Task 고유 식별자
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Task 이름 (표시용)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Task 설명
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 연결된 SSH 세션
        /// </summary>
        public SshSession Session => _session;

        /// <summary>
        /// 모니터링할 로그 파일 경로
        /// </summary>
        public string LogFilePath { get; set; }

        /// <summary>
        /// 현재 Task 상태
        /// </summary>
        public StreamTaskStatus Status { get; private set; } = StreamTaskStatus.Pending;

        /// <summary>
        /// Task 생성 시간
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        /// <summary>
        /// Task 시작 시간
        /// </summary>
        public DateTime? StartedAt { get; private set; }

        /// <summary>
        /// 마지막 오류 메시지
        /// </summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// 수신된 로그 라인 수
        /// </summary>
        public long ReceivedLogCount { get; private set; }

        /// <summary>
        /// 분석기 옵션
        /// </summary>
        public TaskAnalyzerOptions AnalyzerOptions { get; }

        /// <summary>
        /// Client-side log line filter options (grep-like).
        /// </summary>
        public LogStreamFilterOptions? FilterOptions { get; }

        /// <summary>
        /// 실시간 로그 수신 이벤트
        /// </summary>
        public event EventHandler<string>? LogReceived;

        /// <summary>
        /// 상태 변경 이벤트
        /// </summary>
        public event EventHandler<StreamTaskStatus>? StatusChanged;

        /// <summary>
        /// 오류 발생 이벤트
        /// </summary>
        public event EventHandler<Exception>? ErrorOccurred;

        public StreamTask(
            ILogStreamService logStreamService,
            SshSession session,
            string logFilePath,
            string? name = null,
            TaskAnalyzerOptions? analyzerOptions = null,
            LogStreamFilterOptions? filterOptions = null)
        {
            _logStreamService = logStreamService ?? throw new ArgumentNullException(nameof(logStreamService));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            LogFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
            Name = name ?? $"{session.Name} - {System.IO.Path.GetFileName(logFilePath)}";
            AnalyzerOptions = analyzerOptions ?? TaskAnalyzerOptions.Default;
            FilterOptions = filterOptions;

            // Subscribe to LogStreamService events
            _logStreamService.LiveLogReceived += OnLiveLogReceived;
            _logStreamService.StreamError += OnStreamError;
        }

        /// <summary>
        /// Task 시작
        /// </summary>
        public Task StartAsync()
        {
            if (Status == StreamTaskStatus.Running)
            {
                return Task.CompletedTask;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            SetStatus(StreamTaskStatus.Connecting);
            StartedAt = DateTime.UtcNow;

            _streamingTask = Task.Run(async () =>
            {
                try
                {
                    SetStatus(StreamTaskStatus.Running);
                    await _logStreamService.StartStreamingAsync(LogFilePath, FilterOptions, _cancellationTokenSource.Token);
                    SetStatus(StreamTaskStatus.Completed);
                }
                catch (OperationCanceledException)
                {
                    SetStatus(StreamTaskStatus.Cancelled);
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    SetStatus(StreamTaskStatus.Error);
                    ErrorOccurred?.Invoke(this, ex);
                }
            }, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Task 중지
        /// </summary>
        public async Task StopAsync()
        {
            if (_cancellationTokenSource == null || Status == StreamTaskStatus.Cancelled || Status == StreamTaskStatus.Completed)
            {
                return;
            }

            _cancellationTokenSource.Cancel();

            if (_streamingTask != null)
            {
                try
                {
                    await _streamingTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            SetStatus(StreamTaskStatus.Cancelled);
        }

        /// <summary>
        /// Task 일시 정지 (현재 미구현 - 스트리밍 특성상 정지 후 재개 시 데이터 손실 가능)
        /// </summary>
        public void Pause()
        {
            if (Status == StreamTaskStatus.Running)
            {
                SetStatus(StreamTaskStatus.Paused);
                // Note: 실제 스트리밍 일시 정지는 구현하지 않음
                // 필요시 버퍼링 로직 추가 필요
            }
        }

        /// <summary>
        /// Task 재개
        /// </summary>
        public void Resume()
        {
            if (Status == StreamTaskStatus.Paused)
            {
                SetStatus(StreamTaskStatus.Running);
            }
        }

        private void SetStatus(StreamTaskStatus newStatus)
        {
            if (Status != newStatus)
            {
                Status = newStatus;
                StatusChanged?.Invoke(this, newStatus);
            }
        }

        private void OnLiveLogReceived(object? sender, string logLine)
        {
            if (Status == StreamTaskStatus.Running)
            {
                ReceivedLogCount++;
                LogReceived?.Invoke(this, logLine);
            }
        }

        private void OnStreamError(object? sender, Exception ex)
        {
            LastError = ex.Message;
            ErrorOccurred?.Invoke(this, ex);
        }

        public async ValueTask DisposeAsync()
        {
            _logStreamService.LiveLogReceived -= OnLiveLogReceived;
            _logStreamService.StreamError -= OnStreamError;

            await StopAsync();
            _cancellationTokenSource?.Dispose();
        }
    }
}
