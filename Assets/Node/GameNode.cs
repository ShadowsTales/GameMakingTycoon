// ============================================================
//  GameNode.cs  —  NODE-TYCOON  (Loop Architecture)
//
//  NODE HIERARCHY:
//    GenreNode   — the game loop root (was: CoreNode)
//    SystemNode  — a major mechanic   (was: AnchorNode)
//    FeatureNode — refines a system   (was: UpgradeNode)
//    EngineNode  — deployed tech
//    SupportNode — global leaf (Audio, Save, Shader…)
//    OptimizeNode— CPU reducer
//
//  CHANGES FROM LEGACY:
//    • All legacy alias classes removed (CoreNode, AnchorNode,
//      UpgradeNode, OptimizerNode, PillarStartNode, FeatureGameNode)
//    • NodeKind cleaned — legacy values removed from enum
//    • SupportNode.BindOut intentionally absent; Support nodes
//      are leaves. Add PortType.SupportChainSlot if chaining
//      is ever needed.
//    • GetFeatureData() on base class eliminates type-switch
//      boilerplate everywhere else in the codebase.
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

// ── Node kind ────────────────────────────────────────────────
public enum NodeKind
{
    Genre,    // the game loop root
    System,   // major mechanic    (was Anchor)
    Feature,  // refines a system  (was Upgrade)
    Engine,   // deployed tech component
    Support,  // global leaf       (Audio / Narrative / UX)
    Optimize, // CPU reducer
}

// ── Stat bonus ────────────────────────────────────────────────
[Serializable]
public struct StatBonus
{
    public FeatureSO.FeatureCategory Category;
    public int Value;

    public StatBonus(FeatureSO.FeatureCategory cat, int val)
    {
        Category = cat;
        Value    = val;
    }

    public string Label => Value > 0 ? $"+{Value} {Category}" : "";
}

// ============================================================
//  GameNode  —  abstract base
// ============================================================
[Serializable]
public abstract class GameNode
{
    public string   NodeId;
    public NodeKind Kind;
    public Vector2  CanvasPosition;

    public List<NodePort> InputPorts  = new List<NodePort>();
    public List<NodePort> OutputPorts = new List<NodePort>();

    protected GameNode(NodeKind kind)
    {
        NodeId = Guid.NewGuid().ToString();
        Kind   = kind;
    }

    public abstract string                       DisplayName      { get; }
    public abstract FeatureSO.FeatureCategory    Pillar           { get; }
    public virtual  float                        DevWeeks         => 0f;
    public virtual  StatBonus                    PrimaryStatBonus => default;

    /// <summary>
    /// Returns the FeatureSO bound to this node, or null for
    /// GenreNode / EngineNode / OptimizeNode.
    /// Eliminates type-switch boilerplate at call sites.
    /// </summary>
    public virtual FeatureSO GetFeatureData() => null;

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

    public NodePort GetPort(string id)
    {
        foreach (var p in InputPorts)  if (p.PortId == id) return p;
        foreach (var p in OutputPorts) if (p.PortId == id) return p;
        return null;
    }
}

// ============================================================
//  GenreNode  —  THE GAME LOOP  (was CoreNode)
// ============================================================
public class GenreNode : GameNode
{
    public string  ProjectName;
    public string  Platform;
    public float   CpuBudget;
    public GenreSO Genre;

    public string TriggerLabel    = "Trigger";
    public string ResolutionLabel = "Resolution";

    public NodePort GameplayOut { get; private set; }
    public NodePort GraphicOut  { get; private set; }
    public NodePort SoundOut    { get; private set; }
    public NodePort SystemOut   { get; private set; }
    public NodePort SupportOut  { get; private set; }

    public bool AllRequiredConnected =>
        GameplayOut.IsConnected && GraphicOut.IsConnected && SoundOut.IsConnected;

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
                var g when g.Contains("rpg")        => ("Quest erhalten",         "Quest abgeschlossen"),
                var g when g.Contains("action")     => ("Bedrohung",              "Sieg / Überleben"),
                var g when g.Contains("puzzle")     => ("Problem-Zustand",        "Lösung gefunden"),
                var g when g.Contains("strategy")   => ("Ressourcen-Druck",       "Entscheidung + Konsequenz"),
                var g when g.Contains("simulation") => ("System-Ungleichgewicht", "Gleichgewicht"),
                var g when g.Contains("adventure")  => ("Entdeckung",             "Fortschritt"),
                _                                   => ("Spieler-Aktion",         "Spieler-Belohnung"),
            };
        }

        GameplayOut = AddOutput("⚡ GAMEPLAY  [Pflicht]",  PortType.GameplaySlot);
        GraphicOut  = AddOutput("🎨 GRAFIK    [Pflicht]",  PortType.GraphicSlot);
        SoundOut    = AddOutput("🔊 SOUND     [Pflicht]",  PortType.SoundSlot);
        SystemOut   = AddOutput("＋ System    [Optional]", PortType.SystemSlot);
        SupportOut  = AddOutput("＋ Support   [Optional]", PortType.SupportSlot);
    }

    public override string                    DisplayName => ProjectName;
    public override FeatureSO.FeatureCategory Pillar      => FeatureSO.FeatureCategory.Gameplay;
}

// ============================================================
//  SystemNode  —  A MAJOR MECHANIC  (was AnchorNode)
// ============================================================
public class SystemNode : GameNode
{
    public FeatureSO FeatureData { get; }
    public NodePort  GenreIn     { get; private set; }
    public NodePort  FeatureOut  { get; private set; }
    public NodePort  ExpandOut   { get; private set; }

