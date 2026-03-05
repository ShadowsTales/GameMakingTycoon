using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Feature Database")]
public class FeatureDatabase : ScriptableObject
{
    public List<FeatureSO> allFeatures = new List<FeatureSO>();

    // Hilfsfunktion für den GameCreatorController
    public List<FeatureSO> GetFeaturesForYear(int year)
    {
        return allFeatures.FindAll(f => f.yearAvailable <= year);
    }
}