using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SuperTutty.Models;

namespace SuperTutty.Analyzers
{
    public class EquipmentAnalyzer
    {
        private readonly ConcurrentDictionary<string, EquipmentState> _equipments = new();

        public event Action<EquipmentState>? EquipmentUpdated;

        public void OnEquipmentLog(EquipmentLogEvent evt)
        {
            _equipments.AddOrUpdate(evt.EquipmentId,
                id =>
                {
                    var state = new EquipmentState { EquipmentId = id };
                    UpdateState(state, evt);
                    return state;
                },
                (id, state) =>
                {
                    UpdateState(state, evt);
                    return state;
                });

            if (_equipments.TryGetValue(evt.EquipmentId, out var updatedState))
            {
                EquipmentUpdated?.Invoke(updatedState);
            }
        }

        private void UpdateState(EquipmentState state, EquipmentLogEvent evt)
        {
            lock (state)
            {
                state.Events.Add(evt);
                if (evt.EventType == "Alarm")
                    state.AlarmCount++;
            }
        }

        public IReadOnlyCollection<EquipmentState> GetAll()
            => _equipments.Values.ToList();
    }
}
