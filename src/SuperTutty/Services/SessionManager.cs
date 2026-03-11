using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SuperTutty.Services
{
    public interface ISessionManager
    {
        Task<List<SshSession>> GetSessionsAsync();
        Task SaveSessionAsync(SshSession session);
        Task DeleteSessionAsync(SshSession session);
    }

    public class SessionManager : ISessionManager
    {
        private readonly ISessionStorage _storage;

        public SessionManager(ISessionStorage? sessionStorage = null)
        {
            _storage = sessionStorage ?? new InMemorySessionStorage();
        }

        public async Task<List<SshSession>> GetSessionsAsync()
        {
            try
            {
                var sessions = await _storage.LoadAsync();
                if (sessions == null || sessions.Count == 0)
                {
                    return GetDefaultMockSessions();
                }

                return sessions;
            }
            catch
            {
                return GetDefaultMockSessions();
            }
        }

        public async Task SaveSessionAsync(SshSession session)
        {
            var sessions = await GetSessionsAsync();

            // Check if exists (by Name or IP for simplicity)
            var existing = sessions.Find(s => s.Name == session.Name && s.IpAddress == session.IpAddress);
            if (existing != null)
            {
                sessions.Remove(existing);
            }
            sessions.Add(session);

            await _storage.SaveAsync(sessions);
        }

        public async Task DeleteSessionAsync(SshSession session)
        {
            var sessions = await GetSessionsAsync();
            var existing = sessions.Find(s => s.Name == session.Name && s.IpAddress == session.IpAddress);
            if (existing != null)
            {
                sessions.Remove(existing);
                await _storage.SaveAsync(sessions);
            }
        }

        private List<SshSession> GetDefaultMockSessions()
        {
            return new List<SshSession>
            {
                new SshSession("192.168.1.1", "user", "password"),
                new SshSession("staging.db.internal", "user", "password"),
                new SshSession("winlog.internal", "Administrator", "P@ssw0rd", port: 5985, platform: SessionPlatform.Windows)
            };
        }

        private class InMemorySessionStorage : ISessionStorage
        {
            private readonly List<SshSession> _sessions = new();
            private readonly List<Tasks.SavedSessionWithTasks> _sessionsWithTasks = new();

            public Task<List<SshSession>> LoadAsync()
            {
                return Task.FromResult(new List<SshSession>(_sessions));
            }

            public Task SaveAsync(List<SshSession> sessions)
            {
                _sessions.Clear();
                if (sessions != null)
                {
                    _sessions.AddRange(sessions);
                }
                return Task.CompletedTask;
            }
            
            public Task<List<Tasks.SavedSessionWithTasks>> LoadWithTasksAsync()
            {
                return Task.FromResult(new List<Tasks.SavedSessionWithTasks>(_sessionsWithTasks));
            }
            
            public Task SaveWithTasksAsync(List<Tasks.SavedSessionWithTasks> sessions)
            {
                _sessionsWithTasks.Clear();
                if (sessions != null)
                {
                    _sessionsWithTasks.AddRange(sessions);
                }
                return Task.CompletedTask;
            }
            
            public Task AddTaskToSessionAsync(SshSession session, Tasks.SavedTaskInfo taskInfo)
            {
                var existingSession = _sessionsWithTasks.FirstOrDefault(s => 
                    s.Host == session.Host && s.Port == session.Port && s.Username == session.Username);
                
                if (existingSession == null)
                {
                    existingSession = Tasks.SavedSessionWithTasks.FromSshSession(session);
                    _sessionsWithTasks.Add(existingSession);
                }
                
                existingSession.Tasks.Add(taskInfo);
                return Task.CompletedTask;
            }
            
            public Task RemoveTaskFromSessionAsync(SshSession session, string taskId)
            {
                var existingSession = _sessionsWithTasks.FirstOrDefault(s => 
                    s.Host == session.Host && s.Port == session.Port && s.Username == session.Username);
                
                if (existingSession != null)
                {
                    var taskToRemove = existingSession.Tasks.FirstOrDefault(t => t.Id == taskId);
                    if (taskToRemove != null)
                    {
                        existingSession.Tasks.Remove(taskToRemove);
                    }
                }
                return Task.CompletedTask;
            }
        }

    }
}
