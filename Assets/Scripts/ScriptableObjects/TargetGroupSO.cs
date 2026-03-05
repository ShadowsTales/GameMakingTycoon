using UnityEngine;

/// <summary>
/// Enum for target audience categories.
/// </summary>
public enum TargetGroupType
{
    Kids,
    Teens,
    Adults
}

/// <summary>
/// Static data for a target group (e.g. "Kids", "Teens", "Adults").
/// Used to calculate how well a genre fits a target group.
/// </summary>
[CreateAssetMenu(menuName = "Game/Target Group")]
public class TargetGroupSO : ScriptableObject
{
    [Tooltip("Name shown in UI, e.g. 'Kids', 'Teens', 'Adults'.")]
    public string displayName;

    [Tooltip("Logical type used in calculations.")]
    public TargetGroupType group;
}
