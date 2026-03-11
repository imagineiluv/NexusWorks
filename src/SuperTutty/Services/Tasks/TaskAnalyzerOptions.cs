using System;

namespace SuperTutty.Services.Tasks
{
    /// <summary>
    /// Task에서 사용할 분석기 옵션
    /// </summary>
    [Flags]
    public enum TaskAnalyzers
    {
        /// <summary>분석기 없음</summary>
        None = 0,
        
        /// <summary>Drain 로그 파서 - 로그 클러스터링 및 패턴 추출</summary>
        DrainLogParser = 1 << 0,
        
        /// <summary>트랜잭션 분석기 - 트랜잭션 추적 및 분석</summary>
        TransactionAnalyzer = 1 << 1,
        
        /// <summary>장비 분석기 - 장비 상태 모니터링</summary>
        EquipmentAnalyzer = 1 << 2,
        
        /// <summary>모든 분석기</summary>
        All = DrainLogParser | TransactionAnalyzer | EquipmentAnalyzer
    }

    /// <summary>
    /// Task 분석기 옵션 설정
    /// </summary>
    public class TaskAnalyzerOptions
    {
        /// <summary>
        /// 활성화된 분석기들
        /// </summary>
        public TaskAnalyzers EnabledAnalyzers { get; set; } = TaskAnalyzers.All;

        /// <summary>
        /// Drain 로그 파서 활성화 여부
        /// </summary>
        public bool UseDrainLogParser
        {
            get => EnabledAnalyzers.HasFlag(TaskAnalyzers.DrainLogParser);
            set => EnabledAnalyzers = value 
                ? EnabledAnalyzers | TaskAnalyzers.DrainLogParser 
                : EnabledAnalyzers & ~TaskAnalyzers.DrainLogParser;
        }

        /// <summary>
        /// 트랜잭션 분석기 활성화 여부
        /// </summary>
        public bool UseTransactionAnalyzer
        {
            get => EnabledAnalyzers.HasFlag(TaskAnalyzers.TransactionAnalyzer);
            set => EnabledAnalyzers = value 
                ? EnabledAnalyzers | TaskAnalyzers.TransactionAnalyzer 
                : EnabledAnalyzers & ~TaskAnalyzers.TransactionAnalyzer;
        }

        /// <summary>
        /// 장비 분석기 활성화 여부
        /// </summary>
        public bool UseEquipmentAnalyzer
        {
            get => EnabledAnalyzers.HasFlag(TaskAnalyzers.EquipmentAnalyzer);
            set => EnabledAnalyzers = value 
                ? EnabledAnalyzers | TaskAnalyzers.EquipmentAnalyzer 
                : EnabledAnalyzers & ~TaskAnalyzers.EquipmentAnalyzer;
        }

        /// <summary>
        /// 로그 데이터베이스 저장 활성화 여부
        /// </summary>
        public bool PersistLogs { get; set; } = true;

        /// <summary>
        /// 기본 옵션 (모든 분석기 활성화)
        /// </summary>
        public static TaskAnalyzerOptions Default => new() { EnabledAnalyzers = TaskAnalyzers.All };

        /// <summary>
        /// 분석기 없이 로그만 스트리밍
        /// </summary>
        public static TaskAnalyzerOptions StreamOnly => new() { EnabledAnalyzers = TaskAnalyzers.None, PersistLogs = false };
    }
}
