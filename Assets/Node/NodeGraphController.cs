// ════════════════════════════════════════════════════════════════
//  NodeGraphController.cs  — Entry point & shared state
//
//  Responsibilities of this file only:
//    • Inspector fields
//    • SetupGraph() orchestration
//    • Graph event routing
//    • Hardware refresh + stats update
//    • FinalizeGame + Toast
//
//  Everything else lives in the four sibling partial files:
//    • NodeGraphController.Canvas.cs   — pan, wire draw, keyboard
//    • NodeGraphController.Nodes.cs    — card builder, drag, ports
//    • NodeGraphController.Sidebar.cs  — cards, drag, filter, tooltip
//    • NodeGraphController.Scoring.cs  — soft genre filter, quality delta
// ════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Header("Data")]
    public FeatureDatabase featureDB;
    

    [Header("Hardware Budget")]
    public float maxCpu = 100f;
    public float maxRam = 100f;

    // ── Active genre (set by UIController before SetupGraph) ───────
    public GenreSO ActiveGenre { get; set; }

    // ── Public events ──────────────────────────────────────────────
    public event Action<GameBuildResult> OnGameFinalized;

    // ── Core data ──────────────────────────────────────────────────
    private NodeGraph          _graph;
    private NodeScoringService _scoring;

    // ── Canvas & layers ────────────────────────────────────────────
    private VisualElement _canvas;
    private VisualElement _canvasContent;   // translated for pan

    // ── UI references ──────────────────────────────────────────────
    private VisualElement _toastEl;
    private ProgressBar   _cpuBar;
    private ProgressBar   _ramBar;
    private Label         _qualityLabel;
    private Label         _nodeCountLabel;
    private Label         _connCountLabel;
    private VisualElement _inspector;
    private VisualElement _tooltip;
    private VisualElement _ghostCard;

    // ── Node view registry ─────────────────────────────────────────
    private readonly Dictionary<string, VisualElement> _nodeViews = new();
    private string _selectedNodeId;

    // ── Pillar colours — used by Canvas, Nodes, Scoring ────────────
    internal static readonly Dictionary<FeatureSO.FeatureCategory, Color> PillarColors = new()
    {
        { FeatureSO.FeatureCategory.Gameplay,  new Color(0.96f, 0.62f, 0.04f) },
        { FeatureSO.FeatureCategory.Graphic,   new Color(0.94f, 0.27f, 0.27f) },
        { FeatureSO.FeatureCategory.Sound,     new Color(0.13f, 0.77f, 0.37f) },
        { FeatureSO.FeatureCategory.Tech,      new Color(0.23f, 0.51f, 0.96f) },
        { FeatureSO.FeatureCategory.Narrative, new Color(0.66f, 0.33f, 0.97f) },  // purple
        { FeatureSO.FeatureCategory.UX,        new Color(0.08f, 0.72f, 0.65f) },  // teal
    };

    // ════════════════════════════════════════════════════════════════
    //  SETUP
    // ════════════════════════════════════════════════════════════════

    public void SetupGraph(VisualElement root)
    {
        _graph   = new NodeGraph();
        _scoring = new NodeScoringService(featureDB, ActiveGenre);

       

     

        _nodeViews.Clear();
        _selectedNodeId = null;

        CacheUIRefs(root);
        if (_canvas == null) { Debug.LogError("[NodeGraph] 'NodeCanvas' not found!"); return; }

        InitCanvasLayer();      // Canvas.cs  — content layer, tooltip, ghost, painter
        InitCanvasEvents();     // Canvas.cs  — pointer + keyboard callbacks
        HookGraphEvents();      // (below)
        SpawnPillarStarts();
        BuildSidebar(root);     // Sidebar.cs
        WireButtons(root);
        UpdateCanvasHint();
    }

    private void CacheUIRefs(VisualElement root)
    {
        _canvas         = root.Q<VisualElement>("NodeCanvas");
        _toastEl        = root.Q<VisualElement>("ToastMessage");
        _cpuBar         = root.Q<ProgressBar>("CpuBar");
        _ramBar         = root.Q<ProgressBar>("RamBar");
        _qualityLabel   = root.Q<Label>("QualityLabel");
        _nodeCountLabel = root.Q<Label>("LabelNodeCount");
        _connCountLabel = root.Q<Label>("LabelConnCount");
        _inspector      = root.Q<VisualElement>("NodeInspector");
    }

    private void WireButtons(VisualElement root)
    {
        root.Q<Button>("BtnFinalize")      ?.RegisterCallback<ClickEvent>(_ => FinalizeGame());
        root.Q<Button>("BtnAddOptimizer")  ?.RegisterCallback<ClickEvent>(_ => SpawnOptimizer());
        root.Q<Button>("BtnDeleteNode")    ?.RegisterCallback<ClickEvent>(_ => DeleteSelected());
        root.Q<Button>("BtnDuplicateNode") ?.RegisterCallback<ClickEvent>(_ => DuplicateSelected());
    }

    private void SpawnPillarStarts()
    {
        var cats = (FeatureSO.FeatureCategory[])Enum.GetValues(typeof(FeatureSO.FeatureCategory));
        float x = 50f, y0 = 40f, gap = 190f;
        foreach (var cat in cats)
        {
            var node = new PillarStartNode(cat);
            node.CanvasPosition = new Vector2(x, y0 + (int)cat * gap);
            _graph.AddNode(node);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  GRAPH EVENT ROUTING
    // ════════════════════════════════════════════════════════════════

    private void HookGraphEvents()
    {
        _graph.OnNodeAdded         += OnNodeAdded;
        _graph.OnNodeRemoved       += OnNodeRemoved;
        _graph.OnConnectionAdded   += _ => OnConnectionChanged();
        _graph.OnConnectionRemoved += _ => OnConnectionChanged();
        _graph.OnValidationError   += msg => ShowToast("⚠ " + msg, isError: true);
    }

    private void OnNodeAdded(GameNode node)
    {
        var card = CreateNodeCard(node);   // Nodes.cs
        _canvasContent.Add(card);
        _nodeViews[node.NodeId] = card;
        RefreshHardware();
        UpdateStats();
        UpdateCanvasHint();
        RefreshGenreHighlights(); 
      
    }

    private void OnNodeRemoved(GameNode node)
    {
        if (_nodeViews.TryGetValue(node.NodeId, out var card))
        {
            _canvasContent.Remove(card);
            _nodeViews.Remove(node.NodeId);
        }
        if (_selectedNodeId == node.NodeId) SelectNode(null);
        RefreshHardware();
        UpdateStats();
        UpdateCanvasHint();
      
    }

    private void OnConnectionChanged()
    {
        RefreshHardware();
        UpdateStats();
        _canvas.MarkDirtyRepaint();
       
    }

    // ════════════════════════════════════════════════════════════════
    //  HARDWARE + STATS
    // ════════════════════════════════════════════════════════════════

    private void RefreshHardware()
    {
        var r = _graph.Evaluate(maxCpu, maxRam);

        if (_cpuBar != null)
        {
            _cpuBar.highValue = maxCpu;
            _cpuBar.value     = r.TotalCpuUsage;
            _cpuBar.title     = $"CPU {r.TotalCpuUsage:0}/{maxCpu:0}%";
        }
        if (_ramBar != null)
        {
            _ramBar.highValue = maxRam;
            _ramBar.value     = r.TotalRamUsage;
            _ramBar.title     = $"RAM {r.TotalRamUsage:0}/{maxRam:0}%";
        }
        if (_qualityLabel != null)
            _qualityLabel.text = $"Qualität: {r.QualityScore:0.0} / 100";
    }

    private void UpdateStats()
    {
        if (_nodeCountLabel != null) _nodeCountLabel.text = "Nodes: "        + _nodeViews.Count;
        if (_connCountLabel != null) _connCountLabel.text = "Verbindungen: " + _graph.AllConnections.Count();
    }

    private void UpdateCanvasHint()
    {
        var hint = _canvas?.Q<VisualElement>("CanvasHint");
        if (hint == null) return;
        bool hasFeature = _graph.AllNodes.Any(n => n.Kind == NodeKind.Feature);
        if (hasFeature) hint.AddToClassList("hidden");
        else            hint.RemoveFromClassList("hidden");
    }

    // ════════════════════════════════════════════════════════════════
    //  FINALIZE
    // ════════════════════════════════════════════════════════════════

    private void FinalizeGame()
    {
        var r = _graph.Evaluate(maxCpu, maxRam);
        if (!r.IsValid)
        {
            string msg = "";
            if (r.CpuOverBudget > 0) msg += $"CPU um {r.CpuOverBudget:0}% überschritten. ";
            if (r.RamOverBudget > 0) msg += $"RAM um {r.RamOverBudget:0}% überschritten. ";
            ShowToast("⚠ " + msg, isError: true);
            return;
        }
        ShowToast($"✓ {r.SelectedFeatures.Count} Features · Qualität {r.QualityScore:0}/100");
        OnGameFinalized?.Invoke(r);
    }

    // ════════════════════════════════════════════════════════════════
    //  TOAST
    // ════════════════════════════════════════════════════════════════

    private void ShowToast(string msg, bool isError = false)
    {
        if (_toastEl == null) return;
        var lbl = _toastEl.Q<Label>("ToastLabel") ?? _toastEl.Q<Label>();
        if (lbl != null) lbl.text = msg;
        _toastEl.RemoveFromClassList("hidden");
        _toastEl.EnableInClassList("toast-error", isError);
        _toastEl.schedule.Execute(() => _toastEl.AddToClassList("hidden")).StartingIn(3500);
    }
}