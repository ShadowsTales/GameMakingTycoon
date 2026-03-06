// ============================================================
//  FeatureSO.cs
//
//  CHANGES vs previous version:
//    • FeatureCategory: added Narrative (4) and UX (5)
//      — appended at END so existing .asset files keep their
//        integer values (Gameplay=0…Tech=3 unchanged)
//    • conflictsWith: features that CANNOT coexist in the same graph
//    • synergyWith:   features that give a quality BONUS when both present
//    • NodeTierOverride: lets a designer force a tier (optional)
// ============================================================
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFeature", menuName = "GameData/Feature")]
public class FeatureSO : ScriptableObject
{
    // ── IMPORTANT: never reorder or insert — existing .asset files
    //   store the integer index.  New values go at the END only.
    public enum FeatureCategory
    {
        Gameplay  = 0,
        Graphic   = 1,
        Sound     = 2,
        Tech      = 3,
        Narrative = 4,   // NEW — dialogue, quests, story branching
        UX        = 5,   // NEW — UI framework, accessibility, tutorials
    }

    [Header("Basis Informationen")]
    public string featureName;
    public FeatureCategory category;
    [TextArea] public string description;

    [Header("Management & Kosten")]
    public float cpuUsage;
    public float ramUsage;
    public float researchCostPoints;

    [Header("Forschungs-Logik")]
    public bool isResearched;
    public int  releaseYear;
    public bool canExpand;

    [Header("Abhängigkeiten")]
    public List<FeatureSO> prerequisites = new List<FeatureSO>();

    [Header("Synergien & Konflikte  (Copilot-System)")]
    [Tooltip("Features that give a quality BONUS when both are in the same graph.")]
    public List<FeatureSO> synergyWith   = new List<FeatureSO>();

    [Tooltip("Features that CANNOT coexist — placing both triggers a validation error.")]
    public List<FeatureSO> conflictsWith = new List<FeatureSO>();

    [Header("Optionen")]
    [Tooltip("Leave as Default to let the scoring service infer the tier automatically.")]
    public NodeTierOverride tierOverride = NodeTierOverride.Default;

    // Legacy field — kept so old .asset files don't lose data
    [HideInInspector] public int yearAvailable;
}

// Separate enum so FeatureSO doesn't depend on NodeTier (which lives in NodeScoringService)
public enum NodeTierOverride { Default, Core, Feature, Enhancement }