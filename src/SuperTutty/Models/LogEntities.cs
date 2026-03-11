using System;
using SQLite;

namespace SuperTutty.Models
{
    public class ProcessLogEventEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string TransactionId { get; set; } = "";

        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Step { get; set; }
        public string RawLine { get; set; } = "";
    }

    public class EquipmentLogEventEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string EquipmentId { get; set; } = "";

        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = "";
        public string Status { get; set; } = "";
        public string? AlarmCode { get; set; }
        public double? Value { get; set; }
        public string RawLine { get; set; } = "";
    }
}
