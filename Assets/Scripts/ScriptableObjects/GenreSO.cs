using UnityEngine;

[CreateAssetMenu(menuName = "Game/Genre")]
public class GenreSO : ScriptableObject
{
    [Header("Basic Info")]
    public string genreName;
    [Range(0, 100)] public float popularity = 50f;

    [Header("Target Group Appeal (0–1)")]
    public float kidsAppeal = 0.5f;
    public float teensAppeal = 0.5f;
    public float adultsAppeal = 0.5f;

    [Header("Focus Weights (0–1)")]
    public float gameplayWeight = 0.33f;
    public float graphicsWeight = 0.33f;
    public float storyWeight = 0.33f;

    public float GetAppealFor(TargetGroupSO target)
    {
        return target.group switch
        {
            TargetGroupType.Kids => kidsAppeal,
            TargetGroupType.Teens => teensAppeal,
            TargetGroupType.Adults => adultsAppeal,
            _ => 0.5f
        };
    }

    public float CalculateQuality(GameData game)
    {
        return
            game.gameplayFocus * gameplayWeight +
            game.graphicsFocus * graphicsWeight +
            game.storyFocus * storyWeight;
    }
}
