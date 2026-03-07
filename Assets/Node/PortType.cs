// ============================================================
//  PortType.cs  —  NODE-TYCOON  (Loop Architecture)
//
//  CONNECTION MODEL:
//
//  GenreNode (the game loop):
//    [REQ] GameplayOut → SystemNode (Gameplay category)
//    [REQ] GraphicOut  → EngineNode (Graphic/renderer)
//    [REQ] SoundOut    → EngineNode or SystemNode (Sound)
//    [OPT] SystemOut   → any extra SystemNode
//    [OPT] SupportOut  → any SupportNode (Narrative/UX)
//
//  SystemNode (a major mechanic):
//    IN:  GameplaySlot | SystemSlot  (from Genre)
//    OUT: FeatureSlot  → FeatureNode (refines this system)
//    OUT: ExpandSlot   → OptimizeNode (if canExpand)
//
//  FeatureNode (refines a system):
//    IN:  FeatureSlot  (from System or chained Feature)
//    OUT: FeatureSlot  (chain further features)
//    OUT: ExpandSlot   (optional optimizer)
//
//  EngineNode (deployed tech — graphics, audio, physics):
//    IN:  EngineSlot   (from Genre required outputs)
//    OUT: EngineSlot   (features can require this)
//    OUT: TechSlot     (tech features can wire into this)
//
//  OptimizeNode:
//    IN:  ExpandSlot   → from any ExpandSlot output
//    OUT: ExpandSlot   → chain more optimizers
// ============================================================
using System.Collections.Generic;

public enum PortType
{
    // ── Genre → first-level nodes ─────────────────────────────
    GameplaySlot,   // Genre⊕ → Gameplay SystemNode  [REQUIRED]
    GraphicSlot,    // Genre⊕ → Graphic  EngineNode  [REQUIRED]
    SoundSlot,      // Genre⊕ → Sound    EngineNode/SystemNode [REQUIRED]
    SystemSlot,     // Genre⊕ → any extra SystemNode [optional]
    SupportSlot,    // Genre⊕ → any SupportNode      [optional]

    // ── System → Feature chain ────────────────────────────────
    FeatureSlot,    // System⊕ or Feature⊕ → FeatureNode

    // ── Engine tech ───────────────────────────────────────────
    EngineSlot,     // Genre⊕ → EngineNode  /  EngineNode⊕ → requires
    TechSlot,       // EngineNode⊕ → features that need specific engine tech

    // ── Optimizer ─────────────────────────────────────────────
    ExpandSlot,     // any canExpand⊕ → OptimizerNode
    OptimizerSlot,  // OptimizerNode IN

    // ── Legacy (serialized data backward compat) ──────────────
    PillarRoot, FeatureCore, Prerequisite,
    Expandable, Optimizer,
    AudioTrigger, RenderPass, DataFeed,
    AnchorSlot, UpgradeSlot,
    CoreToGameplay, CoreToGraphic, CoreToSound,
    CoreToAnchor, CoreToSupport,
    AnchorToUpgrade, UpgradeChain,
}

