using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFeature", menuName = "GameData/Feature")]
public class FeatureSO : ScriptableObject
{
    public enum FeatureCategory { Gameplay, Graphic, Sound, Tech }

    [Header("Basis Informationen")]
    public string featureName;
    public FeatureCategory category;
    [TextArea] public string description;

    [Header("Management & Kosten")]
    public float cpuUsage;    // Last bei der Spieleentwicklung
    public float ramUsage;    // Last bei der Spieleentwicklung
    public float researchCostPoints; // Basis-Kosten für die Forschung

    [Header("Forschungs-Logik")]
    public bool isResearched;  // Status: Bereits freigeschaltet?
    public int releaseYear;    // Das Jahr, ab dem der Malus auf 0 sinkt
    public bool canExpand;     // Entscheidet, ob nachfolgende Slots erscheinen
    
    [Header("Abhängigkeiten")]
    // Die Voraussetzung, die erfüllt sein muss, um dieses Feature zu erforschen
    [SerializeReference]                // oder List<FeatureSO> wenn du Referenzen willst
    public List<FeatureSO> prerequisites = new List<FeatureSO>(); 

    // Hilfsvariable für die Anzeige im Baukasten (altes System)
    public int yearAvailable; // Nur für die zeitliche Eingrenzung im UI
}