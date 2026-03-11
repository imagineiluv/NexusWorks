using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SuperTutty.Models;

namespace SuperTutty.Analyzers
{
    public class TransactionAnalyzer
    {
        private readonly ConcurrentDictionary<string, Transaction> _transactions = new();

        public event Action<Transaction>? TransactionUpdated;

        public void OnProcessLog(ProcessLogEvent evt)
        {
            _transactions.AddOrUpdate(evt.TransactionId,
                // Add new
                id =>
                {
                    var tx = new Transaction { Id = id };
                    UpdateTransaction(tx, evt);
                    return tx;
                },
                // Update existing
                (id, tx) =>
                {
                    UpdateTransaction(tx, evt);
                    return tx;
                });

            // Retrieve updated for notification
            if (_transactions.TryGetValue(evt.TransactionId, out var updatedTx))
            {
                // Fire event, but ensure subscriber handles thread context if needed.
                // However, strictly speaking, events can be raised on background threads.
                TransactionUpdated?.Invoke(updatedTx);
            }
        }

        private void UpdateTransaction(Transaction tx, ProcessLogEvent evt)
        {
            // Lock individual object for update if needed, though Dictionary is Concurrent, the object itself isn't thread-safe for list modifications
            lock (tx)
            {
                tx.Events.Add(evt);

                if (evt.Message.Contains("Start", StringComparison.OrdinalIgnoreCase))
                {
                    tx.StartTime ??= evt.Timestamp;
                }
                if (evt.Message.Contains("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    tx.EndTime ??= evt.Timestamp;
                }
                if (evt.Level == "ERROR")
                {
                    tx.HasError = true;
                    tx.ErrorMessage = evt.Message;
                    tx.EndTime ??= evt.Timestamp;
                }
            }
        }

        public IReadOnlyCollection<Transaction> GetAllTransactions()
            => _transactions.Values.ToList();
    }
}
