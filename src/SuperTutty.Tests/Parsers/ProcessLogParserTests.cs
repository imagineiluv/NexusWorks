using Xunit;
using SuperTutty.Parsers;
using System;

namespace SuperTutty.Tests.Parsers
{
    public class ProcessLogParserTests
    {
        [Fact]
        public void Parse_ValidStartLog_ReturnsEvent()
        {
            var parser = new ProcessLogParser();
            var line = "2025-12-05 10:00:00 INFO [TX=123] Start orderId=1001";

            var result = parser.Parse(line);

            Assert.NotNull(result);
            Assert.Equal("INFO", result!.Level);
            Assert.Equal("123", result.TransactionId);
            Assert.Equal("Start orderId=1001", result.Message);
            Assert.Equal(new DateTime(2025, 12, 5, 10, 0, 0), result.Timestamp);
        }

        [Fact]
        public void Parse_ValidStepLog_ReturnsEventWithStep()
        {
            var parser = new ProcessLogParser();
            var line = "2025-12-05 10:00:01 INFO [TX=123] Step=VALIDATE";

            var result = parser.Parse(line);

            Assert.NotNull(result);
            Assert.Equal("VALIDATE", result!.Step);
        }

        [Fact]
        public void Parse_InvalidLog_ReturnsNull()
        {
            var parser = new ProcessLogParser();
            var line = "Invalid Log Line";

            var result = parser.Parse(line);

            Assert.Null(result);
        }
    }
}
