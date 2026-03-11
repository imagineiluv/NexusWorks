using System;
using System.Text.RegularExpressions;
using SuperTutty.Models;

namespace SuperTutty.Parsers
{
    public class ProcessLogParser
    {
        private readonly Regex _regex = new Regex(
            @"^(?<ts>\S+\s+\S+)\s+(?<level>\S+)\s+\[TX=(?<tx>\w+)\]\s+(?<msg>.+)$",
            RegexOptions.Compiled);

        public ProcessLogEvent? Parse(string line)
        {
            var m = _regex.Match(line);
            if (!m.Success) return null;

            var evt = new ProcessLogEvent
            {
                RawLine = line,
                Timestamp = DateTime.Parse(m.Groups["ts"].Value),
                Level = m.Groups["level"].Value,
                TransactionId = m.Groups["tx"].Value,
                Message = m.Groups["msg"].Value
            };

            // 메시지에서 Step 정보 따로 파싱 가능
            // ex) "Step=VALIDATE ..." → Step 추출
            var stepMatch = Regex.Match(evt.Message, @"Step=(\w+)");
            if (stepMatch.Success)
                evt.Step = stepMatch.Groups[1].Value;

            return evt;
        }
    }
}
