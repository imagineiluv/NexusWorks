using System;
using System.Collections.Generic;

namespace SuperTutty.Models
{
    public class EquipmentState
    {
        public string EquipmentId { get; set; } = "";
        public List<EquipmentLogEvent> Events { get; set; } = new();

        // 계산된 메트릭
        public TimeSpan UpTime { get; set; }
        public TimeSpan DownTime { get; set; }
        public int AlarmCount { get; set; }
    }
}