public static class PortCompatibility
{
    private static readonly Dictionary<PortType, HashSet<PortType>> _rules =
        new Dictionary<PortType, HashSet<PortType>>
    {
        // Required Genre outputs → exact matching input
        { PortType.GameplaySlot,  new HashSet<PortType>{ PortType.GameplaySlot } },
        { PortType.GraphicSlot,   new HashSet<PortType>{ PortType.GraphicSlot  } },
        { PortType.SoundSlot,     new HashSet<PortType>{ PortType.SoundSlot    } },

        // Optional Genre outputs
        { PortType.SystemSlot,    new HashSet<PortType>{ PortType.SystemSlot   } },
        { PortType.SupportSlot,   new HashSet<PortType>{ PortType.SupportSlot  } },

        // Feature chain
        { PortType.FeatureSlot,   new HashSet<PortType>{ PortType.FeatureSlot  } },

        // Engine
        { PortType.EngineSlot,    new HashSet<PortType>{ PortType.EngineSlot   } },
        { PortType.TechSlot,      new HashSet<PortType>{ PortType.TechSlot     } },

        // Optimizer
        { PortType.ExpandSlot,    new HashSet<PortType>{ PortType.OptimizerSlot } },
        { PortType.OptimizerSlot, new HashSet<PortType>{ PortType.ExpandSlot    } },

        // Legacy — permissive so old graphs keep working
        { PortType.PillarRoot,    new HashSet<PortType>{ PortType.GameplaySlot, PortType.SystemSlot, PortType.FeatureSlot, PortType.PillarRoot, PortType.FeatureCore, PortType.AnchorSlot } },
        { PortType.FeatureCore,   new HashSet<PortType>{ PortType.FeatureSlot, PortType.FeatureCore, PortType.Prerequisite, PortType.SystemSlot, PortType.AnchorSlot, PortType.UpgradeSlot } },
        { PortType.AnchorSlot,    new HashSet<PortType>{ PortType.SystemSlot, PortType.AnchorSlot, PortType.GameplaySlot } },
        { PortType.UpgradeSlot,   new HashSet<PortType>{ PortType.FeatureSlot, PortType.UpgradeSlot } },
        { PortType.Expandable,    new HashSet<PortType>{ PortType.OptimizerSlot, PortType.Optimizer, PortType.ExpandSlot } },
        { PortType.Optimizer,     new HashSet<PortType>{ PortType.ExpandSlot, PortType.Expandable  } },
        { PortType.DataFeed,      new HashSet<PortType>{ PortType.SupportSlot, PortType.DataFeed   } },
        { PortType.AudioTrigger,  new HashSet<PortType>{ PortType.SoundSlot   } },
        { PortType.RenderPass,    new HashSet<PortType>{ PortType.GraphicSlot, PortType.EngineSlot } },
        { PortType.CoreToGameplay,new HashSet<PortType>{ PortType.GameplaySlot, PortType.CoreToGameplay } },
        { PortType.CoreToGraphic, new HashSet<PortType>{ PortType.GraphicSlot,  PortType.CoreToGraphic  } },
        { PortType.CoreToSound,   new HashSet<PortType>{ PortType.SoundSlot,    PortType.CoreToSound    } },
        { PortType.CoreToAnchor,  new HashSet<PortType>{ PortType.SystemSlot,   PortType.AnchorSlot     } },
        { PortType.CoreToSupport, new HashSet<PortType>{ PortType.SupportSlot,  PortType.DataFeed       } },
        { PortType.AnchorToUpgrade,new HashSet<PortType>{ PortType.FeatureSlot, PortType.UpgradeSlot   } },
        { PortType.UpgradeChain,  new HashSet<PortType>{ PortType.FeatureSlot, PortType.UpgradeSlot    } },
        { PortType.Prerequisite,  new HashSet<PortType>{ PortType.FeatureSlot, PortType.FeatureCore    } },
    };

    public static bool IsCompatible(PortType output, PortType input)
        => _rules.TryGetValue(output, out var set) && set.Contains(input);

    public static string GetIncompatibilityReason(PortType output, PortType input)
        => (output) switch
        {
            PortType.GameplaySlot => "⚡ Verbinde mit einem GAMEPLAY-System (Steuerung, Kampf…).",
            PortType.GraphicSlot  => "🎨 Verbinde mit einem GRAFIK-Engine-Node (Renderer).",
            PortType.SoundSlot    => "🔊 Verbinde mit einem SOUND-Node (Audio-Engine oder System).",
            PortType.SystemSlot   => "Dieser Slot nimmt nur System-Nodes auf.",
            PortType.SupportSlot  => "Dieser Slot nimmt nur Support-Nodes auf (Narrative/UX).",
            PortType.FeatureSlot  => "Feature-Slot: verbinde ein Feature-Node.",
            PortType.EngineSlot   => "Engine-Slot: verbinde einen Engine-Node.",
            PortType.TechSlot     => "Tech-Slot: nur Features mit Engine-Anforderung.",
            PortType.ExpandSlot   => "Nur Optimizer-Nodes können sich hier ankoppeln.",
            _                     => $"Verbindung [{output}] → [{input}] ist nicht erlaubt."
        };

    public static bool IsRequiredGenreOutput(PortType t)
        => t == PortType.GameplaySlot || t == PortType.GraphicSlot || t == PortType.SoundSlot;

    /// <summary>Alias for IsRequiredGenreOutput — used by older controller code.</summary>
    public static bool IsRequiredCoreOutput(PortType t) => IsRequiredGenreOutput(t);
}