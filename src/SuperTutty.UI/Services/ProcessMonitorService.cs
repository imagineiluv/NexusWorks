using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SuperTutty.UI.Services
{
    public class ProcessMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public double MemoryUsageGB => MemoryUsageMB / 1024.0;
        public long PrivateMemoryBytes { get; set; }
        public int ThreadCount { get; set; }
        public TimeSpan TotalProcessorTime { get; set; }
    }

    public interface IProcessMonitorService : IDisposable
    {
        ProcessMetrics CurrentMetrics { get; }
        event EventHandler<ProcessMetrics> MetricsUpdated;
        void Start(TimeSpan? interval = null);
        void Stop();
    }

    public class ProcessMonitorService : IProcessMonitorService
    {
        private readonly Process _currentProcess;
        private Timer? _timer;
        private DateTime _lastCpuCheckTime;
        private TimeSpan _lastTotalProcessorTime;
        private readonly int _processorCount;

        public ProcessMetrics CurrentMetrics { get; private set; } = new();
        public event EventHandler<ProcessMetrics>? MetricsUpdated;

        public ProcessMonitorService()
        {
            _currentProcess = Process.GetCurrentProcess();
            _processorCount = Environment.ProcessorCount;
            _lastCpuCheckTime = DateTime.UtcNow;
            _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
        }

        public void Start(TimeSpan? interval = null)
        {
            var updateInterval = interval ?? TimeSpan.FromSeconds(2);
            _timer?.Dispose();
            _timer = new Timer(UpdateMetrics, null, TimeSpan.Zero, updateInterval);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private void UpdateMetrics(object? state)
        {
            try
            {
                _currentProcess.Refresh();

                var currentTime = DateTime.UtcNow;
                var currentCpuTime = _currentProcess.TotalProcessorTime;

                var cpuUsedMs = (currentCpuTime - _lastTotalProcessorTime).TotalMilliseconds;
                var elapsedMs = (currentTime - _lastCpuCheckTime).TotalMilliseconds;

                var cpuUsagePercent = elapsedMs > 0
                    ? (cpuUsedMs / (elapsedMs * _processorCount)) * 100.0
                    : 0;

                _lastCpuCheckTime = currentTime;
                _lastTotalProcessorTime = currentCpuTime;

                CurrentMetrics = new ProcessMetrics
                {
                    CpuUsagePercent = Math.Round(Math.Max(0, Math.Min(100, cpuUsagePercent)), 2),
                    MemoryUsageMB = Math.Round(_currentProcess.WorkingSet64 / (1024.0 * 1024.0), 1),
                    PrivateMemoryBytes = _currentProcess.PrivateMemorySize64,
                    ThreadCount = _currentProcess.Threads.Count,
                    TotalProcessorTime = currentCpuTime
                };

                MetricsUpdated?.Invoke(this, CurrentMetrics);
            }
            catch (Exception)
            {
                // Process may have exited or access denied
            }
        }

        public void Dispose()
        {
            Stop();
            _currentProcess.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
