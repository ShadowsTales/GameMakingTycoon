// ============================================================
//  WireDropSuggestionPanel.cs  — NODE-TYCOON
//
//  Appears when the player releases a wire on empty canvas.
//  Shows Top-6 compatible features, sorted by genre-fit.
//
//  POSITIONING FIX:
//    The panel is always kept within the canvas bounds.
//    It prefers to appear near the drop point but clamps so
//    it never overflows the canvas edges, and falls back to
//    center if the canvas rect isn't available yet.
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class WireDropSuggestionPanel
{
    private readonly VisualElement _panel;
    private readonly VisualElement _list;
    private readonly Label         _titleLabel;

    private Action<FeatureSO, Vector2> _onChosen;
    private Vector2                    _dropCanvas;

    public bool IsVisible => !_panel.ClassListContains("hidden");

    // ════════════════════════════════════════════════════════════════
    //  CONSTRUCTION  — panel lives INSIDE the canvas element
    // ════════════════════════════════════════════════════════════════

    public static WireDropSuggestionPanel Build(VisualElement canvas)
        => new WireDropSuggestionPanel(canvas);

    private WireDropSuggestionPanel(VisualElement canvas)
    {
        _panel = new VisualElement { name = "WireDropPanel" };
        _panel.AddToClassList("wire-drop-panel");
        _panel.AddToClassList("hidden");
        _panel.style.position = Position.Absolute;

        var header = new VisualElement();
        header.AddToClassList("wire-drop-header");

        _titleLabel = new Label("VORSCHLÄGE");
        _titleLabel.AddToClassList("wire-drop-title");
        header.Add(_titleLabel);

        var close = new Button(Hide) { text = "×" };
        close.AddToClassList("wire-drop-close");
        header.Add(close);
        _panel.Add(header);

        _list = new VisualElement();
        _list.AddToClassList("wire-drop-list");
        _panel.Add(_list);

        var hint = new Label("Klicken um Feature direkt anzuhängen");
        hint.AddToClassList("wire-drop-hint");
        _panel.Add(hint);

        canvas.Add(_panel);
    }

    // ════════════════════════════════════════════════════════════════
    //  SHOW
    // ════════════════════════════════════════════════════════════════

    public void Show(
        NodePort                   sourcePort,
        FeatureDatabase            db,
        NodeScoringService         scoring,
        IEnumerable<FeatureSO>     alreadyInGraph,
        Vector2                    dropPosCanvas,   // canvas-local (zoomed) coords
        Vector2                    dropPosScreen,   // screen-space (for initial pos estimate)
        Action<FeatureSO, Vector2> onChosen)
    {
        _onChosen    = onChosen;
        _dropCanvas  = dropPosCanvas;

        var inGraph    = new HashSet<FeatureSO>(alreadyInGraph);
        var candidates = db.allFeatures
            .Where(f => f.isResearched && !inGraph.Contains(f))
            .Where(f => CanReceive(f, sourcePort.Type))
            .OrderByDescending(f => scoring?.GenreFit(f) ?? 0.5f)
            .Take(6)
            .ToList();

        _list.Clear();
        if (candidates.Count == 0)
        {
            var empty = new Label("Keine passenden Features verfügbar.");
            empty.AddToClassList("wire-drop-empty");
            _list.Add(empty);
        }
        else
        {
            _titleLabel.text = $"VORSCHLÄGE ({candidates.Count})";
            foreach (var f in candidates)
                _list.Add(BuildRow(f, scoring?.GenreFit(f) ?? 0.5f));
        }

        // ── POSITION within canvas ───────────────────────────────
        // The panel is an absolute child of the canvas element.
        // We use canvas-local coordinates so it moves with pan/zoom.
        // Use schedule to position after layout is calculated.
        _panel.RemoveFromClassList("hidden");
        _panel.schedule.Execute(() => PositionPanel(dropPosCanvas)).ExecuteLater(0);
    }

    public void Hide()
    {
        _panel.AddToClassList("hidden");
        _list.Clear();
    }

    // ── Panel clamped positioning ─────────────────────────────────
    private void PositionPanel(Vector2 canvasLocal)
    {
        var canvas = _panel.parent;
        if (canvas == null) return;

        float pw = _panel.resolvedStyle.width  > 0 ? _panel.resolvedStyle.width  : 250f;
        float ph = _panel.resolvedStyle.height > 0 ? _panel.resolvedStyle.height : 320f;
        var   cr = canvas.contentRect;

        // Prefer near drop point, shifted right and slightly up
        float x = canvasLocal.x + 10f;
        float y = canvasLocal.y - ph * 0.5f;

        // Clamp so panel stays inside canvas
        x = Mathf.Clamp(x, 8f, cr.width  - pw - 8f);
        y = Mathf.Clamp(y, 8f, cr.height - ph - 8f);

        // If canvas is too small (pre-layout), fall back to center
        if (cr.width < 50f || cr.height < 50f)
        {
            x = (cr.width  - pw) * 0.5f;
            y = (cr.height - ph) * 0.5f;
        }

        _panel.style.left = x;
        _panel.style.top  = y;
    }

    // ════════════════════════════════════════════════════════════════
    //  ROW BUILDER
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildRow(FeatureSO f, float fit)
    {
        var row = new VisualElement();
        row.AddToClassList("wire-drop-row");
        row.AddToClassList("pillar-" + f.category.ToString().ToLower());

        string icon = f.cpuUsage >= 15f && f.prerequisites.Count == 0 ? "▲"
                    : f.category == FeatureSO.FeatureCategory.Sound ||
                      f.category == FeatureSO.FeatureCategory.Narrative ? "◉"
                    : "◆";
        var iconLbl = new Label(icon);
        iconLbl.AddToClassList("wire-drop-row-icon");
        row.Add(iconLbl);

        var info = new VisualElement();
        info.AddToClassList("wire-drop-row-info");
        info.Add(new Label(f.featureName) { name = "n" }.SetClass("wire-drop-row-name"));

        float dw = Mathf.Max(0.5f, f.cpuUsage * 0.12f);
        int   sv = Mathf.CeilToInt(f.cpuUsage * 0.09f);
        info.Add(new Label($"CPU {f.cpuUsage:0}  ·  ⏱{dw:0.0}w  ·  +{sv}").SetClass("wire-drop-row-meta"));
        row.Add(info);

        string stars = fit >= 0.7f ? "★★★" : fit >= 0.4f ? "★★☆" : "★☆☆";
        var fitLbl = new Label(stars).SetClass("wire-drop-row-fit");
        if (fit >= 0.7f) fitLbl.AddToClassList("fit-high");
        else if (fit < 0.35f) fitLbl.AddToClassList("fit-low");
        row.Add(fitLbl);

        row.RegisterCallback<ClickEvent>(_ => { Hide(); _onChosen?.Invoke(f, _dropCanvas); });
        row.RegisterCallback<MouseEnterEvent>(_ => row.AddToClassList("wire-drop-row--hover"));
        row.RegisterCallback<MouseLeaveEvent>(_ => row.RemoveFromClassList("wire-drop-row--hover"));
        return row;
    }

    // ════════════════════════════════════════════════════════════════
    //  PORT COMPATIBILITY
    // ════════════════════════════════════════════════════════════════

    private static bool CanReceive(FeatureSO f, PortType outputType) => outputType switch
    {
        // Core required outputs → only matching category
        PortType.GameplaySlot => f.category == FeatureSO.FeatureCategory.Gameplay,
        PortType.GraphicSlot  => f.category == FeatureSO.FeatureCategory.Graphic,
        PortType.SoundSlot    => f.category == FeatureSO.FeatureCategory.Sound,

        // Optional core slots
        PortType.AnchorSlot   => true,   // any anchor
        PortType.SupportSlot  => IsSupportCategory(f.category),

        // Feature chain
        PortType.UpgradeSlot  => true,   // any upgrade

        // Legacy — permissive
        PortType.PillarRoot   => true,
        PortType.FeatureCore  => true,
        PortType.DataFeed     => IsSupportCategory(f.category),
        PortType.AudioTrigger => f.category == FeatureSO.FeatureCategory.Sound,
        PortType.RenderPass   => f.category == FeatureSO.FeatureCategory.Graphic,

        _ => false,
    };

    private static bool IsSupportCategory(FeatureSO.FeatureCategory c) =>
        c == FeatureSO.FeatureCategory.Sound     ||
        c == FeatureSO.FeatureCategory.Narrative ||
        c == FeatureSO.FeatureCategory.UX;
}

// Small inline helper so label chains read cleanly
internal static class LabelExt
{
    public static Label SetClass(this Label l, string cls) { l.AddToClassList(cls); return l; }
}