using Xunit;
using SuperTutty.Parsers;
using System;

namespace SuperTutty.Tests.Parsers
{
    public class EquipmentLogParserTests
    {
        [Fact]
        public void Parse_StatusLog_ReturnsEvent()
        {
            var parser = new EquipmentLogParser();
            var line = "2025-12-05 10:00:01 EQUIP [EQ=A1] Status=RUN";

            var result = parser.Parse(line);

            Assert.NotNull(result);
            Assert.Equal("A1", result!.EquipmentId);
            Assert.Equal("Status", result.EventType);
            Assert.Equal("RUN", result.Status);
            Assert.Equal(new DateTime(2025, 12, 5, 10, 0, 1), result.Timestamp);
        }

        [Fact]
        public void Parse_AlarmLog_ReturnsEventWithAlarmCodeAndValue()
        {
            var parser = new EquipmentLogParser();
            var line = "2025-12-05 10:05:00 EQUIP [EQ=A1] Alarm=TEMP_HIGH Value=85";

            var result = parser.Parse(line);

            Assert.NotNull(result);
            Assert.Equal("Alarm", result!.EventType);
            Assert.Equal("TEMP_HIGH", result.AlarmCode);
            Assert.Equal(85.0, result.Value);
        }
    }
}
