// ============================================================
//  GameNode.cs
//  Base class for all nodes in the Game Creator graph.
//  Subclasses: PillarStartNode, FeatureGameNode, OptimizerNode
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public enum NodeKind { PillarStart, Feature, Optimizer }

[Serializable]
public abstract class GameNode
{
    public string   NodeId;
    public NodeKind Kind;
    public Vector2  CanvasPosition;  // UI position on the canvas

    public List<NodePort> InputPorts  = new List<NodePort>();
    public List<NodePort> OutputPorts = new List<NodePort>();

    protected GameNode(NodeKind kind)
    {
        NodeId = Guid.NewGuid().ToString();
        Kind   = kind;
    }

    /// <summary>Human-readable name shown in the node header.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Pillar this node belongs to (used for colour coding).</summary>
    public abstract FeatureSO.FeatureCategory Pillar { get; }

    // ── Port helpers ──────────────────────────────────────────────
    protected NodePort AddInput(string label, PortType type)
    {
        var p = new NodePort($"{NodeId}_in_{InputPorts.Count}", label, type, false, this);
        InputPorts.Add(p);
        return p;
    }

    protected NodePort AddOutput(string label, PortType type)
    {
        var p = new NodePort($"{NodeId}_out_{OutputPorts.Count}", label, type, true, this);
        OutputPorts.Add(p);
        return p;
    }

    public NodePort GetPort(string portId)
    {
        foreach (var p in InputPorts)  if (p.PortId == portId) return p;
        foreach (var p in OutputPorts) if (p.PortId == portId) return p;
        return null;
    }
}

// ============================================================
//  PillarStartNode
//  One per pillar (Gameplay / Grafik / Sound / Tech).
//  Fixed — cannot be deleted or moved beyond its anchor column.
//  Has ONE output: PillarRoot → accepts FeatureCore inputs.
// ============================================================
public class PillarStartNode : GameNode
{
    private readonly FeatureSO.FeatureCategory _pillar;

    public PillarStartNode(FeatureSO.FeatureCategory pillar) : base(NodeKind.PillarStart)
    {
        _pillar = pillar;
        AddOutput("Start", PortType.PillarRoot);
    }

    public override string DisplayName => _pillar.ToString() + " — START";
    public override FeatureSO.FeatureCategory Pillar => _pillar;
}

// ============================================================
//  FeatureGameNode
//  Wraps a FeatureSO.  Ports are derived from the SO data.
//
//  PORT RULES:
//   • Always has 1 input:  FeatureCore  (to receive chain signal)
//   • Always has 1 output: FeatureCore  (to chain further features)
//   • If canExpand==true:  1 extra output: Expandable
//   • Per prerequisite:    1 extra input: Prerequisite
//   • Cross-pillar:        determined by category
// ============================================================
public class FeatureGameNode : GameNode
{
    public readonly FeatureSO Feature;

    // Cached typed ports for external access
    public NodePort CoreIn  { get; private set; }
    public NodePort CoreOut { get; private set; }
    public NodePort ExpandOut { get; private set; }   // null if !canExpand

    public FeatureGameNode(FeatureSO feature) : base(NodeKind.Feature)
    {
        Feature = feature;
        BuildPorts();
    }

    private void BuildPorts()
    {
        // ── Inputs ───────────────────────────────────────────────
        CoreIn = AddInput("In", PortType.FeatureCore);

        // One dedicated Prerequisite input per required dependency
        foreach (var req in Feature.prerequisites)
            AddInput($"Needs: {req.featureName}", PortType.Prerequisite);

        // Cross-pillar receive slots
        if (Feature.category == FeatureSO.FeatureCategory.Gameplay)
            AddInput("Audio Trigger", PortType.AudioTrigger);

        if (Feature.category == FeatureSO.FeatureCategory.Tech)
        {
            AddInput("Render Pass", PortType.RenderPass);
            AddInput("Data Feed",   PortType.DataFeed);
        }

        // Narrative receives DataFeed from Tech (e.g. save-state, analytics)
        if (Feature.category == FeatureSO.FeatureCategory.Narrative)
            AddInput("Data Feed", PortType.DataFeed);

        // UX receives DataFeed from Tech (analytics → adaptive UI)
        if (Feature.category == FeatureSO.FeatureCategory.UX)
            AddInput("Data Feed", PortType.DataFeed);

        // ── Outputs ──────────────────────────────────────────────
        CoreOut = AddOutput("Out", PortType.FeatureCore);

        if (Feature.canExpand)
            ExpandOut = AddOutput("Expand", PortType.Expandable);

        // Cross-pillar emit slots
        if (Feature.category == FeatureSO.FeatureCategory.Sound)
            AddOutput("Audio Trigger →", PortType.AudioTrigger);

        if (Feature.category == FeatureSO.FeatureCategory.Graphic)
            AddOutput("Render Pass →", PortType.RenderPass);

        if (Feature.category == FeatureSO.FeatureCategory.Tech)
            AddOutput("Data Feed →", PortType.DataFeed);

        // Narrative emits DataFeed (story state → UX adapts, tech logs)
        if (Feature.category == FeatureSO.FeatureCategory.Narrative)
            AddOutput("Story Feed →", PortType.DataFeed);

        // UX emits DataFeed (UI metrics → Tech analytics)
        if (Feature.category == FeatureSO.FeatureCategory.UX)
            AddOutput("UX Feed →", PortType.DataFeed);
    }

    public override string DisplayName => Feature.featureName;
    public override FeatureSO.FeatureCategory Pillar => Feature.category;
}

// ============================================================
//  OptimizerNode
//  Can only attach to an Expandable output.
//  Reduces effective CPU cost and boosts quality score.
// ============================================================
public class OptimizerNode : GameNode
{
    [Header("Optimizer Settings")]
    public float CpuReductionPercent = 15f;  // % reduction applied to parent feature
    public float QualityBonus        = 0.1f; // Added to final quality multiplier

    private readonly FeatureSO.FeatureCategory _pillar;

    public OptimizerNode(FeatureSO.FeatureCategory pillar) : base(NodeKind.Optimizer)
    {
        _pillar = pillar;
        AddInput("Optimize",  PortType.Optimizer);   // receives Expandable signal
        AddOutput("Optimized", PortType.Expandable); // can chain another optimizer
    }

    public override string DisplayName => "Optimizer";
    public override FeatureSO.FeatureCategory Pillar => _pillar;
}