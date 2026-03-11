using System.Collections.Generic;
using System.Threading.Tasks;
using SuperTutty.Services.Tasks;

namespace SuperTutty.Services
{
    public interface ISessionStorage
    {
        Task<List<SshSession>> LoadAsync();
        Task SaveAsync(List<SshSession> sessions);
        
        /// <summary>
        /// Session과 연결된 Task 정보 포함하여 로드
        /// </summary>
        Task<List<SavedSessionWithTasks>> LoadWithTasksAsync();
        
        /// <summary>
        /// Session과 연결된 Task 정보 포함하여 저장
        /// </summary>
        Task SaveWithTasksAsync(List<SavedSessionWithTasks> sessions);
        
        /// <summary>
        /// 특정 Session에 Task 추가
        /// </summary>
        Task AddTaskToSessionAsync(SshSession session, SavedTaskInfo taskInfo);
        
        /// <summary>
        /// 특정 Session에서 Task 제거
        /// </summary>
        Task RemoveTaskFromSessionAsync(SshSession session, string taskId);
    }
}
