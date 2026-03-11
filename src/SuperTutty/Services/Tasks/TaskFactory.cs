using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperTutty.Analyzers;
using SuperTutty.Services.Drain;

namespace SuperTutty.Services.Tasks
{
    /// <summary>
    /// StreamTask 생성을 위한 팩토리 인터페이스
    /// </summary>
    public interface IStreamTaskFactory
    {
        /// <summary>
        /// 새로운 StreamTask 생성
        /// </summary>
        /// <param name="session">SSH 세션</param>
        /// <param name="logFilePath">로그 파일 경로</param>
        /// <param name="taskName">Task 이름 (선택)</param>
        /// <param name="analyzerOptions">분석기 옵션 (선택)</param>
        /// <param name="filterOptions">로그 필터 옵션 (선택)</param>
        /// <returns>생성된 StreamTask</returns>
        StreamTask CreateTask(
            SshSession session,
            string logFilePath,
            string? taskName = null,
            TaskAnalyzerOptions? analyzerOptions = null,
            LogStreamFilterOptions? filterOptions = null);
    }

    /// <summary>
    /// StreamTask 생성을 위한 팩토리 구현
    /// DI 컨테이너를 사용하여 LogStreamService의 의존성을 주입
    /// </summary>
    public class StreamTaskFactory : IStreamTaskFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StreamTaskFactory>? _logger;

        public StreamTaskFactory(IServiceProvider serviceProvider, ILogger<StreamTaskFactory>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;
        }

        public StreamTask CreateTask(
            SshSession session,
            string logFilePath,
            string? taskName = null,
            TaskAnalyzerOptions? analyzerOptions = null,
            LogStreamFilterOptions? filterOptions = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new ArgumentException("Log file path cannot be empty", nameof(logFilePath));
            }

            var options = analyzerOptions ?? TaskAnalyzerOptions.Default;

            // Create LogStreamOptions from SshSession
            var streamOptions = Options.Create(new LogStreamOptions
            {
                Host = session.IpAddress,
                Platform = session.Platform,
                Port = session.Port,
                Username = session.Username,
                Password = session.Password
            });

            // Resolve dependencies from DI
            var connector = _serviceProvider.GetRequiredService<ILogStreamConnector>();
            var logger = _serviceProvider.GetRequiredService<ILogger<LogStreamService>>();
            
            // 분석기 옵션에 따라 조건부로 분석기 가져오기
            var transactionAnalyzer = options.UseTransactionAnalyzer 
                ? _serviceProvider.GetRequiredService<TransactionAnalyzer>() 
                : new TransactionAnalyzer(); // 비활성화 시 빈 분석기
            
            var equipmentAnalyzer = options.UseEquipmentAnalyzer 
                ? _serviceProvider.GetRequiredService<EquipmentAnalyzer>() 
                : new EquipmentAnalyzer(); // 비활성화 시 빈 분석기
            
            var logPersistence = options.PersistLogs 
                ? _serviceProvider.GetRequiredService<ILogPersistence>() 
                : new NullLogPersistence(); // 비활성화 시 Null 구현체
            
            var drainLogParser = options.UseDrainLogParser 
                ? _serviceProvider.GetRequiredService<DrainLogParser>() 
                : new DrainLogParser(depth: 4, similarityThreshold: 0.5); // 비활성화 시 빈 파서

            // Create LogStreamService instance
            var logStreamService = new LogStreamService(
                connector,
                streamOptions,
                logger,
                transactionAnalyzer,
                equipmentAnalyzer,
                logPersistence,
                drainLogParser
            );

            var task = new StreamTask(logStreamService, session, logFilePath, taskName, options, filterOptions);

            _logger?.LogInformation(
                "Created StreamTask: {TaskName} for {Host}:{Port} -> {LogPath} (Analyzers: {Analyzers})",
                task.Name, session.IpAddress, session.Port, logFilePath, options.EnabledAnalyzers);

            return task;
        }
    }

    /// <summary>
    /// 로그 저장을 하지 않는 Null 구현체
    /// </summary>
    internal class NullLogPersistence : ILogPersistence
    {
        public void EnqueueProcessLog(Models.ProcessLogEvent evt) { }
        public void EnqueueEquipmentLog(Models.EquipmentLogEvent evt) { }
    }
}
