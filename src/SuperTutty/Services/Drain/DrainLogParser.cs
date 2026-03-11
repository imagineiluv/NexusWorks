using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SuperTutty.Services.Drain
{
    public class DrainLogParser : IDisposable
    {
        private readonly int _depth;
        private readonly double _similarityThreshold;
        private readonly DrainNode _root;
        private readonly ConcurrentDictionary<int, LogCluster> _clusterMap = new();
        private int _nextClusterId = 1;

        // ReaderWriterLockSlim allows multiple readers (parsing existing templates)
        // but exclusive access for writers (creating new templates/updating).
        private readonly ReaderWriterLockSlim _rwLock = new();

        private readonly Regex _tokenRegex = new Regex(@"\s+");

        public DrainLogParser(int depth = 4, double similarityThreshold = 0.5)
        {
            _depth = depth;
            _similarityThreshold = similarityThreshold;
            _root = new DrainNode(0);
        }

        public LogCluster ParseLog(string logLine)
        {
            var tokens = _tokenRegex.Split(logLine.Trim()).ToList();

            // 1. Try Read Lock (Search existing)
            _rwLock.EnterUpgradeableReadLock();
            try
            {
                var cluster = TreeSearch(_root, tokens);
                if (cluster != null)
                {
                    // Update existing cluster
                    // We need write lock to update the cluster's statistics or template
                    _rwLock.EnterWriteLock();
                    try
                    {
                        UpdateCluster(cluster, tokens);
                        return cluster;
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                    }
                }

                // If not found, we need to add a new cluster
                _rwLock.EnterWriteLock();
                try
                {
                    // Re-check in case another thread added it while we were upgrading
                    cluster = TreeSearch(_root, tokens);
                    if (cluster != null)
                    {
                        UpdateCluster(cluster, tokens);
                        return cluster;
                    }

                    return AddCluster(tokens);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }
            finally
            {
                _rwLock.ExitUpgradeableReadLock();
            }
        }

        private LogCluster? TreeSearch(DrainNode root, List<string> tokens)
        {
            var node = root;

            // Level 1: Log Length
            string lengthToken = tokens.Count.ToString();
            if (!node.Children.TryGetValue(lengthToken, out var lengthNode))
            {
                return null;
            }
            node = lengthNode;

            // Navigate Tokens up to Depth
            for (int i = 0; i < Math.Min(tokens.Count, _depth - 2); i++)
            {
                string t = tokens[i];
                if (!node.Children.TryGetValue(t, out var child))
                {
                    return null;
                }
                node = child;
            }

            // Leaf Node
            LogCluster? bestMatch = null;
            double maxSim = -1;

            foreach (var cluster in node.Clusters)
            {
                double sim = GetSeqSimilarity(cluster.LogTemplateTokens, tokens);
                if (sim >= _similarityThreshold && sim > maxSim)
                {
                    maxSim = sim;
                    bestMatch = cluster;
                }
            }

            return bestMatch;
        }

        private LogCluster AddCluster(List<string> tokens)
        {
            var cluster = new LogCluster(_nextClusterId++, new List<string>(tokens));
            _clusterMap[cluster.Id] = cluster;

            var node = _root;

            // 1. Length Layer
            string lengthToken = tokens.Count.ToString();
            if (!node.Children.TryGetValue(lengthToken, out var lengthNode))
            {
                lengthNode = new DrainNode(1);
                node.Children[lengthToken] = lengthNode;
            }
            node = lengthNode;

            // 2. Token Layers
            for (int i = 0; i < Math.Min(tokens.Count, _depth - 2); i++)
            {
                string t = tokens[i];
                if (!node.Children.TryGetValue(t, out var child))
                {
                    child = new DrainNode(i + 2);
                    node.Children[t] = child;
                }
                node = child;
            }

            // Add to leaf
            node.Clusters.Add(cluster);
            return cluster;
        }

        private void UpdateCluster(LogCluster cluster, List<string> tokens)
        {
            cluster.Count++;
            for (int i = 0; i < Math.Min(cluster.LogTemplateTokens.Count, tokens.Count); i++)
            {
                if (cluster.LogTemplateTokens[i] != tokens[i])
                {
                    cluster.LogTemplateTokens[i] = "*";
                }
            }
            cluster.Template = string.Join(" ", cluster.LogTemplateTokens);
        }

        private double GetSeqSimilarity(List<string> templateTokens, List<string> logTokens)
        {
            if (templateTokens.Count != logTokens.Count) return 0;

            int match = 0;
            int paramCount = 0;

            for (int i = 0; i < templateTokens.Count; i++)
            {
                if (templateTokens[i] == "*")
                {
                    paramCount++;
                    continue;
                }
                if (templateTokens[i] == logTokens[i])
                {
                    match++;
                }
            }

            int totalStatic = templateTokens.Count - paramCount;
            if (totalStatic == 0) return 1.0;

            return (double)match / totalStatic;
        }

        public IReadOnlyCollection<LogCluster> GetClusters()
        {
            _rwLock.EnterReadLock();
            try
            {
                return _clusterMap.Values.ToList();
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            _rwLock.Dispose();
        }
    }
}
