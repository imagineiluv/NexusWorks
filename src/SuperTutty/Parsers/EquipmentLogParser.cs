using System;
using System.Text.RegularExpressions;
using SuperTutty.Models;

namespace SuperTutty.Parsers
{
    public class EquipmentLogParser
    {
        private readonly Regex _regex = new Regex(
            @"^(?<ts>\S+\s+\S+)\s+EQUIP\s+\[EQ=(?<eq>\w+)\]\s+(?<rest>.+)$",
            RegexOptions.Compiled);

        public EquipmentLogEvent? Parse(string line)
        {
            var m = _regex.Match(line);
            if (!m.Success) return null;

            var rest = m.Groups["rest"].Value;

            var evt = new EquipmentLogEvent
            {
                RawLine = line,
                Timestamp = DateTime.Parse(m.Groups["ts"].Value),
                EquipmentId = m.Groups["eq"].Value,
                // 아래는 rest 파싱
            };

            if (rest.StartsWith("Status="))
            {
                evt.EventType = "Status";
                evt.Status = rest.Substring("Status=".Length);
            }
            else if (rest.StartsWith("Alarm="))
            {
                evt.EventType = "Alarm";
                // Alarm=TEMP_HIGH Value=85
                // We need to extract just the code until space or end
                var alarmPart = rest.Substring("Alarm=".Length);
                var spaceIndex = alarmPart.IndexOf(' ');
                if (spaceIndex > 0)
                {
                    evt.AlarmCode = alarmPart.Substring(0, spaceIndex);
                }
                else
                {
                    evt.AlarmCode = alarmPart;
                }
            }

            // Value= 숫자 파싱
            var valMatch = Regex.Match(rest, @"Value=(\d+(\.\d+)?)");
            if (valMatch.Success)
                evt.Value = double.Parse(valMatch.Groups[1].Value);

            return evt;
        }
    }
}
