// ============================================================
//  NodeGraph.cs  — NODE-TYCOON
//
//  Score-Formel (Design-Doc):
//    Gesamt-Score = (S_Fit + S_Quality + S_Tech) × S_Staff
//
//  S_Fit     — Genre-Passung: Synergien Anker↔Upgrade, Konflikte
//  S_Quality — Feature-Tiefe: Anzahl + Level der Upgrades pro Anker
//  S_Tech    — Lauffähigkeit: CPU-Budget-Einhaltung, Optimizer-Boni
//  S_Staff   — Mitarbeiter-Einfluss (extern injiziert, Standard 1.0)
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
        if (node.Kind == NodeKind.Core)
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
        // 1. Kein Self-Loop
        if (fromPort.OwnerNode.NodeId == toPort.OwnerNode.NodeId)
        { OnValidationError?.Invoke("Self-Verbindung nicht erlaubt."); return null; }

        // 2. Output → Input
        if (!fromPort.IsOutput || toPort.IsOutput)
        { OnValidationError?.Invoke("Verbindung muss von Output → Input gehen."); return null; }

        // 3. Port-Kompatibilität
        if (!PortCompatibility.IsCompatible(fromPort.Type, toPort.Type))
        { OnValidationError?.Invoke($"Inkompatible Ports: {fromPort.Type} → {toPort.Type}"); return null; }

        // 4. Input-Port bereits belegt?
        bool inputOccupied = _connections.Values.Any(c => c.ToPortId == toPort.PortId);
        if (inputOccupied)
        { OnValidationError?.Invoke("Input-Port bereits verbunden."); return null; }

        // 5. Zyklus-Check
        if (WouldCreateCycle(fromPort.OwnerNode.NodeId, toPort.OwnerNode.NodeId))
        { OnValidationError?.Invoke("Zyklische Verbindung nicht erlaubt."); return null; }

        // 6. Voraussetzungen
        if (_nodes.TryGetValue(toPort.OwnerNode.NodeId, out var toNode))
        {
            FeatureSO feat = null;
            if (toNode is AnchorNode  an) feat = an.FeatureData;
            if (toNode is UpgradeNode un) feat = un.FeatureData;
            if (toNode is SupportNode sn) feat = sn.FeatureData;

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
            bool found = _nodes.Values.Any(n =>
                (n is AnchorNode  a && a.FeatureData  == req) ||
                (n is UpgradeNode u && u.FeatureData  == req) ||
                (n is SupportNode s && s.FeatureData  == req));
            if (!found)
                return $"Voraussetzung fehlt: '{req.featureName}' muss vor '{feature.featureName}' platziert werden.";
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════════
    //  GRAPH EVALUATION  →  NodeTycoonBuildResult
    //
    //  RATING = Coherence × 0.45  +  TechFit × 0.35  +  Depth × 0.20
    //
    //  Coherence — synergies boost, conflicts penalise
    //  TechFit   — CPU overrun penalises, missing engine nodes penalise
    //              over-engineering is NOT penalised (sell it later!)
    //  Depth     — systems + features weighted by cpu value vs. budget
    // ════════════════════════════════════════════════════════════════

    /// <summary>Primary entry point — call this to score a finished graph.</summary>
    public NodeTycoonBuildResult EvaluateGame(float cpuBudget, float staffMultiplier = 1f)
        => EvaluateTycoon(cpuBudget, staffMultiplier);

    public NodeTycoonBuildResult EvaluateTycoon(float cpuBudget, float staffMultiplier = 1f)
    {
        var result = new NodeTycoonBuildResult { CpuBudget = cpuBudget };

        // ── Collect nodes (new types + legacy aliases) ────────────
        var genreNode = _nodes.Values.OfType<GenreNode>().FirstOrDefault();

        var systems  = _nodes.Values.OfType<SystemNode>().ToList();
        var features = _nodes.Values.OfType<GameFeatureNode>().ToList();
        var supports = _nodes.Values.OfType<SupportNode>().ToList();
        var engines  = _nodes.Values.OfType<EngineNode>().ToList();
        var optims   = _nodes.Values.OfType<OptimizeNode>().ToList();

        // Backward-compat: AnchorNode / UpgradeNode are subclasses — already captured above
        result.AnchorNodes  = systems.OfType<AnchorNode>().ToList();
        result.UpgradeNodes = features.OfType<UpgradeNode>().ToList();
        result.SupportNodes = supports;

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
            optimReduction       += o.CpuReductionPercent;
            optimQualBonus       += o.QualityBonus;
            result.DevTimeCost   += o.DevTimeCost;
        }
        float effectiveCpu        = Mathf.Max(0f, rawCpu - optimReduction);
        result.TotalCpuUsage      = effectiveCpu;
        result.CpuReductionByOpt  = optimReduction;

        // ═════════════════════════════════════════════════════════
        //  PILLAR 1 — COHERENCE  (weight 0.45)
        //  Synergies bonus, conflicts penalty
        // ═════════════════════════════════════════════════════════
        var allFeatureSOs = systems.Select(s => s.FeatureData)
            .Concat(features.Select(f => f.FeatureData))
            .Concat(supports.Select(s => s.FeatureData))
            .Where(f => f != null).ToList();

        float coherence = 1f;
        float synergyBonus  = 0f;
        float conflictPenalty = 0f;

        foreach (var feat in allFeatureSOs)
        {
            int syn = feat.synergyWith?.Count(s => allFeatureSOs.Contains(s)) ?? 0;
            synergyBonus += syn * 0.08f;
            result.SynergyBonusTotal += syn * 0.08f;

            foreach (var cf in feat.conflictsWith ?? new List<FeatureSO>())
            {
                if (!allFeatureSOs.Contains(cf)) continue;
                conflictPenalty += 0.20f;
                result.IsValid = false;
                var pair = (feat.featureName, cf.featureName);
                if (!result.ConflictPairs.Any(p => (p.A == pair.Item1 && p.B == pair.Item2) ||
                                                   (p.A == pair.Item2 && p.B == pair.Item1)))
                    result.ConflictPairs.Add(pair);
            }
        }
        coherence = Mathf.Clamp(1f + Mathf.Min(synergyBonus, 0.60f) - conflictPenalty, 0f, 2f);
        result.S_Fit = coherence;

        // ═════════════════════════════════════════════════════════
        //  PILLAR 2 — TECH FIT  (weight 0.35)
        //  CPU overrun and missing engine nodes penalise.
        //  Over-engineering does NOT penalise — sell it later!
        // ═════════════════════════════════════════════════════════
        float techFit = 1f;

        if (effectiveCpu > cpuBudget)
        {
            float overrun = effectiveCpu - cpuBudget;
            techFit = Mathf.Max(0f, 1f - (overrun / cpuBudget) * 1.5f);
            result.IsValid = false;
            result.CpuOverBudget = overrun;
        }

        // Features that need a specific EngineNode deployed
        var deployedComponents = new HashSet<string>(engines.Select(e => e.ComponentName));
        int missingEngineReqs  = 0;
        foreach (var fn in features)
        {
            if (string.IsNullOrEmpty(fn.RequiredEngineNodeId)) continue;
            if (deployedComponents.Contains(fn.RequiredEngineNodeId)) continue;
            missingEngineReqs++;
            result.MissingRequiredSlots.Add($"⚙ {fn.FeatureData?.featureName} braucht Engine: {fn.RequiredEngineNodeId}");
            result.IsValid = false;
        }
        techFit  = Mathf.Max(0f, techFit - missingEngineReqs * 0.25f);
        techFit  = Mathf.Min(techFit + optimQualBonus, 1.5f);   // optimizer quality bonus
        result.S_Tech = techFit;

        // ═════════════════════════════════════════════════════════
        //  PILLAR 3 — DEPTH  (weight 0.20)
        //  Systems + features weighted by CPU value vs. budget
        // ═════════════════════════════════════════════════════════
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
        foreach (var su in supports)
            depthRaw += (su.FeatureData?.cpuUsage ?? 0f) * 0.8f;

        float depth = systems.Count > 0
            ? Mathf.Clamp(depthRaw / Mathf.Max(cpuBudget * 0.8f, 1f), 0f, 2f)
            : 0f;
        result.S_Quality = depth;

        // ═════════════════════════════════════════════════════════
        //  FINAL SCORE
        //  Each pillar is 0–2; weighted sum → normalise to 0–100
        // ═════════════════════════════════════════════════════════
        float weighted = coherence * 0.45f + techFit * 0.35f + depth * 0.20f;
        result.FinalScore       = Mathf.Clamp(weighted * 50f * staffMultiplier, 0f, 100f);
        result.SelectedFeatures = allFeatureSOs;
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>Walk FeatureSlot chain from a SystemNode, return all FeatureNodes.</summary>
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

    // Legacy alias
    private List<UpgradeNode> GetUpgradesForAnchor(string anchorId)
        => GetFeaturesForSystem(anchorId).OfType<UpgradeNode>().ToList();

    private float GetOptimizerBonusForNode(string nodeId)
    {
        float bonus = 0f;
        if (!_nodes.TryGetValue(nodeId, out var node)) return bonus;

        // Find an ExpandSlot output on this node
        NodePort expandOut = node.OutputPorts.FirstOrDefault(p =>
            p.Type == PortType.ExpandSlot || p.Type == PortType.Expandable);
        if (expandOut == null) return bonus;

        string outPortId = expandOut.PortId;
        for (int depth = 0; depth < 5; depth++)
        {
            var conn = _connections.Values.FirstOrDefault(c => c.FromPortId == outPortId);
            if (conn == null) break;
            if (_nodes.TryGetValue(conn.ToNodeId, out var ds) && ds is OptimizeNode opt)
            {
                bonus += opt.QualityBonus;
                var nextOut = opt.OutputPorts.FirstOrDefault();
                if (nextOut == null) break;
                outPortId = nextOut.PortId;
            }
            else break;
        }
        return bonus;
    }

    // Legacy shim — alter Code ruft Evaluate(maxCpu, maxRam)
    public GameBuildResult Evaluate(float maxCpu, float maxRam)
    {
        var r = EvaluateGame(maxCpu);
        return new GameBuildResult
        {
            SelectedFeatures  = r.SelectedFeatures,
            TotalCpuUsage     = r.TotalCpuUsage,
            TotalRamUsage     = 0f,
            QualityScore      = r.FinalScore,
            IsValid           = r.IsValid,
            CpuOverBudget     = r.CpuOverBudget,
            SynergyBonusTotal = r.SynergyBonusTotal,
            ConflictPairs     = r.ConflictPairs,
        };
    }
}

