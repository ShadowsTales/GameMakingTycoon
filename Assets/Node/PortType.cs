// ============================================================
//  PortType.cs
//  Defines what kind of data flows through a port.
//  Only ports of compatible types can be wired together.
// ============================================================
using System.Collections.Generic;

public enum PortType
{
    // ── Core flow ──────────────────────────────────────────
    PillarRoot,     // Output of a Pillar Start Node (connects to any Feature of that pillar)
    FeatureCore,    // Standard feature-to-feature chain output/input

    // ── Enhancement slots ──────────────────────────────────
    Expandable,     // This feature CAN accept expansion modules (canExpand = true output)
    Optimizer,      // Accepts an Optimizer node (quality boost, CPU reduction)
    Prerequisite,   // This output MUST be satisfied before the child can be placed

    // ── Cross-pillar signals ───────────────────────────────
    AudioTrigger,   // Sound feature output → Gameplay input
    RenderPass,     // Graphics output → Tech input (e.g. post-processing pipeline)
    DataFeed,       // Tech output → any pillar (analytics, save state injection)
}

/// <summary>
/// Static compatibility table.  A wire is valid only if
/// source.OutputType is listed as compatible with target.InputType.
/// </summary>
public static class PortCompatibility
{
    // Key   = OutputType coming OUT of the source node port
    // Value = set of InputTypes that can RECEIVE that output
    private static readonly Dictionary<PortType, HashSet<PortType>> _table =
        new Dictionary<PortType, HashSet<PortType>>
    {
        { PortType.PillarRoot,    new HashSet<PortType>{ PortType.FeatureCore } },
        { PortType.FeatureCore,   new HashSet<PortType>{ PortType.FeatureCore, PortType.Prerequisite } },
        { PortType.Expandable,    new HashSet<PortType>{ PortType.Optimizer } },
        { PortType.Optimizer,     new HashSet<PortType>{ PortType.Expandable } },      // bidirectional handled by caller
        { PortType.Prerequisite,  new HashSet<PortType>{ PortType.FeatureCore } },
        { PortType.AudioTrigger,  new HashSet<PortType>{ PortType.FeatureCore } },
        { PortType.RenderPass,    new HashSet<PortType>{ PortType.FeatureCore } },
        { PortType.DataFeed,      new HashSet<PortType>{ PortType.FeatureCore } },
    };

    public static bool IsCompatible(PortType output, PortType input)
    {
        return _table.TryGetValue(output, out var allowed) && allowed.Contains(input);
    }

    public static string GetIncompatibilityReason(PortType output, PortType input)
    {
        return $"Cannot connect [{output}] → [{input}]. " +
               $"'{input}' does not accept signals of type '{output}'.";
    }
}
