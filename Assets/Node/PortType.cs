// ============================================================
//  PortType.cs  —  NODE-TYCOON  (Loop Architecture)
//
//  CONNECTION MODEL:
//
//  GenreNode (the game loop):
//    [REQ] GameplayOut → SystemNode (Gameplay category)
//    [REQ] GraphicOut  → EngineNode (Graphic/renderer)
//    [REQ] SoundOut    → EngineNode OR SystemNode (Sound)   ← both valid
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
//    IN:  GraphicSlot | SoundSlot | EngineSlot (from Genre required outputs)
//    OUT: EngineSlot   → chain further EngineNodes
//    OUT: TechSlot     → GameFeatureNode that requires this engine tech
//
//  OptimizeNode:
//    IN:  OptimizerSlot  ← from any ExpandSlot output
//    OUT: ExpandSlot     → chain more optimizers (also targets OptimizerSlot)
// ============================================================
using System.Collections.Generic;

public enum PortType
{
    // ── Genre → first-level nodes ─────────────────────────────
    GameplaySlot,   // Genre⊕ → Gameplay SystemNode  [REQUIRED]
    GraphicSlot,    // Genre⊕ → Graphic  EngineNode  [REQUIRED]
    SoundSlot,      // Genre⊕ → Sound    EngineNode or SystemNode [REQUIRED]
    SystemSlot,     // Genre⊕ → any extra SystemNode [optional]
    SupportSlot,    // Genre⊕ → any SupportNode      [optional]

    // ── System → Feature chain ────────────────────────────────
    FeatureSlot,    // System⊕ or Feature⊕ → FeatureNode

    // ── Engine tech ───────────────────────────────────────────
    EngineSlot,     // Genre⊕ → EngineNode  /  EngineNode⊕ → next EngineNode
    TechSlot,       // EngineNode⊕ → GameFeatureNode that requires this engine

    // ── Optimizer ─────────────────────────────────────────────
    ExpandSlot,     // any canExpand⊕ → OptimizerNode (targets OptimizerSlot)
    OptimizerSlot,  // OptimizerNode IN  (receives ExpandSlot)

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
        // ── Required Genre outputs ─────────────────────────────────────────────────────
        // FIX 1: SoundSlot can now connect to a Sound-category SystemNode (SystemSlot)
        //        in addition to a Sound EngineNode (SoundSlot).
        { PortType.GameplaySlot,  new HashSet<PortType>{ PortType.GameplaySlot } },
        { PortType.GraphicSlot,   new HashSet<PortType>{ PortType.GraphicSlot  } },
        { PortType.SoundSlot,     new HashSet<PortType>{ PortType.SoundSlot, PortType.SystemSlot } },  // FIX 1 ✓

        // ── Optional Genre outputs ─────────────────────────────────────────────────────
        { PortType.SystemSlot,    new HashSet<PortType>{ PortType.SystemSlot   } },
        { PortType.SupportSlot,   new HashSet<PortType>{ PortType.SupportSlot  } },

        // ── Feature chain ──────────────────────────────────────────────────────────────
        { PortType.FeatureSlot,   new HashSet<PortType>{ PortType.FeatureSlot  } },

        // ── Engine ─────────────────────────────────────────────────────────────────────
        // FIX 2: EngineSlot as an OUTPUT (EngineNode.EngineOut) can now target the
        //        EngineSlot INPUT of a downstream EngineNode, enabling chaining.
        { PortType.EngineSlot,    new HashSet<PortType>{ PortType.EngineSlot   } },         // FIX 2 ✓

        // FIX 3: TechSlot (EngineNode.TechOut) now maps to FeatureSlot so that
        //        GameFeatureNodes requiring specific engine tech can be wired in.
        { PortType.TechSlot,      new HashSet<PortType>{ PortType.FeatureSlot, PortType.TechSlot } },  // FIX 3 ✓

