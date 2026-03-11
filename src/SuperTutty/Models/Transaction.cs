using System;
using System.Collections.Generic;

namespace SuperTutty.Models
{
    public class Transaction
    {
        public string Id { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public bool IsCompleted => EndTime.HasValue;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }

        public List<ProcessLogEvent> Events { get; set; } = new();

        public TimeSpan? Duration =>
            (StartTime.HasValue && EndTime.HasValue)
                ? EndTime - StartTime
                : null;
    }
}
