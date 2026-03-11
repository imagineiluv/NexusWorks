using Xunit;
using SuperTutty.Analyzers;
using SuperTutty.Models;
using System.Linq;

namespace SuperTutty.Tests.Analyzers
{
    public class EquipmentAnalyzerTests
    {
        [Fact]
        public void OnEquipmentLog_AccumulatesEventsAndCountsAlarms()
        {
            var analyzer = new EquipmentAnalyzer();
            var evt1 = new EquipmentLogEvent { EquipmentId = "E1", EventType = "Status", Status = "RUN" };
            var evt2 = new EquipmentLogEvent { EquipmentId = "E1", EventType = "Alarm", AlarmCode = "ERR01" };

            analyzer.OnEquipmentLog(evt1);
            analyzer.OnEquipmentLog(evt2);

            var equipments = analyzer.GetAll();
            Assert.Single(equipments);
            var eq = equipments.First();
            Assert.Equal("E1", eq.EquipmentId);
            Assert.Equal(2, eq.Events.Count);
            Assert.Equal(1, eq.AlarmCount);
        }
    }
}
