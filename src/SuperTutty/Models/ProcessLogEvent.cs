using System;

namespace SuperTutty.Models
{
    public class ProcessLogEvent
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";        // INFO, ERROR 등
        public string TransactionId { get; set; } = ""; // TX=123
        public string Message { get; set; } = "";
        public string? Step { get; set; }               // Step=VALIDATE 등
        public string RawLine { get; set; } = "";       // 원본 로그
    }
}
