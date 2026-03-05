using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class GameCreatorController : MonoBehaviour
{
    [Header("Datenbanken")]
    public GenreDatabase genreDB;
    public topicDatabase topicDB;
    public targetGroupDatabase targetDB;
    public FeatureDatabase featureDB;

    [Header("Hardware Limits")]
    public float maxCpu = 100f;
    public float maxRam = 100f;

    // Interne Listen & Referenzen
    private List<FeatureSO> selectedFeatures = new List<FeatureSO>();
    private NodeConnectionService connectionService;
    private VisualElement _board;
    private VisualElement _core;
    private ProgressBar cpuBar;
    private ProgressBar ramBar;

    // --- 1. SETUP PHASE (Dropdowns & Platzhalter) ---
    public void SetupDropdowns(VisualElement root)
    {
        // Diese Methode wird aufgerufen, wenn man Name/Genre eingibt
        root.Q<DropdownField>("DropdownGenre").choices = genreDB.genres.Select(g => g.genreName).ToList();
        root.Q<DropdownField>("DropdownTopic").choices = topicDB.topics.Select(t => t.topicName).ToList();
        root.Q<DropdownField>("DropdownAudience").choices = targetDB.targetGroups.Select(tg => tg.displayName).ToList();
        
    }

    // --- 2. BAUKASTEN PHASE (Das Board) ---
    public void SetupBaukasten(VisualElement baukastenRoot)
    {
        _board = baukastenRoot.Q<VisualElement>("DevelopmentBoard");
        VisualElement spawnArea = _board.Q("GridContent") ?? _board;

        // Core finden oder neu bauen
        _core = spawnArea.Q("GameCore") ?? CreateCore();
        if (_core.parent == null) spawnArea.Add(_core);

        connectionService = new NodeConnectionService(spawnArea);
        cpuBar = baukastenRoot.Q<ProgressBar>("CpuBar");
        ramBar = baukastenRoot.Q<ProgressBar>("RamBar");

        selectedFeatures.Clear();
        connectionService.AllNodes.Clear();

        // Start-Slots (Grafik, Sound, Gameplay, Technik)
        CreateStartSlots(spawnArea);

        // Nur erforschte Features spawnen
        SpawnResearchedFeatures(spawnArea);

        UpdateHardwareLoad();
    }

    private void SpawnResearchedFeatures(VisualElement spawnArea)
    {
        // WICHTIG: Hier greift die Forschung. Nur was "isResearched" ist, erscheint im Pool
        var pool = featureDB.allFeatures.Where(f => f.isResearched).ToList();

        foreach (var feature in pool)
        {
            VisualElement node = new VisualElement();
            node.style.position = Position.Absolute;
            node.style.width = 120; node.style.height = 50;
            node.style.backgroundColor = GetColorForCategory(feature.category);
            
            // Zufällige Position im Start-Bereich
            node.style.left = 2500 + UnityEngine.Random.Range(-300, 300);
            node.style.top = 2500 + UnityEngine.Random.Range(-300, 300);

            node.Add(new Label(feature.featureName) { style = { color = Color.white, unityTextAlign = TextAnchor.MiddleCenter } });

            MakeDraggable(node, feature);
            spawnArea.Add(node);
        }
    }

    // --- 3. LOGIK: SNAPPING & SLOTS ---
    private void SnapToSlot(VisualElement node, FeatureSO feature, VisualElement slot)
    {
        // Passt es noch in die Hardware?
        if (selectedFeatures.Sum(f => f.cpuUsage) + feature.cpuUsage > maxCpu) return;

        slot.Clear();
        slot.Add(node);
        node.style.position = Position.Relative;
        node.style.width = Length.Percent(100);
        node.style.height = Length.Percent(100);

        selectedFeatures.Add(feature);
        UpdateHardwareLoad();

        // Wenn das Feature neue Wege öffnet (canExpand)
        if (feature.canExpand) CreateNextSlots(slot);

        // Linie zeichnen
        FeatureNode fn = new FeatureNode(slot, feature);
        fn.Parent = connectionService.AllNodes.Find(x => x.Element == slot.parent || x.Element == _core);
        connectionService.AllNodes.Add(fn);
        
        connectionService.RequestRedraw();
    }

    // --- 4. EXPORT FÜR UI & GAMEMANAGER ---
    public List<FeatureSO> GetSelectedFeatures()
    {
        // Diese Methode ist kritisch für den UIController, um das Spiel final zu speichern
        return new List<FeatureSO>(selectedFeatures);
    }

    private void UpdateHardwareLoad()
    {
        float curCpu = selectedFeatures.Sum(f => f.cpuUsage);
        float curRam = selectedFeatures.Sum(f => f.ramUsage);
        if (cpuBar != null) { cpuBar.value = curCpu; cpuBar.title = $"CPU: {curCpu}%"; }
        if (ramBar != null) { ramBar.value = curRam; ramBar.title = $"RAM: {curRam}%"; }
    }

    // Hilfsmethoden für Visuals
    private VisualElement CreateCore() { /* ... wie gehabt ... */ return new VisualElement(); }
    private void CreateStartSlots(VisualElement area) { /* ... 4 Richtungen ... */ }
    private void CreateNextSlots(VisualElement parent) { /* ... 90 Grad ... */ }
    private void MakeDraggable(VisualElement node, FeatureSO f) { /* ... Pointer Events ... */ }
    public Color GetColorForCategory(FeatureSO.FeatureCategory cat) => Color.white; // Platzhalter
}