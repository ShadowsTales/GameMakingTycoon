using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "targetGroupDatabase", menuName = "Scriptable Objects/targetGroupDatabase")]
public class targetGroupDatabase : ScriptableObject
{
    public List<TargetGroupSO> targetGroups;
}
