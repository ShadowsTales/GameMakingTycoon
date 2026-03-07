// ════════════════════════════════════════════════════════════════
//  NodeGraphController.cs  — NODE-TYCOON Entry Point
//
//  Die Hierarchie im Canvas:
//    [CORE-Node] ─→ [ANCHOR] ─→ [UPGRADE] ─→ [UPGRADE] …
//                ─→ [ANCHOR] ─→ [UPGRADE] …
//                ─→ [SUPPORT] (globale Nodes)
//
//  Sidebar-Kategorisierung:
//    Tab "ANKER"    → AnchorNode (Main-Features, hohe CPU-Last)
//    Tab "UPGRADE"  → UpgradeNode (Sub-Features, docken an Anker)
//    Tab "SUPPORT"  → SupportNode (Audio, Save, Shader …)
//    Tab "OPTIMIZER"→ OptimizerNode (CPU-Reduktion)
//
//  Score-Anzeige (Topbar rechts):
//    S_Fit · S_Quality · S_Tech · Gesamt-Score 0–100
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

    [Header("Plattform-Budgets")]
    public float cpuBudget1972 = 80f;
    public float cpuBudget1977 = 100f;
    public float cpuBudget1982 = 120f;
    public float cpuBudget1985 = 140f;
    public float cpuBudget1990 = 180f;

    // Aktives Budget (wird von Plattform/Jahr gesetzt)
    public float maxCpu = 120f;
    public float maxRam = 100f;   // Legacy — wird intern noch verwendet

    // ── Active genre / data ───────────────────────────────────────
    public GenreSO   ActiveGenre    { get; set; }
    public GameData  ActiveGameData { get; set; }

    // ── Public events ─────────────────────────────────────────────
    public event Action<NodeTycoonBuildResult> OnGameFinalized;
    // Legacy shim für alten UIController-Code
    public event Action<GameBuildResult> OnGameFinalizedLegacy;

    // ── Core data ─────────────────────────────────────────────────
    private NodeGraph          _graph;
    private NodeScoringService _scoring;

    // ── Canvas ────────────────────────────────────────────────────
    private VisualElement _canvas;
    private VisualElement _canvasContent;

    // ── Score-Labels (Topbar) ─────────────────────────────────────
    private Label _labelSFit;
    private Label _labelSQuality;
    private Label _labelSTech;
    private Label _labelFinalScore;
    private Label _labelCpuBudget;

    // ── Legacy refs ───────────────────────────────────────────────
    private VisualElement _toastEl;
    private ProgressBar   _cpuBar;
    private ProgressBar   _ramBar;
    private Label         _qualityLabel;
    private Label         _nodeCountLabel;
    private Label         _connCountLabel;
    private VisualElement _inspector;
    private VisualElement _tooltip;
    private VisualElement _ghostCard;

    // ── Node view registry ────────────────────────────────────────
    private readonly Dictionary<string, VisualElement> _nodeViews = new();
    private string _selectedNodeId;

    // ── Pillar colours (shared across partials) ───────────────────
    internal static readonly Dictionary<FeatureSO.FeatureCategory, Color> PillarColors = new()
    {
        { FeatureSO.FeatureCategory.Gameplay,  new Color(0.96f, 0.62f, 0.04f) },
        { FeatureSO.FeatureCategory.Graphic,   new Color(0.94f, 0.27f, 0.27f) },
        { FeatureSO.FeatureCategory.Sound,     new Color(0.13f, 0.77f, 0.37f) },
        { FeatureSO.FeatureCategory.Tech,      new Color(0.23f, 0.51f, 0.96f) },
        { FeatureSO.FeatureCategory.Narrative, new Color(0.66f, 0.33f, 0.97f) },
        { FeatureSO.FeatureCategory.UX,        new Color(0.08f, 0.72f, 0.65f) },
    };

    // Farben nach NodeKind für das neue Design
    internal static readonly Dictionary<NodeKind, Color> KindColors = new()
    {
        { NodeKind.Core,      new Color(0.22f, 0.78f, 0.95f) },  // Cyan — Core
        { NodeKind.Anchor,    new Color(0.96f, 0.62f, 0.04f) },  // Orange — Anker
        { NodeKind.Upgrade,   new Color(0.48f, 0.85f, 0.32f) },  // Grün — Upgrade
        { NodeKind.Support,   new Color(0.66f, 0.33f, 0.97f) },  // Violett — Support
        { NodeKind.Optimizer, new Color(0.94f, 0.27f, 0.27f) },  // Rot — Optimizer
    };

    // ════════════════════════════════════════════════════════════════
    //  SETUP
    // ════════════════════════════════════════════════════════════════

    public void SetupGraph(VisualElement root)
    {
        // Budget aus ActiveGameData ableiten
        if (ActiveGameData != null)
            maxCpu = GetBudgetForYear(ActiveGameData.releaseWeek);

        _graph   = new NodeGraph();
        _scoring = new NodeScoringService(featureDB, ActiveGenre);

        _nodeViews.Clear();
        _selectedNodeId = null;

        CacheUIRefs(root);
        if (_canvas == null) { Debug.LogError("[NodeGraph] 'NodeCanvas' not found!"); return; }

        InitCanvasLayer();
        InitCanvasEvents();
        HookGraphEvents();
        SpawnCoreNode();         // NEU: Ein einziger Core-Node statt 6 Pillar-Starts
        BuildSidebar(root);
        WireButtons(root);
        RefreshScoreDisplay();
        UpdateCanvasHint();
    }

    private float GetBudgetForYear(int week)
    {
        int year = 1972 + week / 52;
        if (year < 1977) return cpuBudget1972;
        if (year < 1982) return cpuBudget1977;
        if (year < 1985) return cpuBudget1982;
        if (year < 1990) return cpuBudget1985;
        return cpuBudget1990;
    }

    // -- Neue CPU-Bar-Referenzen
    private VisualElement _cpuBarFill;
    private Label         _cpuValueLabel;
    private Label         _cpuPctLabel;

    private void CacheUIRefs(VisualElement root)
    {
        _canvas         = root.Q<VisualElement>("NodeCanvas");
        _toastEl        = root.Q<VisualElement>("ToastMessage");
        _cpuBar         = root.Q<ProgressBar>("CpuBar");   // Legacy-Fallback
        _qualityLabel   = root.Q<Label>("QualityLabel");
        _nodeCountLabel = root.Q<Label>("LabelNodeCount");
        _connCountLabel = root.Q<Label>("LabelConnCount");
        _inspector      = root.Q<VisualElement>("NodeInspector");

        // Score-Labels
        _labelSFit       = root.Q<Label>("LabelSFit");
        _labelSQuality   = root.Q<Label>("LabelSQuality");
        _labelSTech      = root.Q<Label>("LabelSTech");
        _labelFinalScore = root.Q<Label>("LabelFinalScore");
        _labelCpuBudget  = root.Q<Label>("LabelCpuBudget");

        // Neue custom CPU-Bar
        _cpuBarFill    = root.Q<VisualElement>("CpuBarFill");
        _cpuValueLabel = root.Q<Label>("LabelCpuValue");
        _cpuPctLabel   = root.Q<Label>("LabelCpuPct");
    }

    private void WireButtons(VisualElement root)
    {
        root.Q<Button>("BtnFinalize")     ?.RegisterCallback<ClickEvent>(_ => FinalizeGame());
        root.Q<Button>("BtnAddOptimizer") ?.RegisterCallback<ClickEvent>(_ => SpawnOptimizer());
        root.Q<Button>("BtnDeleteNode")   ?.RegisterCallback<ClickEvent>(_ => DeleteSelected());
        root.Q<Button>("BtnDuplicateNode")?.RegisterCallback<ClickEvent>(_ => DuplicateSelected());
        root.Q<Button>("BtnShowPopup")    ?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_selectedNodeId != null) OpenFeaturePopupForNode(_selectedNodeId);
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWN CORE NODE (ersetzt SpawnPillarStarts)
    // ════════════════════════════════════════════════════════════════

    private void SpawnCoreNode()
    {
        string projectName = ActiveGameData?.gameName ?? "Neues Projekt";
        string platform    = "8-Bit Konsole";

        // Plattform aus GameData falls vorhanden
        // (In echtem Spiel: platform = ActiveGameData.platform)

        var core = new CoreNode(projectName, platform, maxCpu)
        {
            CanvasPosition = new Vector2(80f, 300f)
        };
        _graph.AddNode(core);
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
        var card = CreateNodeCard(node);
        _canvasContent.Add(card);
        _nodeViews[node.NodeId] = card;
        RefreshScoreDisplay();
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
        RefreshScoreDisplay();
        UpdateStats();
        UpdateCanvasHint();
    }

    private void OnConnectionChanged()
    {
        RefreshScoreDisplay();
        UpdateStats();
        _canvas.MarkDirtyRepaint();
    }

    // ════════════════════════════════════════════════════════════════
    //  SCORE DISPLAY (NODE-TYCOON)
    // ════════════════════════════════════════════════════════════════

    private void RefreshScoreDisplay()
    {
        var r = _graph.EvaluateTycoon(maxCpu);

        // ── Score-Säulen ─────────────────────────────────────────
        if (_labelSFit     != null) _labelSFit.text     = $"{r.S_Fit:0.00}";
        if (_labelSQuality != null) _labelSQuality.text = $"{r.S_Quality:0.00}";
        if (_labelSTech    != null) _labelSTech.text    = $"{r.S_Tech:0.00}";

        if (_labelFinalScore != null)
        {
            _labelFinalScore.text = $"{r.FinalScore:0}%";
            var box = _labelFinalScore.parent; // ScoreFinalBox
            box?.RemoveFromClassList("score-excellent");
            box?.RemoveFromClassList("score-good");
            box?.RemoveFromClassList("score-poor");
            if      (r.FinalScore >= 80) box?.AddToClassList("score-excellent");
            else if (r.FinalScore >= 55) box?.AddToClassList("score-good");
            else                         box?.AddToClassList("score-poor");
        }

        // ── Verbesserte CPU-Bar ──────────────────────────────────
        float cpuPct = Mathf.Clamp01(r.TotalCpuUsage / Mathf.Max(1f, maxCpu));
        if (_cpuBarFill != null)
        {
            _cpuBarFill.style.width = Length.Percent(cpuPct * 100f);
            _cpuBarFill.RemoveFromClassList("cpu-bar-fill--warning");
            _cpuBarFill.RemoveFromClassList("cpu-bar-fill--critical");
            if      (cpuPct >= 1.0f) _cpuBarFill.AddToClassList("cpu-bar-fill--critical");
            else if (cpuPct >= 0.8f) _cpuBarFill.AddToClassList("cpu-bar-fill--warning");
        }
        if (_cpuValueLabel != null)
            _cpuValueLabel.text = $"{r.TotalCpuUsage:0} / {maxCpu:0}";
        if (_cpuPctLabel != null)
            _cpuPctLabel.text = $"{cpuPct:0%}";

        // Label-Farbe
        if (_cpuValueLabel != null)
        {
            _cpuValueLabel.RemoveFromClassList("budget-warning");
            _cpuValueLabel.RemoveFromClassList("budget-critical");
            if      (cpuPct >= 1.0f) _cpuValueLabel.AddToClassList("budget-critical");
            else if (cpuPct >= 0.8f) _cpuValueLabel.AddToClassList("budget-warning");
        }

        // Legacy CPU-Bar (falls ProgressBar noch im UXML)
        if (_cpuBar != null) { _cpuBar.highValue = maxCpu; _cpuBar.value = r.TotalCpuUsage; }

        // Statusbar + Budget-Label
        if (_labelCpuBudget != null) _labelCpuBudget.text = "CPU-BUDGET";
        if (_qualityLabel   != null) _qualityLabel.text   = $"Score: {r.FinalScore:0} / 100";
    }

    // Legacy shim
    private void RefreshHardware() => RefreshScoreDisplay();

    /*private void UpdateStats()
    {
        if (_nodeCountLabel != null) _nodeCountLabel.text = "Nodes: "        + _nodeViews.Count;
        if (_connCountLabel != null) _connCountLabel.text = "Verb.: "        + _graph.AllConnections.Count();
    }

    private void UpdateCanvasHint()
    {
        var hint = _canvas?.Q<VisualElement>("CanvasHint");
        if (hint == null) return;
        bool hasAnchor = _graph.AllNodes.Any(n => n.Kind == NodeKind.Anchor);
        if (hasAnchor) hint.AddToClassList("hidden");
        else           hint.RemoveFromClassList("hidden");
    }
    */
    // ════════════════════════════════════════════════════════════════
    //  FINALIZE
    // ════════════════════════════════════════════════════════════════

    private void FinalizeGame()
    {
        var r = _graph.EvaluateTycoon(maxCpu);

        if (!r.IsValid)
        {
            string msg = "";
            if (r.CpuOverBudget > 0) msg += $"CPU um {r.CpuOverBudget:0} überschritten! ";
            if (r.ConflictPairs.Count > 0)
                msg += $"{r.ConflictPairs.Count} Konflikte gefunden.";
            ShowToast("⚠ " + msg, isError: true);
            return;
        }

        ShowToast($"✓ {r.AnchorNodes.Count} Anker · {r.UpgradeNodes.Count} Upgrades · Score {r.FinalScore:0}/100");
        OnGameFinalized?.Invoke(r);

        // Legacy
        var legacyResult = new GameBuildResult
        {
            SelectedFeatures  = r.SelectedFeatures,
            TotalCpuUsage     = r.TotalCpuUsage,
            QualityScore      = r.FinalScore,
            IsValid           = r.IsValid,
            SynergyBonusTotal = r.SynergyBonusTotal,
        };
        OnGameFinalizedLegacy?.Invoke(legacyResult);
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWN OPTIMIZER
    // ════════════════════════════════════════════════════════════════

    private void SpawnOptimizer()
    {
        if (_selectedNodeId == null)
        { ShowToast("Wähle zuerst einen Node mit Expand-Port."); return; }

        if (!_graph.AllNodes.Any(n => n.NodeId == _selectedNodeId)) return;

        var node = _graph.AllNodes.First(n => n.NodeId == _selectedNodeId);
        var opt  = new OptimizerNode(node.Pillar)
        {
            CanvasPosition = node.CanvasPosition + new Vector2(250f, 60f)
        };
        _graph.AddNode(opt);
        ShowToast($"Optimizer hinzugefügt (-{opt.CpuReductionPercent:0}% CPU, +{opt.DevTimeCost:0} Wo.)");
    }
    /*
    private void DeleteSelected()
    {
        if (_selectedNodeId != null) _graph.RemoveNode(_selectedNodeId);
    }

    private void DuplicateSelected()
    {
        if (_selectedNodeId == null) return;
        var source = _graph.AllNodes.FirstOrDefault(n => n.NodeId == _selectedNodeId);
        if (source == null) return;

        GameNode copy = null;
        if (source is AnchorNode  an) copy = new AnchorNode(an.Feature)  { CanvasPosition = an.CanvasPosition  + new Vector2(30f, 30f) };
        if (source is UpgradeNode un) copy = new UpgradeNode(un.Feature) { CanvasPosition = un.CanvasPosition  + new Vector2(30f, 30f) };
        if (source is SupportNode sn) copy = new SupportNode(sn.Feature) { CanvasPosition = sn.CanvasPosition  + new Vector2(30f, 30f) };
        if (copy != null) _graph.AddNode(copy);
    }
    */
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