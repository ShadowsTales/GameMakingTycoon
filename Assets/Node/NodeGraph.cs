// ============================================================
//  NodeGraph.cs  — NODE-TYCOON
//
//  FIXES IN THIS VERSION:
//    1. EvaluateTycoon: result.EngineNodes now populated
//    2. EvaluateTycoon: result.S_Staff = staffMultiplier now written
//    3. GetOptimizerBonusForNode: was walking incoming edges
//       (c.ToNodeId == nodeId) — always returned 0. Now correctly
//       follows the ExpandSlot output chain from the node outward.
//    4. Evaluate() legacy shim restored — was missing from this
//       file, causing compile error in NodeGraphController.
//    5. GameBuildResult.FeaturesByPillar populated in shim via
//       RebuildFeaturesByPillar() helper on GameBuildResult.
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class NodeGraph
{
    private readonly Dictionary<string, GameNode>       _nodes       = new();
    private readonly Dictionary<string, NodeConnection> _connections = new();

    public event Action<GameNode>       OnNodeAdded;
    public event Action<GameNode>       OnNodeRemoved;
    public event Action<NodeConnection> OnConnectionAdded;
    public event Action<NodeConnection> OnConnectionRemoved;
    public event Action<string>         OnValidationError;

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

    public bool RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return false;

        if (node.Kind == NodeKind.Core || node.Kind == NodeKind.Genre)
        {
            OnValidationError?.Invoke("Der Core-Node kann nicht gelöscht werden.");
            return false;
        }

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

    public NodeConnection TryConnect(NodePort fromPort, NodePort toPort)
    {
        if (fromPort.OwnerNode.NodeId == toPort.OwnerNode.NodeId)
        { OnValidationError?.Invoke("Self-Verbindung nicht erlaubt."); return null; }

        if (!fromPort.IsOutput || toPort.IsOutput)
        { OnValidationError?.Invoke("Verbindung muss von Output → Input gehen."); return null; }

        if (!PortCompatibility.IsCompatible(fromPort.Type, toPort.Type))
        {
            OnValidationError?.Invoke(
                PortCompatibility.GetIncompatibilityReason(fromPort.Type, toPort.Type));
            return null;
        }

        if (_connections.Values.Any(c => c.ToPortId == toPort.PortId))
        { OnValidationError?.Invoke("Input-Port bereits verbunden."); return null; }

        if (WouldCreateCycle(fromPort.OwnerNode.NodeId, toPort.OwnerNode.NodeId))
        { OnValidationError?.Invoke("Zyklische Verbindung nicht erlaubt."); return null; }

        if (_nodes.TryGetValue(toPort.OwnerNode.NodeId, out var toNode))
        {
            FeatureSO feat = toNode.GetFeatureData();
            if (feat != null)
            {
                string prereqError = CheckPrerequisites(feat);
                if (prereqError != null)
                { OnValidationError?.Invoke(prereqError); return null; }
            }
        }

        var conn = new NodeConnection(fromPort, toPort);
        _connections[conn.ConnectionId] = conn;
        OnConnectionAdded?.Invoke(conn);
        return conn;
    }

    public bool RemoveConnection(string connId)
    {
        if (!_connections.TryGetValue(connId, out var conn)) return false;
        _connections.Remove(connId);
        OnConnectionRemoved?.Invoke(conn);
        return true;
    }

    // ════════════════════════════════════════════════════════════════
    //  CYCLE DETECTION
    // ════════════════════════════════════════════════════════════════

    private bool WouldCreateCycle(string fromId, string toId)
    {
        var visited = new HashSet<string>();
        var queue   = new Queue<string>();
        queue.Enqueue(toId);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!visited.Add(cur)) continue;
            if (cur == fromId) return true;
            foreach (var c in _connections.Values.Where(c => c.FromNodeId == cur))
                queue.Enqueue(c.ToNodeId);
        }
        return false;
    }

    // ════════════════════════════════════════════════════════════════
    //  PREREQUISITE CHECK
    // ════════════════════════════════════════════════════════════════

    private string CheckPrerequisites(FeatureSO feature)
    {
        foreach (var req in feature.prerequisites)
        {
            bool found = _nodes.Values.Any(n => n.GetFeatureData() == req);
            if (!found)
                return $"Voraussetzung fehlt: '{req.featureName}' muss vor '{feature.featureName}' platziert werden.";
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════════
    //  EVALUATION
    // ════════════════════════════════════════════════════════════════

    public NodeTycoonBuildResult EvaluateGame(float cpuBudget, float staffMultiplier = 1f)
        => EvaluateTycoon(cpuBudget, staffMultiplier);

    public NodeTycoonBuildResult EvaluateTycoon(float cpuBudget, float staffMultiplier = 1f)
    {
        var result = new NodeTycoonBuildResult { CpuBudget = cpuBudget };

        var genreNode = _nodes.Values.OfType<GenreNode>().FirstOrDefault();
        var systems   = _nodes.Values.OfType<SystemNode>().ToList();
        var features  = _nodes.Values.OfType<GameFeatureNode>().ToList();
        var supports  = _nodes.Values.OfType<SupportNode>().ToList();
        var engines   = _nodes.Values.OfType<EngineNode>().ToList();
        var optims    = _nodes.Values.OfType<OptimizeNode>().ToList();

        result.AnchorNodes  = systems.OfType<AnchorNode>().ToList();
        result.UpgradeNodes = features.OfType<UpgradeNode>().ToList();
        result.SupportNodes = supports;
        result.EngineNodes  = engines;         // FIX 1: was never assigned
        result.S_Staff      = staffMultiplier; // FIX 2: was never written

        // ── Required Genre outputs ────────────────────────────────
        if (genreNode != null)
        {
            if (!genreNode.GameplayOut.IsConnected)
                result.MissingRequiredSlots.Add("⚡ GAMEPLAY — Gameplay-System fehlt");
            if (!genreNode.GraphicOut.IsConnected)
                result.MissingRequiredSlots.Add("🎨 GRAFIK — Renderer-EngineNode fehlt");
            if (!genreNode.SoundOut.IsConnected)
                result.MissingRequiredSlots.Add("🔊 SOUND — Audio-Node fehlt");
            if (result.MissingRequiredSlots.Count > 0)
                result.IsValid = false;
        }

        // ── Dev time ──────────────────────────────────────────────
        float devWeeks = 0f;
        foreach (var n in systems)  devWeeks += n.DevWeeks;
        foreach (var n in features) devWeeks += n.DevWeeks;
        foreach (var n in supports) devWeeks += n.DevWeeks;
        foreach (var n in engines)  devWeeks += n.DevWeeks;
        foreach (var n in optims)   devWeeks += n.DevTimeCost;
        result.TotalDevWeeks = devWeeks;

        // ── CPU ───────────────────────────────────────────────────
        float rawCpu = 0f;
        foreach (var n in systems)  rawCpu += n.FeatureData?.cpuUsage ?? 0f;
        foreach (var n in features) rawCpu += n.FeatureData?.cpuUsage ?? 0f;
        foreach (var n in supports) rawCpu += n.FeatureData?.cpuUsage ?? 0f;
        foreach (var n in engines)  rawCpu += n.CpuCost;

        float optimReduction = 0f;
        float optimQualBonus = 0f;
        foreach (var o in optims)
        {
            optimReduction     += o.CpuReductionPercent;
            optimQualBonus     += o.QualityBonus;
            result.DevTimeCost += o.DevTimeCost;
        }
        float effectiveCpu       = Mathf.Max(0f, rawCpu - optimReduction);
        result.TotalCpuUsage     = effectiveCpu;
        result.CpuReductionByOpt = optimReduction;

        // ── Pillar 1: Coherence / S_Fit (weight 0.45) ─────────────
        var allFeatureSOs = _nodes.Values
            .Select(n => n.GetFeatureData())
            .Where(f => f != null)
            .Distinct()
            .ToList();

        float synergyBonus    = 0f;
        float conflictPenalty = 0f;
        foreach (var feat in allFeatureSOs)
        {
            int syn = feat.synergyWith?.Count(s => allFeatureSOs.Contains(s)) ?? 0;
            synergyBonus             += syn * 0.08f;
            result.SynergyBonusTotal += syn * 0.08f;

            foreach (var cf in feat.conflictsWith ?? new List<FeatureSO>())
            {
                if (!allFeatureSOs.Contains(cf)) continue;
                conflictPenalty += 0.20f;
                result.IsValid   = false;
                var pair = (feat.featureName, cf.featureName);
                if (!result.ConflictPairs.Any(p =>
                    (p.A == pair.Item1 && p.B == pair.Item2) ||
                    (p.A == pair.Item2 && p.B == pair.Item1)))
                    result.ConflictPairs.Add(pair);
            }
        }

        float engineBonus  = Mathf.Min(engines.Count  * 0.05f, 0.30f);
        float supportBonus = Mathf.Min(supports.Count * 0.03f, 0.12f);
        float coherence    = Mathf.Clamp(
            1f + Mathf.Min(synergyBonus, 0.60f) + engineBonus + supportBonus - conflictPenalty,
            0f, 2f);
        result.S_Fit = coherence;

        // ── Pillar 2: Tech Fit / S_Tech (weight 0.35) ─────────────
        float techFit = 1f;
        if (effectiveCpu > cpuBudget)
        {
            float overrun        = effectiveCpu - cpuBudget;
            techFit              = Mathf.Max(0f, 1f - (overrun / cpuBudget) * 1.5f);
            result.IsValid       = false;
            result.CpuOverBudget = overrun;
        }

        var deployedComponents = new HashSet<string>(engines.Select(e => e.ComponentName));
        int missingEngineReqs  = 0;
        foreach (var fn in features)
        {
            if (string.IsNullOrEmpty(fn.RequiredEngineNodeId)) continue;
            if (deployedComponents.Contains(fn.RequiredEngineNodeId)) continue;
            missingEngineReqs++;
            result.MissingRequiredSlots.Add(
                $"⚙ {fn.FeatureData?.featureName} braucht Engine: {fn.RequiredEngineNodeId}");
            result.IsValid = false;
        }
        techFit       = Mathf.Max(0f, techFit - missingEngineReqs * 0.25f);
        techFit       = Mathf.Min(techFit + optimQualBonus, 1.5f);
        result.S_Tech = techFit;

        // ── Pillar 3: Depth / S_Quality (weight 0.20) ─────────────
        float depthRaw = 0f;
        foreach (var sys in systems)
        {
            float sc = sys.FeatureData?.cpuUsage ?? 0f;
            foreach (var fn in GetFeaturesForSystem(sys.NodeId))
            {
                sc += (fn.FeatureData?.cpuUsage ?? 0f) * 1.2f * (1f + GetOptimizerBonusForNode(fn.NodeId));
                result.UpgradeDepthTotal++;
            }
            depthRaw += sc;
        }
        foreach (var eng in engines)  depthRaw += eng.CpuCost * 0.9f;
        foreach (var su  in supports) depthRaw += (su.FeatureData?.cpuUsage ?? 0f) * 0.9f;

        bool hasContent = systems.Count > 0 || engines.Count > 0 || supports.Count > 0;
        float depth = hasContent
            ? Mathf.Clamp(depthRaw / Mathf.Max(cpuBudget * 0.8f, 1f), 0f, 2f)
            : 0f;
        result.S_Quality = depth;

        // ── Final score ────────────────────────────────────────────
        float weighted      = coherence * 0.45f + techFit * 0.35f + depth * 0.20f;
        result.FinalScore   = Mathf.Clamp(weighted * 50f * staffMultiplier, 0f, 100f);
        result.SelectedFeatures = allFeatureSOs;
        return result;
    }

    // ════════════════════════════════════════════════════════════════
    //  LEGACY SHIM
    //  FIX 4: was missing entirely from doc 1 — NodeGraphController
    //         calls _graph.Evaluate(), causing a compile error.
    //  FIX 5: now calls RebuildFeaturesByPillar() so FeaturesByPillar
    //         dict is populated rather than always empty.
    // ════════════════════════════════════════════════════════════════

    public GameBuildResult Evaluate(float maxCpu, float maxRam)
    {
        var r      = EvaluateGame(maxCpu);
        var legacy = new GameBuildResult
        {
            SelectedFeatures  = r.SelectedFeatures,
            TotalCpuUsage     = r.TotalCpuUsage,
            TotalRamUsage     = 0f,
            QualityScore      = r.FinalScore,
            IsValid           = r.IsValid,
            CpuOverBudget     = r.CpuOverBudget,
            RamOverBudget     = 0f,
            SynergyBonusTotal = r.SynergyBonusTotal,
            ConflictPairs     = r.ConflictPairs,
        };
        legacy.RebuildFeaturesByPillar(); // FIX 5: builds the dict
        return legacy;
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════

    private List<GameFeatureNode> GetFeaturesForSystem(string systemNodeId)
    {
        var result  = new List<GameFeatureNode>();
        var visited = new HashSet<string>();
        var queue   = new Queue<string>();
        queue.Enqueue(systemNodeId);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            foreach (var conn in _connections.Values.Where(c => c.FromNodeId == id))
            {
                if (_nodes.TryGetValue(conn.ToNodeId, out var n) && n is GameFeatureNode fn)
                { result.Add(fn); queue.Enqueue(fn.NodeId); }
            }
        }
        return result;
    }

    private List<UpgradeNode> GetUpgradesForAnchor(string anchorId)
        => GetFeaturesForSystem(anchorId).OfType<UpgradeNode>().ToList();

    // FIX 3: old version walked INCOMING edges (c.ToNodeId == nodeId)
    // which finds nodes pointing AT this node, not the optimizer chain
    // hanging off its ExpandSlot output. Now correctly follows the
    // outgoing ExpandSlot → OptimizerSlot chain up to depth 5.
    private float GetOptimizerBonusForNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return 0f;

        NodePort expandOut = node.OutputPorts.FirstOrDefault(p =>
            p.Type == PortType.ExpandSlot || p.Type == PortType.Expandable);
        if (expandOut == null) return 0f;

        float  bonus     = 0f;
        string outPortId = expandOut.PortId;

        for (int depth = 0; depth < 5; depth++)
        {
            var conn = _connections.Values.FirstOrDefault(c => c.FromPortId == outPortId);
            if (conn == null) break;

            if (_nodes.TryGetValue(conn.ToNodeId, out var ds) && ds is OptimizeNode opt)
            {
                bonus += opt.QualityBonus;
                // Advance to the optimizer's own ExpandSlot output to follow the chain
                var nextOut = opt.OutputPorts.FirstOrDefault(p => p.Type == PortType.ExpandSlot);
                if (nextOut == null) break;
                outPortId = nextOut.PortId;
            }
            else break;
        }
        return bonus;
    }
}

