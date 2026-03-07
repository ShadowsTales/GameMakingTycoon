// ============================================================
//  EngineSO.cs  —  NODE-TYCOON
//
//  Represents a game engine that the player has developed.
//  Created via the Engine Lab tab.
//  Used in the Game Creator to populate the Engine dropdown.
//
//  Each engine exposes a list of EngineComponentData which
//  become deployable EngineNodes in the Game Creator canvas.
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEngine", menuName = "Node-Tycoon/Engine")]
public class EngineSO : ScriptableObject
{
    public enum EngineStatus { InDevelopment, Finished, ForSale }

    [Header("Identity")]
    public string engineName  = "Unnamed Engine";
    public string version     = "1.0";
    [TextArea(2, 4)]
    public string description = "";

    [Header("Stats")]
    public float cpuBudget          = 100f;  // budget the engine gives game projects
    public float ramBudget          = 64f;
    public int   graphicBonus       = 0;     // flat bonus to graphic score
    public int   audioBonus         = 0;
    public int   licenseCostPerWeek = 0;     // 0 = owned outright

    [Header("Components")]
    // Each component becomes an available EngineNode in Game Creator
    public List<EngineComponentData> components = new List<EngineComponentData>();

    [Header("Status")]
    public EngineStatus status = EngineStatus.InDevelopment;

    // ── Helpers ───────────────────────────────────────────────────
    /// <summary>Returns a fresh EngineNode for the given component name.</summary>
    public EngineNode CreateEngineNode(string componentName)
    {
        var comp = components.Find(c => c.componentName == componentName);
        if (comp == null) return null;

        var category = comp.componentType.ToLower() switch
        {
            "renderer" => FeatureSO.FeatureCategory.Graphic,
            "audio"    => FeatureSO.FeatureCategory.Sound,
            "physics"  => FeatureSO.FeatureCategory.Tech,
            _          => FeatureSO.FeatureCategory.Tech,
        };
        return new EngineNode(comp.componentName, comp.componentType, comp.cpuCost, category);
    }
}

[Serializable]
public class EngineComponentData
{
    public string componentName = "Renderer2D";
    // Type string maps to CSS class suffix and EngineNode category
    // Valid values: "Renderer", "Audio", "Physics", "Streaming", "Network"
    public string componentType = "Renderer";
    public float  cpuCost       = 10f;
    [TextArea(1, 2)]
    public string description   = "";
    // Feature IDs this component unlocks in the Game Creator
    public List<string> unlocksFeatureIds = new List<string>();
}
