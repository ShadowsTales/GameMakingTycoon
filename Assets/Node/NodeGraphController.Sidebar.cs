// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Sidebar.cs  — NODE-TYCOON
//
//  TWO-LEVEL FILTER SYSTEM
//  ─────────────────────────────────────────────────────────────
//  Level 1 — Node-Type tabs (top of sidebar):
//    ■ SYSTEM   → Gameplay / Graphic / Tech, CPU ≥ 15, no prereqs
//    ◆ FEATURE  → CPU < 15 OR has prereqs (sub-features)
//    ◉ SUPPORT  → Sound / Narrative / UX  (global leaves)
//    ⚙ OPTIM.  → Spawns an OptimizeNode immediately
//
//  Level 2 — Category filter row (below tabs):
//    ALLE | GP | GFX | SND | TECH | NARR | UX
//    Dim buttons that have no items in the current tab.
//
//  TOOLTIP
//  ─────────────────────────────────────────────────────────────
//    Opaque dark panel pinned right of the sidebar.
//    Shows: name, kind, category, year, CPU, dev-time, stat,
//    prereqs, synergies, conflicts, genre-fit.
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController
{
    // ── State ─────────────────────────────────────────────────────
    private readonly List<(VisualElement card, FeatureSO feature)> _sidebarCards = new();
    private VisualElement _featureList;
    private VisualElement _sidebar;
    private FeatureSO     _sidebarDragFeature;
    private bool          _isSidebarDragging;

    private NodeKind                   _sidebarKind = NodeKind.System;
    private FeatureSO.FeatureCategory? _sidebarCat  = null;  // null = all

    // Category button name → category value (null = "ALL")
    private static readonly (string btnName, FeatureSO.FeatureCategory? cat)[] CatBtns =
    {
        ("FilterAll",       null),
        ("FilterGameplay",  FeatureSO.FeatureCategory.Gameplay),
        ("FilterGraphic",   FeatureSO.FeatureCategory.Graphic),
        ("FilterSound",     FeatureSO.FeatureCategory.Sound),
        ("FilterTech",      FeatureSO.FeatureCategory.Tech),
        ("FilterNarrative", FeatureSO.FeatureCategory.Narrative),
        ("FilterUX",        FeatureSO.FeatureCategory.UX),
    };

    // ════════════════════════════════════════════════════════════════
    //  SETUP
    // ════════════════════════════════════════════════════════════════

    private void SetupSidebar(VisualElement root)
    {
        _sidebar     = root.Q<VisualElement>("NodeGraphSidebar");
        _featureList = root.Q<ScrollView>("FeatureList");

        if (_sidebar == null || _featureList == null)
        {
            Debug.LogError("[NodeGraph] Sidebar or FeatureList not found in UXML.");
            return;
        }

        WireTabButtons(root);
        WireCatButtons(root);

        // Build all sidebar cards once; hide/show via CSS class
        foreach (var f in featureDB.allFeatures.OrderBy(f => f.releaseYear))
        {
            var card = BuildSidebarCard(f);
            _sidebarCards.Add((card, f));
            _featureList.Add(card);
        }

        // Wire the Optimizer button in the topbar
        root.Q<Button>("BtnAddOptimizer")?.RegisterCallback<ClickEvent>(_ =>
            SpawnOptimizer());

        ApplyFilter();
    }

    // ════════════════════════════════════════════════════════════════
    //  TAB WIRING
    // ════════════════════════════════════════════════════════════════

    private void WireTabButtons(VisualElement root)
    {
        var tabs = new[]
        {
            ("SidebarTabSystem",    NodeKind.System,   "Haupt-Mechaniken, die den Spieltyp definieren"),
            ("SidebarTabFeature",   NodeKind.Feature,  "Sub-Features, die an ein System andocken"),
            ("SidebarTabSupport",   NodeKind.Support,  "Globale Systeme: Audio, Save, Shader…"),
            ("SidebarTabOptimizer", NodeKind.Optimize, "Reduziert CPU-Last eines Systems"),
        };

        var descLabel = root.Q<Label>("SidebarTabDesc");

        foreach (var (name, kind, desc) in tabs)
        {
            var btn = root.Q<Button>(name);
            if (btn == null) continue;

            btn.RegisterCallback<ClickEvent>(_ =>
            {
                _sidebarKind = kind;
                _sidebarCat  = null;

                // Update active state
                foreach (var (n, _, _) in tabs)
                    root.Q<Button>(n)?.EnableInClassList("sidebar-tab--active", n == name);

                if (descLabel != null) descLabel.text = desc;

                UpdateCatHighlights(root);
                UpdateCatVisibility(root, kind);
                ApplyFilter();

                // Optimizer tab directly spawns a node
                if (kind == NodeKind.Optimize) SpawnOptimizer();
            });
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  CATEGORY FILTER WIRING
    // ════════════════════════════════════════════════════════════════

    private void WireCatButtons(VisualElement root)
    {
        foreach (var (btnName, captCat) in CatBtns)
        {
            var btn = root.Q<Button>(btnName);
            if (btn == null) continue;

            btn.RegisterCallback<ClickEvent>(_ =>
            {
                _sidebarCat = captCat;
                UpdateCatHighlights(root);
                ApplyFilter();
            });
        }

        UpdateCatVisibility(root, _sidebarKind);
    }

    private void UpdateCatHighlights(VisualElement root)
    {
        if (root == null) return;
        foreach (var (btnName, cat) in CatBtns)
            root.Q<Button>(btnName)?.EnableInClassList("filter-active", cat == _sidebarCat);
    }

    private void UpdateCatVisibility(VisualElement root, NodeKind kind)
    {
        if (root == null) return;

        var presentCats = featureDB.allFeatures
            .Where(f => f.isResearched && IsKind(f, kind))
            .Select(f => f.category)
            .Distinct()
            .ToHashSet();

        foreach (var (btnName, cat) in CatBtns)
        {
            if (cat == null) continue;  // "ALLE" always visible
            root.Q<Button>(btnName)?.EnableInClassList("filter-btn-empty", !presentCats.Contains(cat.Value));
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  FILTER APPLICATION
    // ════════════════════════════════════════════════════════════════

    private void ApplyFilter()
    {
        foreach (var (card, feat) in _sidebarCards)
        {
            bool show = IsKind(feat, _sidebarKind)
                     && (_sidebarCat == null || feat.category == _sidebarCat);
            card.EnableInClassList("hidden", !show);
        }
        RefreshGenreHighlights();
    }

    // ── Feature classification ────────────────────────────────────
    // A feature is a System if it is a primary mechanic:
    //   Gameplay / Graphic / Tech category, CPU ≥ 15, no prerequisites.
    private static bool IsSystemFeature(FeatureSO f) =>
        (f.category == FeatureSO.FeatureCategory.Gameplay ||
         f.category == FeatureSO.FeatureCategory.Graphic  ||
         f.category == FeatureSO.FeatureCategory.Tech)
        && f.cpuUsage >= 15f
        && f.prerequisites.Count == 0;

    private static bool IsSupportFeature(FeatureSO f) =>
        f.category == FeatureSO.FeatureCategory.Sound     ||
        f.category == FeatureSO.FeatureCategory.Narrative ||
        f.category == FeatureSO.FeatureCategory.UX;

    private static bool IsFeatureFeature(FeatureSO f) =>
        !IsSupportFeature(f) && !IsSystemFeature(f);

    private static bool IsKind(FeatureSO f, NodeKind k) => k switch
    {
        NodeKind.System  => IsSystemFeature(f),
        NodeKind.Feature => IsFeatureFeature(f),
        NodeKind.Support => IsSupportFeature(f),
        _                => false,
    };

    // ════════════════════════════════════════════════════════════════
    //  GENRE-FIT HIGHLIGHTS
    // ════════════════════════════════════════════════════════════════

    private void RefreshGenreHighlights()
    {
        if (ActiveGenre == null) return;
        foreach (var (card, feat) in _sidebarCards)
        {
            float fit = _scoring?.GenreFit(feat) ?? 0f;
            card.EnableInClassList("sidebar-card--recommended", fit >= 0.75f);
            card.EnableInClassList("sidebar-card--off-genre",   fit < 0.25f);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SIDEBAR CARD BUILDER
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildSidebarCard(FeatureSO f)
    {
        var card = new VisualElement();
        card.AddToClassList("sidebar-feature-card");
        card.AddToClassList("pillar-" + f.category.ToString().ToLower());
        card.userData = f;

        // Kind badge
        string kindTxt = IsSystemFeature(f) ? "SYSTEM" : IsSupportFeature(f) ? "SUPPORT" : "FEATURE";
        var badge = new Label(kindTxt) { pickingMode = PickingMode.Ignore };
        badge.AddToClassList("sidebar-type-badge");
        badge.AddToClassList("sidebar-type-badge--" + kindTxt.ToLower());
        card.Add(badge);

        // Name + year row
        var nameRow = RowEl();
        var name = new Label(f.isResearched ? f.featureName : "🔒 " + f.featureName)
            { pickingMode = PickingMode.Ignore };
        name.style.flexGrow = 1;
        name.style.fontSize = 11;

        var yr = new Label(f.releaseYear.ToString()) { pickingMode = PickingMode.Ignore };
        yr.AddToClassList("sidebar-year-tag");

        nameRow.Add(name);
        nameRow.Add(yr);
        card.Add(nameRow);

        // CPU bar + dev-time + stat row
        var statsRow = RowEl();
        statsRow.style.marginTop    = 4;
        statsRow.style.marginBottom = 2;

        var cpuOuter = new VisualElement() { pickingMode = PickingMode.Ignore };
        cpuOuter.AddToClassList("sidebar-cpu-bar-outer");

        var cpuFill = new VisualElement() { pickingMode = PickingMode.Ignore };
        cpuFill.AddToClassList("sidebar-cpu-fill");
        cpuFill.style.width = Length.Percent(Mathf.Clamp01(f.cpuUsage / Mathf.Max(1f, maxCpu)) * 100f);
        cpuOuter.Add(cpuFill);

        var cpuLbl = new Label($"CPU {f.cpuUsage:0}") { pickingMode = PickingMode.Ignore };
        cpuLbl.AddToClassList("sidebar-cpu-label");

        float dw = Mathf.Max(0.5f, f.cpuUsage * 0.12f);
        var devLbl = new Label($"⏱{dw:0.0}w") { pickingMode = PickingMode.Ignore };
        devLbl.AddToClassList("sidebar-devtime-label");

        int    sv = Mathf.CeilToInt(f.cpuUsage * 0.09f);
        string ic = f.category switch
        {
            FeatureSO.FeatureCategory.Gameplay  => "🎮",
            FeatureSO.FeatureCategory.Graphic   => "🎨",
            FeatureSO.FeatureCategory.Sound     => "🎵",
            FeatureSO.FeatureCategory.Tech      => "⚙",
            FeatureSO.FeatureCategory.Narrative => "📖",
            FeatureSO.FeatureCategory.UX        => "✦",
            _                                   => "+",
        };
        var statLbl = new Label($"{ic}+{sv}") { pickingMode = PickingMode.Ignore };
        statLbl.AddToClassList("sidebar-stat-label");
        statLbl.AddToClassList("sidebar-stat-" + f.category.ToString().ToLower());

        statsRow.Add(cpuOuter);
        statsRow.Add(cpuLbl);
        statsRow.Add(devLbl);
        statsRow.Add(statLbl);
        card.Add(statsRow);

        if (!f.isResearched)
        {
            card.AddToClassList("sidebar-feature-card--locked");
            return card;
        }

        WireSidebarDrag(card, f);
        card.RegisterCallback<MouseEnterEvent>(_ => ShowFeatureTooltip(f, card));
        card.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
        return card;
    }

    private static VisualElement RowEl()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        return row;
    }

    // ════════════════════════════════════════════════════════════════
    //  DRAG — Sidebar → Canvas
    // ════════════════════════════════════════════════════════════════

    private void WireSidebarDrag(VisualElement card, FeatureSO feature)
    {
        bool dragging = false;

        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            dragging            = true;
            _sidebarDragFeature = feature;
            _isSidebarDragging  = false;
            card.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || !card.HasPointerCapture(evt.pointerId)) return;
            if (!_isSidebarDragging)
            {
                _isSidebarDragging = true;
                ShowGhostCard(feature, evt.position);
            }
            MoveGhostCard(evt.position);
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging            = false;
            _sidebarDragFeature = null;
            card.ReleasePointer(evt.pointerId);
            HideGhostCard();

            if (_isSidebarDragging)
            {
                _isSidebarDragging = false;
                SpawnFeatureNode(feature, CanvasLocal(evt.position));
            }
            else
            {
                // Short click → open detail popup
                var center = new Vector2(_canvas.contentRect.width * .5f, _canvas.contentRect.height * .5f);
                OpenFeaturePopupForSidebar(feature, center);
            }
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWN
    // ════════════════════════════════════════════════════════════════

    internal void SpawnFeatureNode(FeatureSO f, Vector2 pos)
    {
        GameNode node;

        if (IsSupportFeature(f))
            node = new SupportNode(f) { CanvasPosition = pos };
        else if (IsSystemFeature(f))
            node = new SystemNode(f)  { CanvasPosition = pos };
        else
            node = new FeatureNode(f) { CanvasPosition = pos };

        _graph.AddNode(node);
        ShowToast($"✓ '{f.featureName}' hinzugefügt.");
    }

    private void SpawnOptimizer()
    {
        var pillar = ActiveGenre?.primaryCategory ?? FeatureSO.FeatureCategory.Gameplay;
        var opt    = new OptimizeNode(pillar) { CanvasPosition = CanvasCenter() };
        _graph.AddNode(opt);
        ShowToast("⚙ Optimizer hinzugefügt.");
    }

    private Vector2 CanvasCenter() =>
        new Vector2(_canvas?.contentRect.width * 0.5f ?? 400f,
                    _canvas?.contentRect.height * 0.5f ?? 300f);

    // ════════════════════════════════════════════════════════════════
    //  TOOLTIP
    // ════════════════════════════════════════════════════════════════

    private void ShowFeatureTooltip(FeatureSO f, VisualElement anchor)
    {
        if (_tooltip == null) return;
        _tooltip.RemoveFromClassList("hidden");

        _tooltip.Q<Label>("TooltipName")?.SetText(f.featureName);
        _tooltip.Q<Label>("TooltipYear")?.SetText(f.releaseYear.ToString());
        _tooltip.Q<Label>("TooltipCpu")?.SetText($"CPU: {f.cpuUsage:0}");
        _tooltip.Q<Label>("TooltipDesc")?.SetText(f.description);

        // Position right of sidebar, clamped to screen
        var anchorWorld = anchor.worldBound;
        _tooltip.style.top  = Mathf.Clamp(anchorWorld.y, 4f, Screen.height - _tooltip.contentRect.height - 4f);
        _tooltip.style.left = anchorWorld.xMax + 6f;
    }

    private void HideTooltip() => _tooltip?.AddToClassList("hidden");
}

// ── Extension helper to avoid null-label crashes ──────────────
internal static class LabelEx
{
    public static void SetText(this Label l, string text) { if (l != null) l.text = text; }
}