using Xunit;
using SuperTutty.Analyzers;
using SuperTutty.Models;
using System;
using System.Linq;

namespace SuperTutty.Tests.Analyzers
{
    public class TransactionAnalyzerTests
    {
        [Fact]
        public void OnProcessLog_AggregatesEventsIntoTransaction()
        {
            var analyzer = new TransactionAnalyzer();
            var evt1 = new ProcessLogEvent
            {
                Timestamp = new DateTime(2025, 12, 5, 10, 0, 0),
                Level = "INFO",
                TransactionId = "100",
                Message = "Start"
            };
            var evt2 = new ProcessLogEvent
            {
                Timestamp = new DateTime(2025, 12, 5, 10, 0, 5),
                Level = "INFO",
                TransactionId = "100",
                Message = "Completed"
            };

            analyzer.OnProcessLog(evt1);
            analyzer.OnProcessLog(evt2);

            var transactions = analyzer.GetAllTransactions();
            Assert.Single(transactions);
            var tx = transactions.First();
            Assert.Equal("100", tx.Id);
            Assert.Equal(evt1.Timestamp, tx.StartTime);
            Assert.Equal(evt2.Timestamp, tx.EndTime);
            Assert.True(tx.IsCompleted);
            Assert.Equal(TimeSpan.FromSeconds(5), tx.Duration);
        }

        [Fact]
        public void OnProcessLog_ErrorLog_SetsErrorFlag()
        {
            var analyzer = new TransactionAnalyzer();
            var evt = new ProcessLogEvent
            {
                Timestamp = DateTime.Now,
                Level = "ERROR",
                TransactionId = "101",
                Message = "Something failed"
            };

            analyzer.OnProcessLog(evt);

            var tx = analyzer.GetAllTransactions().First();
            Assert.True(tx.HasError);
            Assert.Equal("Something failed", tx.ErrorMessage);
        }
    }
}
