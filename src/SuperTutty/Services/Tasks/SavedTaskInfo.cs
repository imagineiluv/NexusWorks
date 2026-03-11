using System;
using System.Collections.Generic;
using SuperTutty.Services;

namespace SuperTutty.Services.Tasks
{
    /// <summary>
    /// Task 정보를 저장하기 위한 DTO
    /// </summary>
    public class SavedTaskInfo
    {
        /// <summary>Task 고유 ID</summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>Task 이름</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Task 설명</summary>
        public string? Description { get; set; }
        
        /// <summary>모니터링할 로그 파일 경로</summary>
        public string LogFilePath { get; set; } = string.Empty;
        
        /// <summary>생성 시간</summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>Drain Log Parser 사용 여부</summary>
        public bool UseDrainLogParser { get; set; } = true;
        
        /// <summary>Transaction Analyzer 사용 여부</summary>
        public bool UseTransactionAnalyzer { get; set; } = true;
        
        /// <summary>Equipment Analyzer 사용 여부</summary>
        public bool UseEquipmentAnalyzer { get; set; } = true;
        
        /// <summary>로그 저장 여부</summary>
        public bool PersistLogs { get; set; } = true;
        
        /// <summary>앱 시작 시 자동 시작 여부</summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>Include filter pattern (grep-like)</summary>
        public string? FilterInclude { get; set; }

        /// <summary>Exclude filter pattern (grep-like)</summary>
        public string? FilterExclude { get; set; }

        public LogStreamFilterKind FilterKind { get; set; } = LogStreamFilterKind.FixedString;

        public bool FilterIgnoreCase { get; set; } = true;

        public bool FilterInvertMatch { get; set; }
        
        /// <summary>
        /// StreamTask에서 SavedTaskInfo 생성
        /// </summary>
        public static SavedTaskInfo FromStreamTask(StreamTask task)
        {
            return new SavedTaskInfo
            {
                Id = task.Id.ToString(),
                Name = task.Name,
                Description = task.Description,
                LogFilePath = task.LogFilePath,
                CreatedAt = task.CreatedAt,
                UseDrainLogParser = task.AnalyzerOptions.UseDrainLogParser,
                UseTransactionAnalyzer = task.AnalyzerOptions.UseTransactionAnalyzer,
                UseEquipmentAnalyzer = task.AnalyzerOptions.UseEquipmentAnalyzer,
                PersistLogs = task.AnalyzerOptions.PersistLogs,
                FilterInclude = task.FilterOptions?.Include,
                FilterExclude = task.FilterOptions?.Exclude,
                FilterKind = task.FilterOptions?.Kind ?? LogStreamFilterKind.FixedString,
                FilterIgnoreCase = task.FilterOptions?.IgnoreCase ?? true,
                FilterInvertMatch = task.FilterOptions?.InvertMatch ?? false
            };
        }
        
        /// <summary>
        /// TaskAnalyzerOptions로 변환
        /// </summary>
        public TaskAnalyzerOptions ToAnalyzerOptions()
        {
            return new TaskAnalyzerOptions
            {
                UseDrainLogParser = UseDrainLogParser,
                UseTransactionAnalyzer = UseTransactionAnalyzer,
                UseEquipmentAnalyzer = UseEquipmentAnalyzer,
                PersistLogs = PersistLogs
            };
        }

        public LogStreamFilterOptions? ToFilterOptions()
        {
            var include = (FilterInclude ?? string.Empty).Trim();
            var exclude = (FilterExclude ?? string.Empty).Trim();
            if (include.Length == 0 && exclude.Length == 0)
            {
                return null;
            }

            return new LogStreamFilterOptions
            {
                Include = include.Length == 0 ? null : include,
                Exclude = exclude.Length == 0 ? null : exclude,
                Kind = FilterKind,
                IgnoreCase = FilterIgnoreCase,
                InvertMatch = FilterInvertMatch
            };
        }
    }

    /// <summary>
    /// Session과 연결된 Task 목록을 저장하기 위한 DTO
    /// </summary>
    public class SavedSessionWithTasks
    {
        /// <summary>Session 호스트</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>Session 포트</summary>
        public int Port { get; set; } = 22;

        /// <summary>Session 사용자명</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Session 비밀번호</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Session 이름</summary>
        public string? Name { get; set; }

        /// <summary>Session 플랫폼</summary>
        public SessionPlatform Platform { get; set; } = SessionPlatform.Linux;

        /// <summary>연결된 Task 목록</summary>
        public List<SavedTaskInfo> Tasks { get; set; } = new();
        
        /// <summary>
        /// SshSession으로 변환
        /// </summary>
        public SshSession ToSshSession()
        {
            return new SshSession(Host, Username, Password, Name, Port, Platform);
        }
        
        /// <summary>
        /// SshSession에서 생성
        /// </summary>
        public static SavedSessionWithTasks FromSshSession(SshSession session, IEnumerable<StreamTask>? tasks = null)
        {
            var dto = new SavedSessionWithTasks
            {
                Host = session.Host,
                Port = session.Port,
                Username = session.Username,
                Password = session.Password,
                Name = session.Name,
                Platform = session.Platform
            };
            
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    dto.Tasks.Add(SavedTaskInfo.FromStreamTask(task));
                }
            }
            
            return dto;
        }
    }
}
