using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "topicDatabase", menuName = "Scriptable Objects/topicDatabase")]
public class topicDatabase : ScriptableObject
{
    public List<TopicSO> topics;  
}
