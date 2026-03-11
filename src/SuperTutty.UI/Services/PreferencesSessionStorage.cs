#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using SuperTutty.Services;
using SuperTutty.Services.Tasks;

namespace SuperTutty.UI.Services
{
    internal class PreferencesSessionStorage : ISessionStorage
    {
        private const string SessionsKey = "saved_sessions_list";
        private const string SessionsWithTasksKey = "saved_sessions_with_tasks";
        private const string DefaultSessionsAsset = "mock_sessions.json";
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<List<SshSession>> LoadAsync()
        {
            // Base list: explicit saved sessions if present, otherwise defaults.
            var json = Preferences.Default.Get(SessionsKey, string.Empty);
            var sessions = string.IsNullOrWhiteSpace(json)
                ? await LoadDefaultSessionsAsync()
                : (JsonSerializer.Deserialize<List<SshSession>>(json, SerializerOptions) ?? new List<SshSession>());

            // Merge: also include any sessions that exist only in the sessions-with-tasks store.
            var sessionsWithTasks = await LoadWithTasksAsync();
            if (sessionsWithTasks.Count > 0)
            {
                foreach (var sessionWithTasks in sessionsWithTasks)
                {
                    var candidate = sessionWithTasks.ToSshSession();
                    var alreadyPresent = sessions.Any(s =>
                        s.Host == candidate.Host &&
                        s.Port == candidate.Port &&
                        s.Username == candidate.Username &&
                        s.Platform == candidate.Platform);

                    if (!alreadyPresent)
                    {
                        sessions.Add(candidate);
                    }
                }
            }

            return sessions;
        }

        public Task SaveAsync(List<SshSession> sessions)
        {
            var json = JsonSerializer.Serialize(sessions ?? new List<SshSession>(), SerializerOptions);
            Preferences.Default.Set(SessionsKey, json);
            return Task.CompletedTask;
        }
        
        public Task<List<SavedSessionWithTasks>> LoadWithTasksAsync()
        {
            var json = Preferences.Default.Get(SessionsWithTasksKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Task.FromResult(new List<SavedSessionWithTasks>());
            }

            var sessions = JsonSerializer.Deserialize<List<SavedSessionWithTasks>>(json, SerializerOptions);
            return Task.FromResult(sessions ?? new List<SavedSessionWithTasks>());
        }
        
        public Task SaveWithTasksAsync(List<SavedSessionWithTasks> sessions)
        {
            var json = JsonSerializer.Serialize(sessions ?? new List<SavedSessionWithTasks>(), SerializerOptions);
            Preferences.Default.Set(SessionsWithTasksKey, json);
            return Task.CompletedTask;
        }
        
        public async Task AddTaskToSessionAsync(SshSession session, SavedTaskInfo taskInfo)
        {
            var sessions = await LoadWithTasksAsync();
            var existingSession = sessions.FirstOrDefault(s => 
                s.Host == session.Host && s.Port == session.Port && s.Username == session.Username);
            
            if (existingSession == null)
            {
                // Create new session entry with task
                existingSession = SavedSessionWithTasks.FromSshSession(session);
                sessions.Add(existingSession);
            }
            
            // Check if task already exists
            var existingTask = existingSession.Tasks.FirstOrDefault(t => t.Id == taskInfo.Id);
            if (existingTask != null)
            {
                existingSession.Tasks.Remove(existingTask);
            }
            
            existingSession.Tasks.Add(taskInfo);
            await SaveWithTasksAsync(sessions);
        }
        
        public async Task RemoveTaskFromSessionAsync(SshSession session, string taskId)
        {
            var sessions = await LoadWithTasksAsync();
            var existingSession = sessions.FirstOrDefault(s => 
                s.Host == session.Host && s.Port == session.Port && s.Username == session.Username);
            
            if (existingSession == null)
            {
                return;
            }
            
            var taskToRemove = existingSession.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToRemove != null)
            {
                existingSession.Tasks.Remove(taskToRemove);
                await SaveWithTasksAsync(sessions);
            }
        }

        private async Task<List<SshSession>> LoadDefaultSessionsAsync()
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(DefaultSessionsAsset);
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                var defaults = JsonSerializer.Deserialize<List<SessionDto>>(content, SerializerOptions);
                if (defaults == null)
                {
                    return new List<SshSession>();
                }

                var sessions = new List<SshSession>(defaults.Count);
                foreach (var dto in defaults)
                {
                    sessions.Add(dto.ToSshSession());
                }

                return sessions;
            }
            catch
            {
                return new List<SshSession>();
            }
        }

        private sealed class SessionDto
        {
            public string Host { get; init; } = string.Empty;
            public string? Name { get; init; }
            public string Username { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
            public int Port { get; init; } = 22;
            public SessionPlatform Platform { get; init; } = SessionPlatform.Linux;

            public SshSession ToSshSession()
            {
                return new SshSession(Host, Username, Password, name: Name, port: Port, platform: Platform);
            }
        }
    }
}
