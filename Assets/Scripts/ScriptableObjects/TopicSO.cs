using UnityEngine;

[CreateAssetMenu(menuName = "Game/Topic")]
public class TopicSO : ScriptableObject
{
    public string topicName;

    [Header("Genre Synergy (0–1)")]
    public float actionSynergy = 0.5f;
    public float rpgSynergy = 0.5f;
    public float strategySynergy = 0.5f;
    public float simulationSynergy = 0.5f;
    public float visualNovelSynergy = 0.5f;

    public float GetSynergy(GenreSO genre)
    {
        return genre.genreName switch
        {
            "Action" => actionSynergy,
            "RPG" => rpgSynergy,
            "Strategy" => strategySynergy,
            "Simulation" => simulationSynergy,
            "Visual Novel" => visualNovelSynergy,
            _ => 0.5f
        };
    }
}
