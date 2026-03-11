using System;

namespace SuperTutty.Models
{
    public class EquipmentLogEvent
    {
        public DateTime Timestamp { get; set; }
        public string EquipmentId { get; set; } = "";   // EQ=A1
        public string EventType { get; set; } = "";     // Status, Alarm, Value 등
        public string Status { get; set; } = "";        // RUN, STOP 등
        public string? AlarmCode { get; set; }
        public double? Value { get; set; }              // 온도, 압력 등 숫자값
        public string RawLine { get; set; } = "";
    }
}
