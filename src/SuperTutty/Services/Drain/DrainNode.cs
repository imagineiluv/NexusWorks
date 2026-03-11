using System.Collections.Concurrent;

namespace SuperTutty.Services.Drain
{
    public class DrainNode
    {
        public Dictionary<string, DrainNode> Children { get; } = new();
        public List<LogCluster> Clusters { get; } = new();
        public int Depth { get; set; }

        public DrainNode(int depth)
        {
            Depth = depth;
        }
    }
}
