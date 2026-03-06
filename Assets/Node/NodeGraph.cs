// ============================================================
//  NodeGraph.cs
//  The core data model for the Game Creator node graph.
//
//  Responsibilities:
//   • Hold all nodes and connections
//   • Validate wire attempts (PortCompatibility + prerequisite check)
//   • Evaluate the graph → produce GameBuildResult
//   • Detect cycles (prevent infinite loops)
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class NodeGraph
{
    // ── Storage ──────────────────────────────────────────────────────
    private readonly Dictionary<string, GameNode>       _nodes       = new();
    private readonly Dictionary<string, NodeConnection> _connections = new();

    // ── Events ───────────────────────────────────────────────────────
    public event Action<GameNode>       OnNodeAdded;
    public event Action<GameNode>       OnNodeRemoved;
    public event Action<NodeConnection> OnConnectionAdded;
    public event Action<NodeConnection> OnConnectionRemoved;
    public event Action<string>         OnValidationError;  // message for UI toast

    // ── Read-only views ──────────────────────────────────────────────
    public IEnumerable<GameNode>       AllNodes       => _nodes.Values;
    public IEnumerable<NodeConnection> AllConnections => _connections.Values;

    // ════════════════════════════════════════════════════════════════
    //  NODE MANAGEMENT
    // ════════════════════════════════════════════════════════════════

    public void AddNode(GameNode node)
    {
        if (_nodes.ContainsKey(node.NodeId)) return;
        _nodes[node.NodeId] = node;
        OnNodeAdded?.Invoke(node);
    }

    /// <summary>
    /// Removes a node AND all its connections.
    /// Pillar start nodes cannot be removed.
    /// </summary>
    public bool RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return false;
        if (node.Kind == NodeKind.PillarStart)
        {
            OnValidationError?.Invoke("Pillar Start nodes cannot be deleted.");
            return false;
        }

        // Remove all connections that touch this node
        var toRemove = _connections.Values
            .Where(c => c.FromNodeId == nodeId || c.ToNodeId == nodeId)
            .Select(c => c.ConnectionId)
            .ToList();

        foreach (var cid in toRemove) RemoveConnection(cid);

        _nodes.Remove(nodeId);
        OnNodeRemoved?.Invoke(node);
        return true;
    }

    // ════════════════════════════════════════════════════════════════
    //  CONNECTION MANAGEMENT
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempts to create a connection.  Returns null and fires
    /// OnValidationError if validation fails.
    /// </summary>
    public NodeConnection TryConnect(NodePort fromPort, NodePort toPort)
    {
        // 1. Direction check
        if (!fromPort.IsOutput || toPort.IsOutput)
        {
            OnValidationError?.Invoke("Connect from an Output port to an Input port.");
            return null;
        }

        // 2. Self-loop check
        if (fromPort.OwnerNode == toPort.OwnerNode)
        {
            OnValidationError?.Invoke("A node cannot connect to itself.");
            return null;
        }

        // 3. Port type compatibility
        if (!PortCompatibility.IsCompatible(fromPort.Type, toPort.Type))
        {
            OnValidationError?.Invoke(
                PortCompatibility.GetIncompatibilityReason(fromPort.Type, toPort.Type));
            return null;
        }

        // 4. Duplicate connection check
        if (_connections.Values.Any(c =>
                c.FromPortId == fromPort.PortId && c.ToPortId == toPort.PortId))
        {
            OnValidationError?.Invoke("These ports are already connected.");
            return null;
        }

        // 5. Input-port already occupied (inputs accept only 1 connection)
        if (_connections.Values.Any(c => c.ToPortId == toPort.PortId))
        {
            OnValidationError?.Invoke($"Input port '{toPort.Label}' is already in use.");
            return null;
        }

        // 6. Cycle detection (prevent A→B→C→A)
        if (WouldCreateCycle(fromPort.OwnerNode, toPort.OwnerNode))
        {
            OnValidationError?.Invoke("This connection would create a cycle.");
            return null;
        }

        // 7. Prerequisite satisfaction check
        if (toPort.OwnerNode is FeatureGameNode featureNode)
        {
            string prereqError = CheckPrerequisites(featureNode);
            if (prereqError != null)
            {
                OnValidationError?.Invoke(prereqError);
                return null;
            }
        }

        // ── All checks passed — create connection ──
        var conn = new NodeConnection(fromPort, toPort);
        _connections[conn.ConnectionId] = conn;
        OnConnectionAdded?.Invoke(conn);
        return conn;
    }

    public bool RemoveConnection(string connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var conn)) return false;

        conn.FromPort.IsConnected = _connections.Values
            .Any(c => c.ConnectionId != connectionId && c.FromPortId == conn.FromPortId);
        conn.ToPort.IsConnected   = _connections.Values
            .Any(c => c.ConnectionId != connectionId && c.ToPortId   == conn.ToPortId);

        _connections.Remove(connectionId);
        OnConnectionRemoved?.Invoke(conn);
        return true;
    }

    // ════════════════════════════════════════════════════════════════
    //  VALIDATION HELPERS
    // ════════════════════════════════════════════════════════════════

    private bool WouldCreateCycle(GameNode from, GameNode to)
    {
        // BFS/DFS: can we reach 'from' by traversing forward from 'to'?
        var visited = new HashSet<string>();
        var queue   = new Queue<string>();
        queue.Enqueue(to.NodeId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (current == from.NodeId) return true;
            if (!visited.Add(current)) continue;

            foreach (var conn in _connections.Values.Where(c => c.FromNodeId == current))
                queue.Enqueue(conn.ToNodeId);
        }
        return false;
    }

    /// <summary>
    /// Returns an error message if the feature's prerequisites are not
    /// yet present (and connected) in the graph, null if all satisfied.
    /// </summary>
    private string CheckPrerequisites(FeatureGameNode node)
    {
        foreach (var req in node.Feature.prerequisites)
        {
            bool found = _nodes.Values
                .OfType<FeatureGameNode>()
                .Any(n => n.Feature == req);

            if (!found)
                return $"Missing prerequisite: '{req.featureName}' must be placed before '{node.Feature.featureName}'.";
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════════
    //  GRAPH EVALUATION  →  GameBuildResult
    // ════════════════════════════════════════════════════════════════

    public GameBuildResult Evaluate(float maxCpu, float maxRam)
    {
        var result = new GameBuildResult();

        // Collect all feature nodes reachable from any PillarStart
        var reachable = GetReachableFeatureNodes();

        var featureSet = reachable.Select(n => n.Feature).ToList();

        foreach (var fn in reachable)
        {
            result.SelectedFeatures.Add(fn.Feature);
            result.TotalCpuUsage += fn.Feature.cpuUsage;
            result.TotalRamUsage += fn.Feature.ramUsage;

            // Conflict check — mark invalid and record which pair conflicts
            if (fn.Feature.conflictsWith != null)
            {
                foreach (var conflict in fn.Feature.conflictsWith)
                {
                    if (featureSet.Contains(conflict))
                    {
                        result.ConflictPairs.Add((fn.Feature.featureName, conflict.featureName));
                        result.IsValid = false;
                    }
                }
            }

            // Synergy bonus — each matching partner adds to quality
            float synergyMult = 1f;
            if (fn.Feature.synergyWith != null)
            {
                int matches = fn.Feature.synergyWith.Count(s => featureSet.Contains(s));
                synergyMult += Mathf.Min(matches * 0.10f, 0.50f);
                if (matches > 0) result.SynergyBonusTotal += matches * 0.10f;
            }

            float optimizerBonus = GetOptimizerBonus(fn);
            result.QualityScore += fn.Feature.cpuUsage * (1f + optimizerBonus) * synergyMult;

            result.FeaturesByPillar
                .GetOrCreate(fn.Feature.category)
                .Add(fn.Feature);
        }

        bool budgetOk        = result.TotalCpuUsage <= maxCpu && result.TotalRamUsage <= maxRam;
        result.IsValid       = result.IsValid && budgetOk;
        result.CpuOverBudget = result.TotalCpuUsage - maxCpu;
        result.RamOverBudget = result.TotalRamUsage - maxRam;

        // Normalize quality 0–100
        float maxPossible = _nodes.Values.OfType<FeatureGameNode>()
                                         .Sum(n => n.Feature.cpuUsage * 1.5f);
        result.QualityScore = maxPossible > 0
            ? Mathf.Clamp01(result.QualityScore / maxPossible) * 100f
            : 0f;

        return result;
    }

    private List<FeatureGameNode> GetReachableFeatureNodes()
    {
        var reachable = new List<FeatureGameNode>();
        var visited   = new HashSet<string>();
        var queue     = new Queue<string>();

        // Seed from all Pillar Start nodes
        foreach (var n in _nodes.Values.Where(n => n.Kind == NodeKind.PillarStart))
            queue.Enqueue(n.NodeId);

        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!visited.Add(id)) continue;

            if (_nodes[id] is FeatureGameNode fn)
                reachable.Add(fn);

            foreach (var conn in _connections.Values.Where(c => c.FromNodeId == id))
                if (_nodes.ContainsKey(conn.ToNodeId))
                    queue.Enqueue(conn.ToNodeId);
        }

        return reachable;
    }

    private float GetOptimizerBonus(FeatureGameNode node)
    {
        float bonus = 0f;
        if (node.ExpandOut == null) return bonus;

        // Walk Expand chain
        var portQueue = new Queue<string>();
        portQueue.Enqueue(node.ExpandOut.PortId);

        while (portQueue.Count > 0)
        {
            string outPortId = portQueue.Dequeue();
            var conn = _connections.Values.FirstOrDefault(c => c.FromPortId == outPortId);
            if (conn == null) break;

            if (_nodes.TryGetValue(conn.ToNodeId, out var downstream) &&
                downstream is OptimizerNode opt)
            {
                bonus += opt.QualityBonus;

                // Chain: optimizer's output can feed into another optimizer
                var nextOut = opt.OutputPorts.FirstOrDefault();
                if (nextOut != null) portQueue.Enqueue(nextOut.PortId);
            }
        }
        return bonus;
    }
}