// ════════════════════════════════════════════════════════════════
//  NodeTycoonBuildResult
// ════════════════════════════════════════════════════════════════
public class NodeTycoonBuildResult
{
    // Node lists
    public List<AnchorNode>  AnchorNodes      = new();
    public List<UpgradeNode> UpgradeNodes     = new();
    public List<SupportNode> SupportNodes     = new();
    public List<EngineNode>  EngineNodes      = new(); // was missing from original
    public List<FeatureSO>   SelectedFeatures = new();

    // Resources
    public float CpuBudget;
    public float TotalCpuUsage;
    public float CpuReductionByOpt;
    public float CpuOverBudget;
    public float DevTimeCost;
    public float TotalDevWeeks;

    // Score pillars
    public float S_Fit;
    public float S_Quality;
    public float S_Tech;
    public float S_Staff = 1f;

    // Final grade
    public float FinalScore;

    // Detail / validation
    public float SynergyBonusTotal;
    public int   UpgradeDepthTotal;
    public bool  IsValid = true;
    public List<(string A, string B)> ConflictPairs        = new();
    public List<string>               MissingRequiredSlots = new();

    public string Summary =>
        $"Systeme: {AnchorNodes.Count} | Features: {UpgradeNodes.Count} | " +
        $"Engines: {EngineNodes.Count} | Support: {SupportNodes.Count} | " +
        $"CPU: {TotalCpuUsage:0}/{CpuBudget:0} | Score: {FinalScore:0.0}/100\n" +
        $"  S_Fit={S_Fit:0.00}  S_Quality={S_Quality:0.00}  " +
        $"S_Tech={S_Tech:0.00}  S_Staff={S_Staff:0.00}" +
        (MissingRequiredSlots.Count > 0
            ? "\n  ⚠ " + string.Join(" | ", MissingRequiredSlots)
            : "");
}