    public SystemNode(FeatureSO feature) : base(NodeKind.System)
    {
        FeatureData = feature;

        (PortType inType, string inLabel) = feature.category switch
        {
            FeatureSO.FeatureCategory.Gameplay => (PortType.GameplaySlot, "← Genre  [⚡ GAMEPLAY]"),
            FeatureSO.FeatureCategory.Sound    => (PortType.SoundSlot,    "← Genre  [🔊 SOUND]"),
            _                                  => (PortType.SystemSlot,   "← Genre  [＋ System]"),
        };

        GenreIn    = AddInput (inLabel,        inType);
        FeatureOut = AddOutput("Feature →",    PortType.FeatureSlot);

        if (feature.canExpand)
            ExpandOut = AddOutput("Expand →", PortType.ExpandSlot);
    }

    public override string                    DisplayName      => FeatureData.featureName;
    public override FeatureSO.FeatureCategory Pillar           => FeatureData.category;
    public override float                     DevWeeks         => Mathf.Max(1f, FeatureData.cpuUsage * 0.15f);
    public override StatBonus                 PrimaryStatBonus =>
        new StatBonus(FeatureData.category, Mathf.CeilToInt(FeatureData.cpuUsage * 0.10f));
    public override FeatureSO GetFeatureData() => FeatureData;
}

// ============================================================
//  FeatureNode  —  REFINES A SYSTEM  (was UpgradeNode / GameFeatureNode)
// ============================================================
public class FeatureNode : GameNode
{
    public FeatureSO FeatureData           { get; }
    public string    RequiredEngineNodeId;

    public NodePort SystemIn  { get; private set; }
    public NodePort ChainOut  { get; private set; }
    public NodePort ExpandOut { get; private set; }

    public FeatureNode(FeatureSO feature) : base(NodeKind.Feature)
    {
        FeatureData = feature;
        SystemIn  = AddInput ("← System / ← Chain", PortType.FeatureSlot);
        ChainOut  = AddOutput("Chain →",             PortType.FeatureSlot);

        if (feature.canExpand)
            ExpandOut = AddOutput("Expand →", PortType.ExpandSlot);
    }

    public override string                    DisplayName      => FeatureData.featureName;
    public override FeatureSO.FeatureCategory Pillar           => FeatureData.category;
    public override float                     DevWeeks         => Mathf.Max(0.5f, FeatureData.cpuUsage * 0.10f);
    public override StatBonus                 PrimaryStatBonus =>
        new StatBonus(FeatureData.category, Mathf.CeilToInt(FeatureData.cpuUsage * 0.07f));
    public override FeatureSO GetFeatureData() => FeatureData;
}

// ============================================================
//  EngineNode  —  DEPLOYED TECH  (Graphics / Audio / Physics)
// ============================================================
public class EngineNode : GameNode
{
    public float   DevTimeCost = 3f;
    private readonly FeatureSO.FeatureCategory _pillar;

    public NodePort EngineIn  { get; private set; }
    public NodePort EngineOut { get; private set; }
    public NodePort TechOut   { get; private set; }

    public EngineNode(FeatureSO.FeatureCategory pillar) : base(NodeKind.Engine)
    {
        _pillar = pillar;

        PortType inSlot = pillar switch
        {
            FeatureSO.FeatureCategory.Graphic => PortType.GraphicSlot,
            FeatureSO.FeatureCategory.Sound   => PortType.SoundSlot,
            _                                 => PortType.EngineSlot,
        };

        EngineIn  = AddInput ("← Genre / ← Engine", inSlot);
        EngineOut = AddOutput("Engine →",            PortType.EngineSlot);
        TechOut   = AddOutput("Tech →",              PortType.TechSlot);
    }

    public override string                    DisplayName => $"ENGINE [{_pillar}]";
    public override FeatureSO.FeatureCategory Pillar      => _pillar;
    public override float                     DevWeeks    => DevTimeCost;
}

// ============================================================
//  SupportNode  —  GLOBAL LEAF  (Audio, Save, Shader…)
//
//  Support nodes are leaves — they receive from Genre and have
//  no output. If chaining is ever needed, add
//  PortType.SupportChainSlot with explicit compatibility rules.
// ============================================================
public class SupportNode : GameNode
{
    public FeatureSO FeatureData { get; }
    public NodePort  GenreIn     { get; private set; }

    public SupportNode(FeatureSO feature) : base(NodeKind.Support)
    {
        FeatureData = feature;
        GenreIn = AddInput("← Genre  [＋ Support]", PortType.SupportSlot);
    }

    public override string                    DisplayName      => FeatureData.featureName;
    public override FeatureSO.FeatureCategory Pillar           => FeatureData.category;
    public override float                     DevWeeks         => Mathf.Max(0.5f, FeatureData.cpuUsage * 0.08f);
    public override StatBonus                 PrimaryStatBonus =>
        new StatBonus(FeatureData.category, Mathf.CeilToInt(FeatureData.cpuUsage * 0.06f));
    public override FeatureSO GetFeatureData() => FeatureData;
}

// ============================================================
//  OptimizeNode  —  CPU REDUCER
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
        ExpandIn  = AddInput ("← Expand",  PortType.OptimizerSlot);
        ExpandOut = AddOutput("Expand →",  PortType.ExpandSlot);
    }

    public override string                    DisplayName => "OPTIMIZER";
    public override FeatureSO.FeatureCategory Pillar      => _pillar;
    public override float                     DevWeeks    => DevTimeCost;
}