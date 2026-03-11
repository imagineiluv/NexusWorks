using Xunit;
using SuperTutty.Services.Drain;
using System.Linq;

namespace SuperTutty.Tests.Drain
{
    public class DrainLogParserTests
    {
        [Fact]
        public void ParseLog_IdenticalLogs_MapsToSameCluster()
        {
            var parser = new DrainLogParser();
            string log1 = "Connected to 192.168.1.1 port 80";
            string log2 = "Connected to 192.168.1.1 port 80";

            var c1 = parser.ParseLog(log1);
            var c2 = parser.ParseLog(log2);

            Assert.Equal(c1.Id, c2.Id);
            Assert.Equal(2, c1.Count);
        }

        [Fact]
        public void ParseLog_VariablePart_MapsToClusterWithWildcard()
        {
            var parser = new DrainLogParser();
            string log1 = "Connected to 192.168.1.1 port 80";
            string log2 = "Connected to 10.0.0.1 port 80";

            var c1 = parser.ParseLog(log1);
            var c2 = parser.ParseLog(log2);

            Assert.Equal(c1.Id, c2.Id);
            // "Connected" "to" "*" "port" "80"
            Assert.Contains("*", c1.Template);
            Assert.Equal("Connected to * port 80", c1.Template);
        }

        [Fact]
        public void ParseLog_DifferentStructure_CreatesNewCluster()
        {
            var parser = new DrainLogParser();
            string log1 = "Connected to 192.168.1.1";
            string log2 = "Connection failed reason timeout";

            var c1 = parser.ParseLog(log1);
            var c2 = parser.ParseLog(log2);

            Assert.NotEqual(c1.Id, c2.Id);
            Assert.Equal(2, parser.GetClusters().Count);
        }
    }
}
