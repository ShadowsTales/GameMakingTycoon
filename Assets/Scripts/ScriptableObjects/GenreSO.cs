// ============================================================
//  GenreSO.cs
//
//  CHANGES vs previous version:
//    • narrativeWeight — how much story/dialogue features matter
//    • uxWeight        — how much UI/accessibility features matter
//    • CalculateQuality() now includes all 6 pillars
// ============================================================
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Genre")]
public class GenreSO : ScriptableObject
{
    [Header("Basic Info")]
    public string genreName;
    [Range(0, 100)] public float popularity = 50f;

    [Header("Target Group Appeal (0–1)")]
    public float kidsAppeal   = 0.5f;
    public float teensAppeal  = 0.5f;
    public float adultsAppeal = 0.5f;

    [Header("Focus Weights (0–1) — should sum to ~1.0")]
    [Tooltip("How much Gameplay features contribute to score")]
    public float gameplayWeight  = 0.25f;

    [Tooltip("How much Graphics features contribute to score")]
    public float graphicsWeight  = 0.20f;

    [Tooltip("How much Sound/Music features contribute to score")]
    public float soundWeight     = 0.15f;

    [Tooltip("How much Tech/Engine features contribute to score")]
    public float techWeight      = 0.15f;

    [Tooltip("How much Narrative/Story features contribute to score")]
    public float narrativeWeight = 0.15f;

    [Tooltip("How much UX/UI features contribute to score")]
    public float uxWeight        = 0.10f;

    // ── Legacy field — kept so old .asset files deserialise without error
    // storyWeight was previously used; now replaced by narrativeWeight + soundWeight
    [HideInInspector] public float storyWeight = 0f;

    public float GetAppealFor(TargetGroupSO target)
    {
        return target.group switch
        {
            TargetGroupType.Kids   => kidsAppeal,
            TargetGroupType.Teens  => teensAppeal,
            TargetGroupType.Adults => adultsAppeal,
            _                      => 0.5f
        };
    }

    /// <summary>
    /// Returns a 0–1 quality score based on GameData focus sliders
    /// weighted by this genre's pillar priorities.
    /// </summary>
    public float CalculateQuality(GameData game)
    {
        return
            game.gameplayFocus  * gameplayWeight  +
            game.graphicsFocus  * graphicsWeight  +
            game.storyFocus     * (soundWeight + narrativeWeight) +  // storyFocus covers both
            game.gameplayFocus  * uxWeight * 0.5f;                   // UX loosely follows gameplay
    }
}