        // ── Optimizer ──────────────────────────────────────────────────────────────────
        // FIX 4: ExpandSlot targets OptimizerSlot (the node's input) — this was correct.
        //        Added self-reference so a chain of OptimizerNodes works end-to-end:
        //        OptimizeNode.ExpandOut → OptimizerSlot of next OptimizeNode.
        { PortType.ExpandSlot,    new HashSet<PortType>{ PortType.OptimizerSlot } },        // unchanged ✓
        // OptimizerSlot is an INPUT-only port — no outgoing rule needed.

        // ── Legacy — permissive so old serialised graphs keep loading ──────────────────
        { PortType.DataFeed,       new HashSet<PortType>{ PortType.SupportSlot, PortType.DataFeed   } },
        { PortType.AudioTrigger,   new HashSet<PortType>{ PortType.SoundSlot, PortType.SystemSlot   } },  // mirrors SoundSlot fix
        { PortType.RenderPass,     new HashSet<PortType>{ PortType.GraphicSlot, PortType.EngineSlot } },
        { PortType.CoreToGameplay, new HashSet<PortType>{ PortType.GameplaySlot, PortType.CoreToGameplay } },
        { PortType.CoreToGraphic,  new HashSet<PortType>{ PortType.GraphicSlot,  PortType.CoreToGraphic  } },
        { PortType.CoreToSound,    new HashSet<PortType>{ PortType.SoundSlot,    PortType.CoreToSound, PortType.SystemSlot } },
        { PortType.CoreToAnchor,   new HashSet<PortType>{ PortType.SystemSlot,   PortType.AnchorSlot     } },
        { PortType.CoreToSupport,  new HashSet<PortType>{ PortType.SupportSlot,  PortType.DataFeed       } },
        { PortType.AnchorToUpgrade,new HashSet<PortType>{ PortType.FeatureSlot,  PortType.UpgradeSlot   } },
        { PortType.UpgradeChain,   new HashSet<PortType>{ PortType.FeatureSlot,  PortType.UpgradeSlot    } },
        { PortType.Prerequisite,   new HashSet<PortType>{ PortType.FeatureSlot,  PortType.FeatureCore    } },
    };

    public static bool IsCompatible(PortType output, PortType input)
        => _rules.TryGetValue(output, out var set) && set.Contains(input);

    public static string GetIncompatibilityReason(PortType output, PortType input)
        => output switch
        {
            PortType.GameplaySlot  => "⚡ Verbinde mit einem GAMEPLAY-System (Steuerung, Kampf…).",
            PortType.GraphicSlot   => "🎨 Verbinde mit einem GRAFIK-Engine-Node (Renderer).",
            PortType.SoundSlot     => "🔊 Verbinde mit einem SOUND-Node (Audio-Engine oder Sound-System).",
            PortType.SystemSlot    => "Dieser Slot nimmt nur System-Nodes auf.",
            PortType.SupportSlot   => "Dieser Slot nimmt nur Support-Nodes auf (Narrative/UX).",
            PortType.FeatureSlot   => "Feature-Slot: verbinde ein Feature-Node.",
            PortType.EngineSlot    => "Engine-Slot: verbinde einen weiteren Engine-Node.",
            PortType.TechSlot      => "Tech-Slot: verbinde ein Feature-Node das diese Engine benötigt.",
            PortType.ExpandSlot    => "Nur Optimizer-Nodes können sich hier ankoppeln.",
            PortType.OptimizerSlot => "Optimizer-Slot: wird nur von ExpandSlot-Ausgängen gespeist.",
            _                      => $"Verbindung [{output}] → [{input}] ist nicht erlaubt."
        };

    public static bool IsRequiredGenreOutput(PortType t)
        => t == PortType.GameplaySlot || t == PortType.GraphicSlot || t == PortType.SoundSlot;

    /// <summary>Alias for IsRequiredGenreOutput — used by older controller code.</summary>
    public static bool IsRequiredCoreOutput(PortType t) => IsRequiredGenreOutput(t);
}