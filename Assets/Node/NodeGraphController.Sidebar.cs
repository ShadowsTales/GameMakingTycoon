// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Sidebar.cs
//  Responsibilities: sidebar card build · drag-to-canvas · filter · tooltip
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController
{
    // ── Sidebar state ──────────────────────────────────────────────
    private readonly List<(VisualElement card, FeatureSO feature)> _sidebarCards = new();
    private VisualElement _featureList;
    private FeatureSO     _sidebarDragFeature;
    private bool          _isSidebarDragging;

    // ════════════════════════════════════════════════════════════════
    //  BUILD
    // ════════════════════════════════════════════════════════════════

    private void BuildSidebar(VisualElement root)
    {
        _featureList = root.Q<ScrollView>("FeatureList");
        if (_featureList == null) return;
        _sidebarCards.Clear();

        var sorted = featureDB.allFeatures
            .OrderByDescending(f => f.isResearched)
            .ThenBy(f => f.releaseYear)
            .ToList();

        foreach (var feature in sorted)
        {
            var card = BuildSidebarCard(feature);
            _featureList.Add(card);
            _sidebarCards.Add((card, feature));
        }
    }

    private VisualElement BuildSidebarCard(FeatureSO feature)
    {
        var card = new VisualElement();
        card.AddToClassList("sidebar-feature-card");
        card.AddToClassList("pillar-" + feature.category.ToString().ToLower());
        card.userData = feature;

        // Name row + year tag
        var row  = RowEl();
        var name = new Label(feature.isResearched ? feature.featureName : "🔒 " + feature.featureName);
        name.style.flexGrow = 1; name.style.fontSize = 11; name.pickingMode = PickingMode.Ignore;
        var yr = new Label(feature.releaseYear.ToString());
        yr.AddToClassList("sidebar-year-tag"); yr.pickingMode = PickingMode.Ignore;
        row.Add(name); row.Add(yr); card.Add(row);

        if (!string.IsNullOrEmpty(feature.description))
        {
            var desc = new Label(feature.description);
            desc.AddToClassList("sidebar-card-desc"); desc.pickingMode = PickingMode.Ignore;
            card.Add(desc);
        }

        if (!feature.isResearched) { card.AddToClassList("sidebar-feature-card--locked"); return card; }

        WireSidebarDrag(card, feature);
        card.RegisterCallback<MouseEnterEvent>(_ => ShowFeatureTooltip(feature, card));
        card.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
        return card;
    }

    // ════════════════════════════════════════════════════════════════
    //  DRAG FROM SIDEBAR → CANVAS
    // ════════════════════════════════════════════════════════════════

    private void WireSidebarDrag(VisualElement card, FeatureSO feature)
    {
        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            _sidebarDragFeature = feature; _isSidebarDragging = false;
            card.CapturePointer(evt.pointerId); evt.StopPropagation();
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_sidebarDragFeature != feature || !card.HasPointerCapture(evt.pointerId)) return;
            _isSidebarDragging = true;
            ShowGhost(feature, evt.position); evt.StopPropagation();
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_sidebarDragFeature != feature) return;
            card.ReleasePointer(evt.pointerId);
            HideGhost();

            if (_isSidebarDragging)
            {
                var local = (Vector2)_canvas.WorldToLocal(evt.position);
                var cr    = _canvas.contentRect;
                if (local.x >= 0 && local.y >= 0 && local.x <= cr.width && local.y <= cr.height)
                    SpawnFeatureNode(feature, local - _panOffset);
            }
            _sidebarDragFeature = null; _isSidebarDragging = false; evt.StopPropagation();
        });
    }

    private void ShowGhost(FeatureSO feature, Vector2 screenPos)
    {
        if (_ghostCard.childCount == 0)
        {
            var gl = new Label(feature.featureName);
            gl.style.color = Color.white; gl.style.fontSize = 11; gl.pickingMode = PickingMode.Ignore;
            _ghostCard.Add(gl);
            foreach (var cat in System.Enum.GetValues(typeof(FeatureSO.FeatureCategory)))
                _ghostCard.RemoveFromClassList("pillar-" + cat.ToString().ToLower());
            _ghostCard.AddToClassList("pillar-" + feature.category.ToString().ToLower());
        }
        var local = _canvas.WorldToLocal(screenPos);
        _ghostCard.style.left    = local.x - 70;
        _ghostCard.style.top     = local.y - 16;
        _ghostCard.style.display = DisplayStyle.Flex;
    }

    private void HideGhost() { _ghostCard.style.display = DisplayStyle.None; _ghostCard.Clear(); }

    // ════════════════════════════════════════════════════════════════
    //  FILTER (called by UIController pillar tabs)
    // ════════════════════════════════════════════════════════════════

    public void FilterSidebarByPillar(FeatureSO.FeatureCategory? pillar)
    {
        foreach (var (card, feature) in _sidebarCards)
            card.EnableInClassList("hidden", pillar.HasValue && feature.category != pillar.Value);
    }

    // ════════════════════════════════════════════════════════════════
    //  GENRE HIGHLIGHT  (soft filter — from Copilot doc)
    //  Recommended nodes get a gold accent; off-genre get muted
    // ════════════════════════════════════════════════════════════════

    private void RefreshGenreHighlights()
    {
        foreach (var (card, feature) in _sidebarCards)
        {
            card.RemoveFromClassList("sidebar-card--recommended");
            card.RemoveFromClassList("sidebar-card--off-genre");

            if (ActiveGenre == null || !feature.isResearched) continue;

            float fit = _scoring.GenreFit(feature);
            if (fit >= 0.7f) card.AddToClassList("sidebar-card--recommended");
            else if (fit < 0.3f) card.AddToClassList("sidebar-card--off-genre");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  TOOLTIP
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildTooltipElement()
    {
        var tt = new VisualElement();
        tt.AddToClassList("feature-tooltip"); tt.AddToClassList("hidden");

        tt.Add(TLabel("TT_Name",   13, Color.white,                     bold: true));
        tt.Add(TLabel("TT_Meta",    9, new Color(0.55f, 0.7f, 0.85f)));
        tt.Add(Separator());
        tt.Add(TLabel("TT_Desc",   10, new Color(0.72f, 0.77f, 0.83f), wrap: true));
        tt.Add(TLabel("TT_Tags",    9, new Color(0.47f, 0.73f, 0.47f)));
        tt.Add(TLabel("TT_Prereqs", 9, new Color(0.95f, 0.55f, 0.3f)));
        tt.Add(TLabel("TT_Cost",    9, new Color(0.47f, 0.67f, 0.9f)));
        tt.Add(TLabel("TT_GenreFit",9, new Color(0.95f, 0.82f, 0.1f)));  // NEW — genre fit score
        return tt;
    }

    private void ShowFeatureTooltip(FeatureSO f, VisualElement anchor)
    {
        if (_tooltip == null) return;
        _tooltip.RemoveFromClassList("hidden");

        TT("TT_Name",   f.featureName);
        TT("TT_Meta",   $"{f.category.ToString().ToUpper()}  ·  {f.releaseYear}");
        TT("TT_Desc",   f.description.NullIfEmpty() ?? "Keine Beschreibung vorhanden.");

        var tags = new List<string> { f.category.ToString() };
        if (f.canExpand)       tags.Add("Erweiterbar");
        if (f.cpuUsage > 15)   tags.Add("CPU-intensiv");
        if (f.ramUsage > 15)   tags.Add("RAM-intensiv");
        if (!f.prerequisites.Any()) tags.Add("Basis-Feature");
        TT("TT_Tags",   "◈ " + string.Join("  ·  ", tags));
        TT("TT_Prereqs", f.prerequisites.Any()
            ? "⚠ Benötigt: " + string.Join(", ", f.prerequisites.Select(p => p.featureName))
            : "✓ Keine Voraussetzungen");
        TT("TT_Cost", $"CPU {f.cpuUsage}%  ·  RAM {f.ramUsage}%  ·  {f.researchCostPoints} Pt.");

        // NEW — live genre fit indicator
        if (ActiveGenre != null)
        {
            float fit = _scoring.GenreFit(f);
            string fitText = fit >= 0.7f ? $"★ {ActiveGenre.genreName}: Empfohlen (+{fit*100:0}% fit)"
                           : fit < 0.3f  ? $"✗ {ActiveGenre.genreName}: Schwache Synergie"
                                         : $"○ {ActiveGenre.genreName}: Neutral";
            TT("TT_GenreFit", fitText);
        }
        else TT("TT_GenreFit", "");

        // Clamp inside canvas
        var aw = anchor.worldBound; var cw = _canvas.worldBound;
        _tooltip.style.left = Mathf.Clamp(aw.xMax - cw.x + 10, 4, cw.width  - 250);
        _tooltip.style.top  = Mathf.Clamp(aw.yMin  - cw.y,      4, cw.height - 200);
    }

    private void HideTooltip() => _tooltip?.AddToClassList("hidden");
    private void TT(string n, string t) { var l = _tooltip?.Q<Label>(n); if (l != null) l.text = t; }

    // ════════════════════════════════════════════════════════════════
    //  LOCAL HELPERS
    // ════════════════════════════════════════════════════════════════

    private static VisualElement RowEl()
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems    = Align.Center;
        r.pickingMode         = PickingMode.Ignore;
        return r;
    }

    private static Label TLabel(string name, int size, Color col, bool bold = false, bool wrap = false)
    {
        var l = new Label { name = name };
        l.style.fontSize = size; l.style.color = col;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (wrap) { l.style.whiteSpace = WhiteSpace.Normal; l.style.maxWidth = 230; }
        return l;
    }

    private static VisualElement Separator()
    {
        var s = new VisualElement();
        s.style.height = 1; s.style.marginTop = 6; s.style.marginBottom = 6;
        s.style.backgroundColor = new Color(0.2f, 0.3f, 0.45f);
        return s;
    }
}
