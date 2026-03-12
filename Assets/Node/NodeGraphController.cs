// ════════════════════════════════════════════════════════════════
//  NodeGraphController.cs  — NODE-TYCOON  Entry Point
//
//  Canvas hierarchy:
//    [GENRE] ─→ [SYSTEM]  ─→ [FEATURE] ─→ [FEATURE] …
//            ─→ [SYSTEM]  ─→ [FEATURE] …
//            ─→ [ENGINE]  (Graphic / Sound renderer)
//            ─→ [SUPPORT] (global leaf nodes)
//
//  Sidebar tabs:
//    "SYSTEM"   → SystemNode  (major mechanics, high CPU, no prereqs)
//    "FEATURE"  → FeatureNode (sub-features, chain onto Systems)
//    "SUPPORT"  → SupportNode (Audio, Save, Shader…)
//    "OPTIMIZER"→ OptimizeNode (CPU reducer)
//
//  Score display (topbar right):
//    S_Fit · S_Quality · S_Tech · Final score 0–100
// ════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────
    [Header("Data")]
    public FeatureDatabase featureDB;

    [Header("Platform CPU Budgets")]
    public float cpuBudget1972 = 80f;
    public float cpuBudget1977 = 100f;
    public float cpuBudget1982 = 120f;
    public float cpuBudget1985 = 140f;
    public float cpuBudget1990 = 180f;

    public float maxCpu = 120f;

    // ── Active genre / project data ───────────────────────────────
    public GenreSO  ActiveGenre    { get; set; }
    public GameData ActiveGameData { get; set; }

    // ── Internal services ─────────────────────────────────────────
    private NodeGraph          _graph;
    private NodeScoringService _scoring;

    // ── UI references ─────────────────────────────────────────────
    private VisualElement _canvas;
    private VisualElement _canvasContent;
    private VisualElement _inspector;
    private Label         _nodeCountLabel;
    private Label         _connCountLabel;
    private Label         _cpuValueLabel;
    private VisualElement _cpuBarFill;
    private Label         _cpuPctLabel;

    // Score labels
    private Label _scoreFitLabel;
    private Label _scoreQualityLabel;
    private Label _scoreTechLabel;
    private Label _scoreFinalLabel;

    // ── Node view cache ───────────────────────────────────────────
    private readonly Dictionary<string, VisualElement> _nodeViews = new();
    private string _selectedNodeId;

    // ── Wire drawing ──────────────────────────────────────────────
    private bool      _isDrawingWire;
    private NodePort  _wireSource;
    private Vector2   _wireEndWorld;
    private WireDropSuggestionPanel _wireDrop;

    // ── Pan / Zoom ────────────────────────────────────────────────
    private bool    _isPanning;
    private Vector2 _panOffset = Vector2.zero;
    private float   _zoom      = 1f;
    private const float ZoomMin = 0.25f;
    private const float ZoomMax = 2.5f;

    // ── Popup / Tooltip ───────────────────────────────────────────
    private FeaturePopupController _featurePopup;
    private VisualElement          _tooltip;

    // ── Category colour map ───────────────────────────────────────
    internal static readonly Dictionary<FeatureSO.FeatureCategory, Color> CategoryColors = new()
    {
        { FeatureSO.FeatureCategory.Gameplay,  new Color(0.96f, 0.62f, 0.04f) },
        { FeatureSO.FeatureCategory.Graphic,   new Color(0.94f, 0.27f, 0.27f) },
        { FeatureSO.FeatureCategory.Sound,     new Color(0.13f, 0.77f, 0.37f) },
        { FeatureSO.FeatureCategory.Tech,      new Color(0.23f, 0.51f, 0.96f) },
        { FeatureSO.FeatureCategory.Narrative, new Color(0.66f, 0.33f, 0.97f) },
        { FeatureSO.FeatureCategory.UX,        new Color(0.08f, 0.72f, 0.65f) },
    };

    // ── NodeKind colour map ───────────────────────────────────────
    internal static readonly Dictionary<NodeKind, Color> KindColors = new()
    {
        { NodeKind.Genre,    new Color(0.22f, 0.78f, 0.95f) },  // Cyan
        { NodeKind.System,   new Color(0.96f, 0.62f, 0.04f) },  // Orange
        { NodeKind.Feature,  new Color(0.48f, 0.85f, 0.32f) },  // Green
        { NodeKind.Engine,   new Color(0.23f, 0.51f, 0.96f) },  // Blue
        { NodeKind.Support,  new Color(0.66f, 0.33f, 0.97f) },  // Violet
        { NodeKind.Optimize, new Color(0.94f, 0.27f, 0.27f) },  // Red
    };

    // ════════════════════════════════════════════════════════════════
    //  SETUP
    // ════════════════════════════════════════════════════════════════

    public void SetupGraph(VisualElement root)
    {
        if (ActiveGameData != null)
            maxCpu = GetBudgetForYear(ActiveGameData.releaseWeek);

        _graph   = new NodeGraph();
        _scoring = new NodeScoringService(featureDB, ActiveGenre);

        _nodeViews.Clear();
        _selectedNodeId = null;

        CacheUIRefs(root);
        if (_canvas == null) { Debug.LogError("[NodeGraph] 'NodeCanvas' not found!"); return; }

        SetupCanvas(root);
        SetupSidebar(root);

        _featurePopup = FeaturePopupController.Build(root, _scoring);
        _wireDrop     = new WireDropSuggestionPanel(root, _scoring);

        // Spawn the Genre (root) node
        var genreNode = new GenreNode(
            ActiveGameData?.gameName ?? "New Game",
            ActiveGameData?.platform ?? "PC",
            maxCpu,
            ActiveGenre);

        genreNode.CanvasPosition = new Vector2(60, 200);
        _graph.AddNode(genreNode);
        RebuildCanvas();

        _graph.OnNodeAdded       += _ => RebuildCanvas();
        _graph.OnNodeRemoved     += _ => RebuildCanvas();
        _graph.OnConnectionAdded += _ => { RebuildCanvas(); RefreshScore(); };
        _graph.OnConnectionRemoved += _ => { RebuildCanvas(); RefreshScore(); };
        _graph.OnValidationError += msg => ShowToast(msg, isError: true);
    }

    private float GetBudgetForYear(int releaseWeek)
    {
        int year = 1970 + releaseWeek / 52;
        if (year < 1977) return cpuBudget1972;
        if (year < 1982) return cpuBudget1977;
        if (year < 1985) return cpuBudget1982;
        if (year < 1990) return cpuBudget1985;
        return cpuBudget1990;
    }

    // ════════════════════════════════════════════════════════════════
    //  SCORE REFRESH
    // ════════════════════════════════════════════════════════════════

    private void RefreshScore()
    {
        var result = _graph.EvaluateGame(maxCpu);

        float cpuPct = maxCpu > 0 ? result.TotalCpuUsage / maxCpu : 0f;
        if (_cpuBarFill != null)
        {
            _cpuBarFill.style.width = Length.Percent(Mathf.Clamp01(cpuPct) * 100f);
            _cpuBarFill.EnableInClassList("cpu-bar-fill--warning",  cpuPct > 0.80f && cpuPct <= 0.95f);
            _cpuBarFill.EnableInClassList("cpu-bar-fill--critical", cpuPct > 0.95f);
        }
        if (_cpuValueLabel != null)
            _cpuValueLabel.text = $"{result.TotalCpuUsage:0} / {maxCpu:0}";
        if (_cpuPctLabel != null)
            _cpuPctLabel.text   = $"{cpuPct * 100f:0}%";

        if (_scoreFitLabel     != null) _scoreFitLabel.text     = $"{result.S_Fit * 100f:0}";
        if (_scoreQualityLabel != null) _scoreQualityLabel.text = $"{result.S_Quality * 100f:0}";
        if (_scoreTechLabel    != null) _scoreTechLabel.text    = $"{result.S_Tech * 100f:0}";
        if (_scoreFinalLabel   != null) _scoreFinalLabel.text   = $"{result.FinalScore:0}%";
    }

    // ════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ════════════════════════════════════════════════════════════════

    private void CacheUIRefs(VisualElement root)
    {
        _canvas         = root.Q<VisualElement>("NodeCanvas");
        _canvasContent  = root.Q<VisualElement>("CanvasContent") ?? _canvas;
        _inspector      = root.Q<VisualElement>("NodeInspector");
        _nodeCountLabel = root.Q<Label>("LabelNodeCount");
        _connCountLabel = root.Q<Label>("LabelConnCount");
        _cpuValueLabel  = root.Q<Label>("LabelCpuValue");
        _cpuBarFill     = root.Q<VisualElement>("CpuBarFill");
        _cpuPctLabel    = root.Q<Label>("LabelCpuPct");
        _scoreFitLabel     = root.Q<Label>("LabelScoreFit");
        _scoreQualityLabel = root.Q<Label>("LabelScoreQuality");
        _scoreTechLabel    = root.Q<Label>("LabelScoreTech");
        _scoreFinalLabel   = root.Q<Label>("LabelFinalScore");
        _tooltip        = root.Q<VisualElement>("FeatureTooltip");
    }

    private void ShowToast(string message, bool isError = false)
    {
        Debug.Log($"[NodeGraph] {(isError ? "ERROR" : "INFO")}: {message}");
        // TODO: wire up UI toast element
    }
}