// ════════════════════════════════════════════════════════════════
//  NodeTycoonBuildResult
// ════════════════════════════════════════════════════════════════
public class NodeTycoonBuildResult
{
    // Pillar-Listen
    public List<AnchorNode>  AnchorNodes  = new();
    public List<UpgradeNode> UpgradeNodes = new();
    public List<SupportNode> SupportNodes = new();
    public List<FeatureSO>   SelectedFeatures = new();

    // Ressourcen
    public float CpuBudget;
    public float TotalCpuUsage;
    public float CpuReductionByOpt;
    public float CpuOverBudget;
    public float DevTimeCost;   // Zusatz-Wochen durch Optimizer

    // Score-Säulen
    public float S_Fit;
    public float S_Quality;
    public float S_Tech;
    public float S_Staff = 1f;  // Von außen gesetzt

    // Endnote
    public float FinalScore;    // 0–100

    // Details
    public float SynergyBonusTotal;
    public int   UpgradeDepthTotal;
    public bool  IsValid = true;
    public List<(string A, string B)> ConflictPairs = new();

    // Pflicht-Ports die noch fehlen (leer = alles verbunden)
    public List<string> MissingRequiredSlots = new();

    // Gesamte Entwicklungszeit in Wochen (alle Nodes + Optimizer)
    public float TotalDevWeeks;

    public string Summary =>
        $"Anker: {AnchorNodes.Count} | Upgrades: {UpgradeNodes.Count} | " +
        $"Support: {SupportNodes.Count} | CPU: {TotalCpuUsage:0}/{CpuBudget:0} | " +
        $"Score: {FinalScore:0.0}/100\n" +
        $"  S_Fit={S_Fit:0.00}  S_Quality={S_Quality:0.00}  S_Tech={S_Tech:0.00}  S_Staff={S_Staff:0.00}";
}

// ════════════════════════════════════════════════════════════════
//  Legacy shim — GameBuildResult wird noch von UIController u.a. genutzt
// ════════════════════════════════════════════════════════════════
public class GameBuildResult
{
    public List<FeatureSO>                                        SelectedFeatures  = new();
    public Dictionary<FeatureSO.FeatureCategory, List<FeatureSO>> FeaturesByPillar  = new();
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
}