// ============================================================
//  GameNode.cs  —  NODE-TYCOON  (Loop Architecture)
//
//  FIXES IN THIS VERSION:
//    • SupportNode.BindOut removed — Support nodes are leaves;
//      they receive from Genre and have no meaningful output
//      target. The dead BindOut port was using SupportSlot as
//      an output which can never satisfy any input rule.
//      If chaining support nodes is ever needed, add a new
//      PortType.SupportChainSlot with explicit rules.
//    • All node types now implement a GetFeatureData() helper
//      via the base class so callers don't need type-switches.
//    • EngineNode inSlot corrected: Sound-category engines
//      accept SoundSlot (already correct) but the fallback
//      for non-Graphic/non-Sound now explicitly uses EngineSlot
//      (was also correct, documented more clearly).
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public enum NodeKind
{
    Genre,      // replaces Core / GenreNode
    System,     // replaces Anchor
    Feature,    // replaces Upgrade
    Engine,     // deployed engine component
    Support,    // replaces Support (narrative/UX global)
    Optimize,   // replaces Optimizer
    // Legacy aliases kept so existing code compiles
    Core, Anchor, Upgrade, Optimizer,
    PillarStart,
}

[Serializable]
public struct StatBonus
{
    public FeatureSO.FeatureCategory Category;
    public int Value;
    public StatBonus(FeatureSO.FeatureCategory cat, int val) { Category = cat; Value = val; }
    public string Label => Value > 0 ? $"+{Value} {Category}" : "";
}

[Serializable]
public abstract class GameNode
{
    public string   NodeId;
    public NodeKind Kind;
    public Vector2  CanvasPosition;
    public List<NodePort> InputPorts  = new List<NodePort>();
    public List<NodePort> OutputPorts = new List<NodePort>();

    protected GameNode(NodeKind kind)
    { NodeId = Guid.NewGuid().ToString(); Kind = kind; }

    public abstract string DisplayName { get; }
    public abstract FeatureSO.FeatureCategory Pillar { get; }
    public virtual float     DevWeeks         => 0f;
    public virtual StatBonus PrimaryStatBonus => default;

    /// <summary>
    /// Returns the FeatureSO associated with this node, or null for
    /// GenreNode / EngineNode / OptimizeNode which don't have one.
    /// Avoids type-switch boilerplate everywhere else in the codebase.
    /// </summary>
    public virtual FeatureSO GetFeatureData() => null;

    protected NodePort AddInput(string label, PortType type)
    { var p = new NodePort($"{NodeId}_in_{InputPorts.Count}", label, type, false, this); InputPorts.Add(p); return p; }
    protected NodePort AddOutput(string label, PortType type)
    { var p = new NodePort($"{NodeId}_out_{OutputPorts.Count}", label, type, true, this); OutputPorts.Add(p); return p; }
    public NodePort GetPort(string id)
    { foreach (var p in InputPorts)  if (p.PortId == id) return p;
      foreach (var p in OutputPorts) if (p.PortId == id) return p;
      return null; }
}

// ============================================================
//  GenreNode  —  THE GAME LOOP
// ============================================================
public class GenreNode : GameNode
{
    public string    ProjectName;
    public string    Platform;
    public float     CpuBudget;
    public GenreSO   Genre;

    public string    TriggerLabel    = "Trigger";
    public string    ResolutionLabel = "Resolution";

    public NodePort GameplayOut { get; private set; }
    public NodePort GraphicOut  { get; private set; }
    public NodePort SoundOut    { get; private set; }
    public NodePort SystemOut   { get; private set; }
    public NodePort SupportOut  { get; private set; }

    public GenreNode(string projectName, string platform, float cpuBudget, GenreSO genre = null)
        : base(NodeKind.Genre)
    {
        ProjectName = projectName;
        Platform    = platform;
        CpuBudget   = cpuBudget;
        Genre       = genre;

        if (genre != null)
        {
            (TriggerLabel, ResolutionLabel) = genre.genreName?.ToLower() switch
            {
                var g when g.Contains("rpg")        => ("Quest erhalten",          "Quest abgeschlossen"),
                var g when g.Contains("action")     => ("Bedrohung",               "Sieg / Überleben"),
                var g when g.Contains("puzzle")     => ("Problem-Zustand",         "Lösung gefunden"),
                var g when g.Contains("strategy")   => ("Ressourcen-Druck",        "Entscheidung + Konsequenz"),
                var g when g.Contains("simulation") => ("System-Ungleichgewicht",  "Gleichgewicht"),
                var g when g.Contains("adventure")  => ("Entdeckung",              "Fortschritt"),
                _                                   => ("Spieler-Aktion",          "Spieler-Belohnung"),
            };
        }

        GameplayOut = AddOutput("⚡ GAMEPLAY  [Pflicht]",  PortType.GameplaySlot);
        GraphicOut  = AddOutput("🎨 GRAFIK    [Pflicht]",  PortType.GraphicSlot);
        SoundOut    = AddOutput("🔊 SOUND     [Pflicht]",  PortType.SoundSlot);
        SystemOut   = AddOutput("＋ System    [Optional]", PortType.SystemSlot);
        SupportOut  = AddOutput("＋ Support   [Optional]", PortType.SupportSlot);
    }

