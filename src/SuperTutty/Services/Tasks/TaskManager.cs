using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SuperTutty.Services.Tasks
{
    /// <summary>
    /// Task 관리자 인터페이스
    /// </summary>
    public interface ITaskManager
    {
        /// <summary>모든 Task 조회</summary>
        IReadOnlyList<StreamTask> Tasks { get; }

        /// <summary>실행 중인 Task 수</summary>
        int RunningTaskCount { get; }

        /// <summary>새 Task 추가</summary>
        StreamTask AddTask(StreamTask task);

        /// <summary>Task 제거</summary>
        Task<bool> RemoveTaskAsync(Guid taskId);

        /// <summary>특정 Task 조회</summary>
        StreamTask? GetTask(Guid taskId);

        /// <summary>세션별 Task 조회</summary>
        IEnumerable<StreamTask> GetTasksBySession(SshSession session);

        /// <summary>모든 Task 시작</summary>
        Task StartAllAsync();

        /// <summary>모든 Task 중지</summary>
        Task StopAllAsync();

        /// <summary>특정 Task 시작</summary>
        Task StartTaskAsync(Guid taskId);

        /// <summary>특정 Task 중지</summary>
        Task StopTaskAsync(Guid taskId);

        /// <summary>Task 추가됨 이벤트</summary>
        event EventHandler<StreamTask>? TaskAdded;

        /// <summary>Task 제거됨 이벤트</summary>
        event EventHandler<StreamTask>? TaskRemoved;

        /// <summary>Task 상태 변경됨 이벤트</summary>
        event EventHandler<StreamTask>? TaskStatusChanged;
    }

    /// <summary>
    /// Task 관리자 구현
    /// </summary>
    public class TaskManager : ITaskManager, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Guid, StreamTask> _tasks = new();
        private readonly ILogger<TaskManager>? _logger;

        public event EventHandler<StreamTask>? TaskAdded;
        public event EventHandler<StreamTask>? TaskRemoved;
        public event EventHandler<StreamTask>? TaskStatusChanged;

        public TaskManager(ILogger<TaskManager>? logger = null)
        {
            _logger = logger;
        }

        public IReadOnlyList<StreamTask> Tasks => _tasks.Values.ToList().AsReadOnly();

        public int RunningTaskCount => _tasks.Values.Count(t => t.Status == StreamTaskStatus.Running);

        public StreamTask AddTask(StreamTask task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (!_tasks.TryAdd(task.Id, task))
            {
                throw new InvalidOperationException($"Task with ID {task.Id} already exists.");
            }

            // Subscribe to task status changes
            task.StatusChanged += OnTaskStatusChanged;

            _logger?.LogInformation("Task added: {TaskId} - {TaskName}", task.Id, task.Name);
            TaskAdded?.Invoke(this, task);

            return task;
        }

        public async Task<bool> RemoveTaskAsync(Guid taskId)
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                task.StatusChanged -= OnTaskStatusChanged;

                // Stop and dispose the task
                await task.DisposeAsync();

                _logger?.LogInformation("Task removed: {TaskId} - {TaskName}", task.Id, task.Name);
                TaskRemoved?.Invoke(this, task);

                return true;
            }

            return false;
        }

        public StreamTask? GetTask(Guid taskId)
        {
            _tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public IEnumerable<StreamTask> GetTasksBySession(SshSession session)
        {
            return _tasks.Values.Where(t => 
                t.Session.IpAddress == session.IpAddress && 
                t.Session.Port == session.Port);
        }

        public async Task StartAllAsync()
        {
            var pendingTasks = _tasks.Values
                .Where(t => t.Status == StreamTaskStatus.Pending || t.Status == StreamTaskStatus.Cancelled)
                .ToList();

            foreach (var task in pendingTasks)
            {
                try
                {
                    await task.StartAsync();
                    _logger?.LogInformation("Task started: {TaskId} - {TaskName}", task.Id, task.Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start task: {TaskId}", task.Id);
                }
            }
        }

        public async Task StopAllAsync()
        {
            var runningTasks = _tasks.Values
                .Where(t => t.Status == StreamTaskStatus.Running || t.Status == StreamTaskStatus.Connecting)
                .ToList();

            foreach (var task in runningTasks)
            {
                try
                {
                    await task.StopAsync();
                    _logger?.LogInformation("Task stopped: {TaskId} - {TaskName}", task.Id, task.Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to stop task: {TaskId}", task.Id);
                }
            }
        }

        public async Task StartTaskAsync(Guid taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await task.StartAsync();
                _logger?.LogInformation("Task started: {TaskId} - {TaskName}", task.Id, task.Name);
            }
        }

        public async Task StopTaskAsync(Guid taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await task.StopAsync();
                _logger?.LogInformation("Task stopped: {TaskId} - {TaskName}", task.Id, task.Name);
            }
        }

        private void OnTaskStatusChanged(object? sender, StreamTaskStatus status)
        {
            if (sender is StreamTask task)
            {
                _logger?.LogDebug("Task status changed: {TaskId} -> {Status}", task.Id, status);
                TaskStatusChanged?.Invoke(this, task);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAllAsync();

            foreach (var task in _tasks.Values)
            {
                task.StatusChanged -= OnTaskStatusChanged;
                await task.DisposeAsync();
            }

            _tasks.Clear();
        }
    }
}