// ════════════════════════════════════════════════════════════════
//  GameBuildResult  —  Legacy shim
// ════════════════════════════════════════════════════════════════
public class GameBuildResult
{
    public List<FeatureSO>                                         SelectedFeatures = new();
    public Dictionary<FeatureSO.FeatureCategory, List<FeatureSO>> FeaturesByPillar = new();
    public float TotalCpuUsage;
    public float TotalRamUsage;
    public float QualityScore;
    public bool  IsValid = true;
    public float CpuOverBudget;
    public float RamOverBudget;
    public float SynergyBonusTotal;
    public List<(string A, string B)> ConflictPairs = new();

    public string Summary =>
        $"Features: {SelectedFeatures.Count} | CPU: {TotalCpuUsage:0}% | " +
        $"Qualität: {QualityScore:0.0}/100";

    /// <summary>
    /// Builds FeaturesByPillar from SelectedFeatures.
    /// Always call this after populating SelectedFeatures.
    /// </summary>
    public void RebuildFeaturesByPillar()
    {
        FeaturesByPillar.Clear();
        foreach (var f in SelectedFeatures)
        {
            if (!FeaturesByPillar.TryGetValue(f.category, out var list))
            {
                list = new List<FeatureSO>();
                FeaturesByPillar[f.category] = list;
            }
            list.Add(f);
        }
    }
}