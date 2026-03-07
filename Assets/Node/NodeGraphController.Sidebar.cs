// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Sidebar.cs  — NODE-TYCOON
//
//  TWO-LEVEL FILTER SYSTEM
//  ─────────────────────────────────────────────────────────────
//  Level 1 — Node-Type tabs (top of sidebar):
//    ▲ ANKER    → Gameplay / Graphic / Tech features, CPU≥15, no prereqs
//    ◆ UPGRADE  → Features with prereqs OR CPU<15 (sub-features)
//    ◉ SUPPORT  → Sound / Narrative / UX  (global systems)
//    ⚙ OPTIM.  → Spawns an OptimizerNode immediately
//
//  Level 2 — Category filter row (below tabs):
//    Buttons: ALLE | GP | GFX | SND | TECH | NARR | UX
//    Only shows categories relevant to the current tab.
//    Inactive buttons dim if they have no items in this tab.
//
//  TOOLTIP
//  ─────────────────────────────────────────────────────────────
//  • Opaque dark panel (no transparency)
//  • Pinned to the right edge of the sidebar, vertically near
//    the hovered card, clamped so it never leaves the screen
//  • Shows: name, type, category, year, CPU, dev-time, stat,
//    prereqs, synergies, conflicts, genre-fit
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
    private NodeKind                   _sidebarKind = NodeKind.Anchor;
    private FeatureSO.FeatureCategory? _sidebarCat  = null;  // null = show all

    // Names of category buttons for easy lookup
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
    //  BUILD
    // ════════════════════════════════════════════════════════════════

    private void BuildSidebar(VisualElement root)
    {
        _sidebar     = root.Q<VisualElement>("NodeGraphSidebar");
        _featureList = root.Q<ScrollView>("FeatureList");
        if (_featureList == null) return;
        _sidebarCards.Clear();

        WireSidebarTabs(root);
        WireCatFilterRow(root);

        var sorted = featureDB.allFeatures
            .OrderByDescending(f => f.isResearched)
            .ThenBy(f => f.releaseYear)
            .ThenBy(f => f.featureName)
            .ToList();

        foreach (var f in sorted)
        {
            var card = BuildSidebarCard(f);
            _featureList.Add(card);
            _sidebarCards.Add((card, f));
        }

        ApplyFilter();
    }

    // ── Public API for UIController.WirePillarTabs ─────────────────
    public void FilterSidebarByPillar(FeatureSO.FeatureCategory? pillar)
    {
        _sidebarCat = pillar;
        UpdateCatHighlights(_sidebar);
        ApplyFilter();
    }

    // ════════════════════════════════════════════════════════════════
    //  TAB WIRING
    // ════════════════════════════════════════════════════════════════

    private void WireSidebarTabs(VisualElement root)
    {
        var tabs = new (string name, NodeKind kind, string desc)[]
        {
            ("SidebarTabAnker",     NodeKind.Anchor,    "Haupt-Features die den Spieltyp definieren"),
            ("SidebarTabUpgrade",   NodeKind.Upgrade,   "Verfeinerungen die an Anker andocken"),
            ("SidebarTabSupport",   NodeKind.Support,   "Globale Systeme: Sound, Story, UX"),
            ("SidebarTabOptimizer", NodeKind.Optimizer, ""),
        };
        var desc = root.Q<Label>("SidebarTabDesc");

        foreach (var (name, kind, d) in tabs)
        {
            var btn = root.Q<Button>(name);
            if (btn == null) continue;
            var captKind = kind; var captDesc = d;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                if (captKind == NodeKind.Optimizer) { SpawnOptimizer(); return; }

                foreach (var (n, _, _) in tabs)
                    root.Q<Button>(n)?.RemoveFromClassList("sidebar-tab--active");
                btn.AddToClassList("sidebar-tab--active");
                if (desc != null) desc.text = captDesc;

                _sidebarKind = captKind;
                _sidebarCat  = null;    // reset category on tab switch
                UpdateCatVisibility(root, captKind);
                UpdateCatHighlights(root);
                ApplyFilter();
            });
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  CATEGORY FILTER ROW
    // ════════════════════════════════════════════════════════════════

    private void WireCatFilterRow(VisualElement root)
    {
        foreach (var (btnName, cat) in CatBtns)
        {
            var btn = root.Q<Button>(btnName);
            if (btn == null) continue;
            var captCat = cat;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                // Toggle: clicking active cat resets to ALL
                _sidebarCat = (_sidebarCat == captCat) ? null : captCat;
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
        {
            var btn = root.Q<Button>(btnName);
            btn?.EnableInClassList("filter-active", cat == _sidebarCat);
        }
    }

    // Dim buttons that have no matching features in the current tab
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
            if (cat == null) continue;   // "ALLE" always visible
            var btn = root.Q<Button>(btnName);
            bool empty = !presentCats.Contains(cat.Value);
            btn?.EnableInClassList("filter-btn-empty", empty);
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

    private bool IsKind(FeatureSO f, NodeKind k) => k switch
    {
        NodeKind.Anchor  => IsAnchorFeature(f),
        NodeKind.Upgrade => IsUpgradeFeature(f),
        NodeKind.Support => IsSupportFeature(f),
        _                => false,
    };

     /*── Classification ───────────────────────────────────────────
    private static bool IsAnchorFeature(FeatureSO f) =>
        (f.category == FeatureSO.FeatureCategory.Gameplay ||
         f.category == FeatureSO.FeatureCategory.Graphic  ||
         f.category == FeatureSO.FeatureCategory.Tech)
        && f.cpuUsage >= 15f && f.prerequisites.Count == 0;
    */
    private static bool IsUpgradeFeature(FeatureSO f) =>
        !IsSupportFeature(f) && (f.cpuUsage < 15f || f.prerequisites.Count > 0);

/*
    private static bool IsSupportFeature(FeatureSO f) =>
        f.category == FeatureSO.FeatureCategory.Sound     ||
        f.category == FeatureSO.FeatureCategory.Narrative ||
        f.category == FeatureSO.FeatureCategory.UX;

    */
     
    //  SIDEBAR CARD
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildSidebarCard(FeatureSO f)
    {
        var card = new VisualElement();
        card.AddToClassList("sidebar-feature-card");
        card.AddToClassList("pillar-" + f.category.ToString().ToLower());
        card.userData = f;

        // Type badge
        string typeTxt = IsAnchorFeature(f) ? "ANKER" : IsSupportFeature(f) ? "SUPPORT" : "UPGRADE";
        var badge = new Label(typeTxt) { pickingMode = PickingMode.Ignore };
        badge.AddToClassList("sidebar-type-badge");
        badge.AddToClassList("sidebar-type-badge--" + typeTxt.ToLower());
        card.Add(badge);

        // Name + year
        var nameRow = RowEl();
        var name = new Label(f.isResearched ? f.featureName : "🔒 " + f.featureName)
            { pickingMode = PickingMode.Ignore };
        name.style.flexGrow = 1;
        name.style.fontSize = 11;
        var yr = new Label(f.releaseYear.ToString()) { pickingMode = PickingMode.Ignore };
        yr.AddToClassList("sidebar-year-tag");
        nameRow.Add(name); nameRow.Add(yr);
        card.Add(nameRow);

        // CPU bar + dev-time + stat
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

        int sv = Mathf.CeilToInt(f.cpuUsage * 0.09f);
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

        statsRow.Add(cpuOuter); statsRow.Add(cpuLbl);
        statsRow.Add(devLbl);   statsRow.Add(statLbl);
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
            if (!_isSidebarDragging) { _isSidebarDragging = true; ShowGhostCard(feature, evt.position); }
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

    internal void SpawnFeatureNode(FeatureSO f, Vector2 pos)
    {
        GameNode node = IsSupportFeature(f) ? (GameNode)new SupportNode(f) { CanvasPosition = pos }
                      : IsAnchorFeature(f)  ?           new AnchorNode(f)  { CanvasPosition = pos }
                      :                                  new UpgradeNode(f) { CanvasPosition = pos };
        _graph.AddNode(node);
        ShowToast($"'{f.featureName}' als {node.Kind} hinzugefügt.");
    }

    private void ShowGhostCard(FeatureSO f, Vector2 pos)
    {
        if (_ghostCard == null) return;
        _ghostCard.Clear();
        _ghostCard.RemoveFromClassList("hidden");
        foreach (var c in new[]{"pillar-gameplay","pillar-graphic","pillar-sound","pillar-tech","pillar-narrative","pillar-ux"})
            _ghostCard.RemoveFromClassList(c);
        _ghostCard.AddToClassList("pillar-" + f.category.ToString().ToLower());
        _ghostCard.Add(new Label(f.featureName) { pickingMode = PickingMode.Ignore,
            style = { color = Color.white, fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold } });
        _ghostCard.Add(new Label($"CPU {f.cpuUsage:0}") { pickingMode = PickingMode.Ignore,
            style = { fontSize = 9 } });
        MoveGhostCard(pos);
    }
    private void MoveGhostCard(Vector2 s)
    {
        if (_ghostCard == null) return;
        var l = _canvas.WorldToLocal(s);
        _ghostCard.style.left = l.x + 12f; _ghostCard.style.top = l.y + 12f;
        _ghostCard.style.display = DisplayStyle.Flex;
    }
    private void HideGhostCard() { if (_ghostCard != null) _ghostCard.style.display = DisplayStyle.None; }

    // ════════════════════════════════════════════════════════════════
    //  GENRE HIGHLIGHTS
    // ════════════════════════════════════════════════════════════════

    private void RefreshGenreHighlights()
    {
        foreach (var (card, f) in _sidebarCards)
        {
            card.RemoveFromClassList("sidebar-card--recommended");
            card.RemoveFromClassList("sidebar-card--off-genre");
            if (ActiveGenre == null || !f.isResearched) continue;
            float fit = _scoring?.GenreFit(f) ?? 0.5f;
            if      (fit >= 0.7f) card.AddToClassList("sidebar-card--recommended");
            else if (fit < 0.3f)  card.AddToClassList("sidebar-card--off-genre");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  TOOLTIP — opaque, pinned just right of sidebar
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildTooltipElement()
    {
        var tt = new VisualElement();
        tt.AddToClassList("feature-tooltip");
        tt.AddToClassList("hidden");
        tt.pickingMode = PickingMode.Ignore;

        tt.Add(TLbl("TT_Name",       13, Color.white, bold: true));
        tt.Add(TLbl("TT_Meta",        9, new Color(0.55f, 0.70f, 0.85f)));
        tt.Add(Sep());
        tt.Add(TLbl("TT_Desc",       10, new Color(0.82f, 0.87f, 0.93f), wrap: true));
        tt.Add(Sep());
        tt.Add(TLbl("TT_Stats",       9, new Color(0.50f, 0.92f, 0.60f)));
        tt.Add(TLbl("TT_Prereqs",     9, new Color(0.95f, 0.75f, 0.28f)));
        tt.Add(TLbl("TT_Synergies",   9, new Color(0.40f, 0.88f, 0.60f)));
        tt.Add(TLbl("TT_Conflicts",   9, new Color(0.95f, 0.42f, 0.32f)));
        tt.Add(Sep());
        tt.Add(TLbl("TT_GenreFit",    9, new Color(0.95f, 0.82f, 0.10f)));
        return tt;
    }

    private void ShowFeatureTooltip(FeatureSO f, VisualElement card)
    {
        if (_tooltip == null) return;
        _tooltip.RemoveFromClassList("hidden");

        string kind = IsAnchorFeature(f) ? "▲ ANKER" : IsSupportFeature(f) ? "◉ SUPPORT" : "◆ UPGRADE";
        TT("TT_Name",  f.featureName);
        TT("TT_Meta",  $"{kind}  ·  {f.category}  ·  {f.releaseYear}");
        TT("TT_Desc",  string.IsNullOrWhiteSpace(f.description) ? "Keine Beschreibung." : f.description);

        float dw   = Mathf.Max(0.5f, f.cpuUsage * 0.12f);
        int   stat = Mathf.CeilToInt(f.cpuUsage * 0.09f);
        TT("TT_Stats",  $"CPU {f.cpuUsage:0}  ·  ⏱ {dw:0.0} Wo.  ·  +{stat} {f.category}");

        TT("TT_Prereqs", f.prerequisites.Any()
            ? "⚠ Benötigt: " + string.Join(", ", f.prerequisites.Select(p => p.featureName))
            : "✓ Keine Voraussetzungen");

        TT("TT_Synergies", f.synergyWith?.Any() == true
            ? "⬆ Synergie: " + string.Join(", ", f.synergyWith.Select(s => s.featureName))
            : "");

        TT("TT_Conflicts", f.conflictsWith?.Any() == true
            ? "✗ Konflikt: " + string.Join(", ", f.conflictsWith.Select(c => c.featureName))
            : "");

        float fit = _scoring?.GenreFit(f) ?? 0.5f;
        TT("TT_GenreFit", fit >= 0.7f ? $"★★★ Empfohlen ({fit:0%})"
                        : fit >= 0.4f ? $"★★☆ Neutral ({fit:0%})"
                                      : $"★☆☆ Schwach ({fit:0%})");

        // ── Position: RIGHT edge of sidebar, vertically near card ──
        if (_sidebar != null)
        {
            var sb   = _sidebar.worldBound;
            var cb   = card.worldBound;
            var root = _tooltip.parent;

            // X: right of sidebar + small gap, converted to local space of parent
            float ttLeft = root != null
                ? root.WorldToLocal(new Vector2(sb.xMax + 6f, 0)).x
                : sb.xMax + 6f;

            // Y: align top to card top, clamped
            float ttTop = root != null
                ? root.WorldToLocal(new Vector2(0, cb.yMin)).y
                : cb.yMin;

            float ttH = 230f;
            if (root != null) ttTop = Mathf.Clamp(ttTop, 6f, root.contentRect.height - ttH - 6f);

            _tooltip.style.left = ttLeft;
            _tooltip.style.top  = ttTop;
        }
    }

    private void HideTooltip() => _tooltip?.AddToClassList("hidden");

    // ── Helpers ──────────────────────────────────────────────────

    private static VisualElement RowEl()
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems    = Align.Center;
        r.pickingMode         = PickingMode.Ignore;
        return r;
    }

    private static Label TLbl(string n, int sz, Color col, bool bold=false, bool wrap=false)
    {
        var l = new Label { name = n };
        l.style.fontSize   = sz; l.style.color = col;
        l.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
        l.style.marginBottom = 2;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.pickingMode = PickingMode.Ignore;
        return l;
    }

    private static VisualElement Sep()
    {
        var s = new VisualElement();
        s.style.height = 1f; s.style.marginTop = 4f; s.style.marginBottom = 4f;
        s.style.backgroundColor = new Color(0.22f, 0.32f, 0.44f, 0.7f);
        s.pickingMode = PickingMode.Ignore;
        return s;
    }

    private void TT(string n, string t) { var l = _tooltip?.Q<Label>(n); if (l != null) l.text = t; }
}

public static class StringExtensions
{
    public static string NullIfEmpty(this string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}