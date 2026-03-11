using System;
using System.IO;
using Renci.SshNet;

namespace SuperTutty.Services
{
    public class SshSession
    {
        private readonly string _host;
        private readonly string _username;
        private readonly string _password;
        private SshClient? _client;


        public string Username { get { return _username; } }
        public string Password { get { return _password; } }
        public string Host { get { return _host; } }

        public SessionPlatform Platform { get; set; } = SessionPlatform.Linux;

        public bool IsWindows => Platform == SessionPlatform.Windows;

        public SshSession(string host, string username, string password, string? name = null, int port = 22, SessionPlatform platform = SessionPlatform.Linux)
        {
            _host = host;
            _username = username;
            _password = password;
            Name = name ?? host;
            IpAddress = host;
            Port = port;
            Platform = platform;
        }

        public void Connect()
        {
            _client = new SshClient(_host, Port, _username, _password);
            _client.Connect();
        }

        public void Disconnect()
        {
            _client?.Disconnect();
            _client?.Dispose();
            _client = null;
        }

        public bool IsConnected => _client?.IsConnected ?? false;

        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; } = 22;

        public ShellStream? CreateShellStream()
        {
            return _client?.CreateShellStream("vt100", 80, 24, 800, 600, 1024);
        }
    }
}
