using System;

namespace SuperTutty.Services
{
    public class LogStreamOptions
    {
        public string Host { get; set; } = string.Empty;

        public SessionPlatform Platform { get; set; } = SessionPlatform.Linux;

        public int Port { get; set; } = 22;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int MaxReconnectAttempts { get; set; } = 5;

        public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    }
}