    public override string DisplayName => ProjectName;
    public override FeatureSO.FeatureCategory Pillar => FeatureSO.FeatureCategory.Gameplay;
    public bool AllRequiredConnected =>
        GameplayOut.IsConnected && GraphicOut.IsConnected && SoundOut.IsConnected;
}

// ============================================================
//  SystemNode  —  A major mechanic
// ============================================================
public class SystemNode : GameNode
{
    public FeatureSO FeatureData { get; }
    public NodePort GenreIn    { get; private set; }
    public NodePort FeatureOut { get; private set; }
    public NodePort ExpandOut  { get; private set; }

    public SystemNode(FeatureSO feature) : base(NodeKind.System)
    {
        FeatureData = feature;
        (PortType inType, string inLabel) = feature.category switch
        {
            FeatureSO.FeatureCategory.Gameplay => (PortType.GameplaySlot, "← Genre  [⚡ GAMEPLAY]"),
            FeatureSO.FeatureCategory.Sound    => (PortType.SoundSlot,    "← Genre  [🔊 SOUND]"),
            _                                  => (PortType.SystemSlot,   "← Genre  [＋ System]"),
        };
        GenreIn    = AddInput(inLabel,         inType);
        FeatureOut = AddOutput("Feature →",    PortType.FeatureSlot);
        if (feature.canExpand)
            ExpandOut = AddOutput("Expand →",  PortType.ExpandSlot);
    }

    public override string DisplayName => FeatureData.featureName;
    public override FeatureSO.FeatureCategory Pillar => FeatureData.category;
    public override float DevWeeks => Mathf.Max(1f, FeatureData.cpuUsage * 0.15f);
    public override StatBonus PrimaryStatBonus =>
        new StatBonus(FeatureData.category, Mathf.CeilToInt(FeatureData.cpuUsage * 0.10f));
    public override FeatureSO GetFeatureData() => FeatureData;
}

// ============================================================
//  GameFeatureNode  —  Refines a system
// ============================================================
public class GameFeatureNode : GameNode
{
    public FeatureSO FeatureData { get; }
    public string    RequiredEngineNodeId;

    public NodePort SystemIn  { get; private set; }
    public NodePort ChainOut  { get; private set; }
    public NodePort ExpandOut { get; private set; }

    public GameFeatureNode(FeatureSO feature) : base(NodeKind.Feature)
    {
        FeatureData = feature;
        SystemIn  = AddInput("← System / ← Chain", PortType.FeatureSlot);
        ChainOut  = AddOutput("Chain →",            PortType.FeatureSlot);
        if (feature.canExpand)
            ExpandOut = AddOutput("Expand →",       PortType.ExpandSlot);
    }

    public override string DisplayName => FeatureData.featureName;
    public override FeatureSO.FeatureCategory Pillar => FeatureData.category;
    public override float DevWeeks => Mathf.Max(0.5f, FeatureData.cpuUsage * 0.10f);
    public override StatBonus PrimaryStatBonus =>
        new StatBonus(FeatureData.category, Mathf.CeilToInt(FeatureData.cpuUsage * 0.07f));
    public override FeatureSO GetFeatureData() => FeatureData;
}

// ============================================================
//  EngineNode  —  Deployed engine component
// ============================================================
public class EngineNode : GameNode
{
    public string    ComponentName;
    public string    ComponentType;
    public float     CpuCost;
    public string    Description;
    public List<string> UnlocksFeatureIds = new List<string>();

    private readonly FeatureSO.FeatureCategory _category;

    public NodePort EngineIn  { get; private set; }
    public NodePort EngineOut { get; private set; }
    public NodePort TechOut   { get; private set; }