// ════════════════════════════════════════════════════════════════
//  GameBuildResult  (output of graph evaluation)
// ════════════════════════════════════════════════════════════════
public class GameBuildResult
{
    public List<FeatureSO>                                        SelectedFeatures  = new();
    public Dictionary<FeatureSO.FeatureCategory, List<FeatureSO>> FeaturesByPillar  = new();
    public float TotalCpuUsage;
    public float TotalRamUsage;
    public float QualityScore;
    public bool  IsValid = true;   // starts true; set false on conflict or budget breach
    public float CpuOverBudget;
    public float RamOverBudget;
    public float SynergyBonusTotal;                              // NEW
    public List<(string A, string B)> ConflictPairs = new();    // NEW

    public string Summary =>
        $"Features: {SelectedFeatures.Count} | CPU: {TotalCpuUsage:0}% | " +
        $"RAM: {TotalRamUsage:0}% | Quality: {QualityScore:0.0}/100" +
        (SynergyBonusTotal > 0 ? $" | Synergien: +{SynergyBonusTotal*100:0}%" : "") +
        (ConflictPairs.Count > 0 ? $" | ⚠ {ConflictPairs.Count} Konflikt(e)" : "");
}

// ── Small Dictionary extension ───────────────────────────────────
public static class DictionaryExtensions
{
    public static TValue GetOrCreate<TKey, TValue>(
        this Dictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        if (!dict.TryGetValue(key, out var val))
        {
            val = new TValue();
            dict[key] = val;
        }
        return val;
    }
}