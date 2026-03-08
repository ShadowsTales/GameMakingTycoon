// ============================================================
//  BlueprintSO.cs  —  NODE-TYCOON  (Redesign v2)
//
//  A Blueprint is a saved group ("Stack") of nodes.
//  When placed as a NestedNode it:
//    • Occupies one card on the canvas instead of many
//    • Carries over TechDebt and BugRisk from the original
//    • Reduces dev time vs. building from scratch
//    • Shows a warning badge if debt/risk is high
// ============================================================
using UnityEngine;

[CreateAssetMenu(fileName = "NewBlueprint", menuName = "NodeTycoon/Blueprint")]
public class BlueprintSO : ScriptableObject
{
    [Header("── Identity ────────────────────────────────────────")]
    public string blueprintName;
    [TextArea(2, 3)] public string description;
    public Texture2D thumbnail;

    [Header("── Primary Domain ──────────────────────────────────")]
    [Tooltip("Which domain socket this blueprint primarily belongs to.")]
    public FeatureSO.FeatureCategory PrimaryPillar = FeatureSO.FeatureCategory.Gameplay;

    [Header("── Serialized Graph ─────────────────────────────────")]
    [Tooltip("JSON snapshot of the node group (set by NodeGraphController.SaveBlueprint).")]
    [TextArea(3, 6)]
    public string nodeSnapshot;

    [Header("── Carried-Over Metrics ──────────────────────────────")]
    [Range(0f, 1f)]
    [Tooltip("Technical debt from the original design (0=clean, 1=heavy debt). Degrades Stability.")]
    public float techDebt = 0f;

    [Range(0f, 1f)]
    [Tooltip("Bug risk from the original design (0=stable, 1=very buggy). Adds instability.")]
    public float bugRisk = 0f;

    [Tooltip("Total CPU usage of all nodes in this blueprint.")]
    public float totalCpu = 0f;

    [Tooltip("Total dev weeks the original group required.")]
    public float totalDevWeeks = 0f;

    [Tooltip("Number of nodes this blueprint contains.")]
    public int nodeCount = 0;

    [Tooltip("Number of synergy pairs active in the original design.")]
    public int synergyCount = 0;

    [Tooltip("Were there conflict pairs in the original? (carries over as risk)")]
    public bool hadConflicts = false;

    // ── Computed properties ─────────────────────────────────────────

    /// <summary>Traffic-light colour for the debt/risk level.</summary>
    public string DebtBadge => (techDebt + bugRisk) switch
    {
        var v when v < 0.3f => "✓ Clean",
        var v when v < 0.7f => "⚠ Some Debt",
        _                   => "🔴 Heavy Debt",
    };

    public bool IsClean => techDebt < 0.2f && bugRisk < 0.2f && !hadConflicts;
}