    public EngineNode(string name, string type, float cpuCost,
                      FeatureSO.FeatureCategory category = FeatureSO.FeatureCategory.Tech)
        : base(NodeKind.Engine)
    {
        ComponentName = name;
        ComponentType = type;
        CpuCost       = cpuCost;
        _category     = category;

        PortType inSlot = category switch
        {
            FeatureSO.FeatureCategory.Graphic => PortType.GraphicSlot,
            FeatureSO.FeatureCategory.Sound   => PortType.SoundSlot,
            _                                 => PortType.EngineSlot,
        };
        string inLabel = category switch
        {
            FeatureSO.FeatureCategory.Graphic => "← Genre  [🎨 GRAFIK]",
            FeatureSO.FeatureCategory.Sound   => "← Genre  [🔊 SOUND]",
            _                                 => "← Engine Chain",
        };

        EngineIn  = AddInput(inLabel,            inSlot);
        EngineOut = AddOutput("Engine Chain →",  PortType.EngineSlot);
        TechOut   = AddOutput("Tech →",          PortType.TechSlot);
    }

    public override string DisplayName => ComponentName;
    public override FeatureSO.FeatureCategory Pillar => _category;
    public override float DevWeeks => Mathf.Max(0.5f, CpuCost * 0.05f);
}

// ============================================================
//  SupportNode  —  Global system (Narrative, UX, global tech)
//
//  FIX: BindOut removed. SupportNodes are leaf nodes — they
//  receive from GenreNode.SupportOut and provide no downstream
//  port. The old BindOut(SupportSlot) could never connect to
//  anything because SupportSlot is an INPUT-only target in the
//  compatibility rules. If chaining Support nodes is needed in
//  the future, add PortType.SupportChainSlot with explicit rules.
// ============================================================
public class SupportNode : GameNode
{
    public FeatureSO FeatureData { get; }
    public NodePort  GenreIn     { get; private set; }
    // BindOut intentionally removed — see class comment above.

    public SupportNode(FeatureSO feature) : base(NodeKind.Support)
    {
        FeatureData = feature;
        GenreIn = AddInput("← Genre  [＋ Support]", PortType.SupportSlot);
    }

    public override string DisplayName => FeatureData.featureName;
    public override FeatureSO.FeatureCategory Pillar => FeatureData.category;
    public override float DevWeeks => Mathf.Max(0.5f, FeatureData.cpuUsage * 0.08f);
    public override StatBonus PrimaryStatBonus =>
        new StatBonus(FeatureData.category, Mathf.CeilToInt(FeatureData.cpuUsage * 0.06f));
    public override FeatureSO GetFeatureData() => FeatureData;
}

// ============================================================
//  OptimizeNode  —  CPU reducer
// ============================================================
public class OptimizeNode : GameNode
{
    public float CpuReductionPercent = 15f;
    public float QualityBonus        = 0.08f;
    public float DevTimeCost         = 2f;
    private readonly FeatureSO.FeatureCategory _pillar;

    public NodePort ExpandIn  { get; private set; }
    public NodePort ExpandOut { get; private set; }

    public OptimizeNode(FeatureSO.FeatureCategory pillar) : base(NodeKind.Optimize)
    {
        _pillar   = pillar;
        ExpandIn  = AddInput("← Expand",  PortType.OptimizerSlot);
        ExpandOut = AddOutput("Expand →", PortType.ExpandSlot);
    }

    public override string DisplayName => "OPTIMIZER";
    public override FeatureSO.FeatureCategory Pillar => _pillar;
    public override float DevWeeks => DevTimeCost;
}

// ============================================================
//  Legacy aliases
// ============================================================
public class CoreNode        : GenreNode       { public CoreNode(string n, string p, float c) : base(n, p, c) {} }
public class AnchorNode      : SystemNode      { public NodePort CoreIn => GenreIn; public NodePort UpgradeOut => FeatureOut; public AnchorNode(FeatureSO f) : base(f) {} }
public class UpgradeNode     : GameFeatureNode { public NodePort AnchorIn => SystemIn; public UpgradeNode(FeatureSO f) : base(f) {} }
public class OptimizerNode   : OptimizeNode    { public OptimizerNode(FeatureSO.FeatureCategory p) : base(p) {} }
public class PillarStartNode : GenreNode       { public PillarStartNode(FeatureSO.FeatureCategory p) : base(p.ToString(), "Legacy", 100f) {} }
public class FeatureGameNode : SystemNode      { public NodePort CoreOut => FeatureOut; public FeatureGameNode(FeatureSO f) : base(f) {} }