using System.Collections.Concurrent;

namespace SuperTutty.Services.Drain
{
    public class LogCluster
    {
        public int Id { get; set; }
        public string Template { get; set; } = "";
        public int Count { get; set; }
        public List<string> LogTemplateTokens { get; set; } = new();

        public LogCluster(int id, List<string> logTemplateTokens)
        {
            Id = id;
            LogTemplateTokens = logTemplateTokens;
            Template = string.Join(" ", logTemplateTokens);
            Count = 1;
        }

        public override string ToString() => $"[ID={Id}, Count={Count}] {Template}";
    }
}
