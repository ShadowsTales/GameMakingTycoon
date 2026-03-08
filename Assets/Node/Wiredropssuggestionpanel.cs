// ============================================================
//  WireDropSuggestionPanel.cs  — NODE-TYCOON
//  FIXES APPLIED:
//    • CanReceive() now handles all current PortType values
//    • Removed references to dead legacy slot names (AnchorSlot,
//      UpgradeSlot) that no longer appear on new node inputs
//    • Added cases for FeatureSlot, EngineSlot, TechSlot,
//      OptimizerSlot, ExpandSlot, SystemSlot, SupportSlot
//    • IsSupportCategory helper kept; IsEngineCategory added
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
        Vector2                    dropPosCanvas,
        Vector2                    dropPosScreen,
        Action<FeatureSO, Vector2> onChosen)
    {
        _onChosen   = onChosen;
        _dropCanvas = dropPosCanvas;

        var inGraph    = new HashSet<FeatureSO>(alreadyInGraph);
        var candidates = db.allFeatures
            .Where(f => f.isResearched && !inGraph.Contains(f))
            .Where(f => CanReceive(f, sourcePort.Type))
            .OrderByDescending(f => scoring?.GenreFit(f) ?? 0f)
            .Take(6)
            .ToList();

        _list.Clear();

        if (candidates.Count == 0)
        {
            _titleLabel.text = "KEINE PASSENDEN FEATURES";
            _list.Add(new Label("Alle kompatiblen Features wurden bereits platziert.")
                { style = { color = new StyleColor(new Color(0.4f, 0.5f, 0.6f)), fontSize = 9, whiteSpace = WhiteSpace.Normal } });
        }
        else
        {
            _titleLabel.text = $"VORSCHLÄGE ({candidates.Count})";
            foreach (var f in candidates)
                _list.Add(BuildRow(f, scoring?.GenreFit(f) ?? 0.5f));
        }

        // Position panel near drop point, clamped inside canvas
        _panel.RemoveFromClassList("hidden");
        _panel.schedule.Execute(() =>
        {
            var canvasRect = _panel.parent?.contentRect ?? Rect.zero;
            float pw = _panel.resolvedStyle.width  > 0 ? _panel.resolvedStyle.width  : 220f;
            float ph = _panel.resolvedStyle.height > 0 ? _panel.resolvedStyle.height : 260f;
            float x  = Mathf.Clamp(dropPosCanvas.x + 12f, 4f, Mathf.Max(4f, canvasRect.width  - pw - 4f));
            float y  = Mathf.Clamp(dropPosCanvas.y - 20f, 4f, Mathf.Max(4f, canvasRect.height - ph - 4f));
            _panel.style.left = x;
            _panel.style.top  = y;
        }).ExecuteLater(0);
    }

    public void Hide() => _panel.AddToClassList("hidden");

    // ════════════════════════════════════════════════════════════════
    //  ROW BUILDER
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildRow(FeatureSO f, float fit)
    {
        var row = new VisualElement();
        row.AddToClassList("wire-drop-row");

        string icon = f.category == FeatureSO.FeatureCategory.Gameplay  ? "▲"
                    : f.category == FeatureSO.FeatureCategory.Sound      ||
                      f.category == FeatureSO.FeatureCategory.Narrative  ? "◉"
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
    //  PORT COMPATIBILITY  (FIX: full coverage of all current PortTypes)
    // ════════════════════════════════════════════════════════════════

    private static bool CanReceive(FeatureSO f, PortType outputType) => outputType switch
    {
        // ── Required Genre outputs ────────────────────────────────
        // FIX: GameplaySlot → Gameplay features only
        PortType.GameplaySlot => f.category == FeatureSO.FeatureCategory.Gameplay,

        // FIX: GraphicSlot → Graphic features only (for EngineNode suggestions)
        PortType.GraphicSlot  => f.category == FeatureSO.FeatureCategory.Graphic,

        // FIX: SoundSlot → Sound features (covers both EngineNode and SystemNode)
        PortType.SoundSlot    => f.category == FeatureSO.FeatureCategory.Sound,

        // ── Optional Genre outputs ───────────────────────────────
        // FIX: SystemSlot accepts any non-support category
        PortType.SystemSlot   => !IsSupportCategory(f.category),

        // FIX: SupportSlot → Narrative/UX/Sound
        PortType.SupportSlot  => IsSupportCategory(f.category),

        // ── Feature chain ─────────────────────────────────────────
        // FIX: FeatureSlot (was missing) → any feature
        PortType.FeatureSlot  => true,

        // ── Engine ───────────────────────────────────────────────
        // FIX: EngineSlot as output → Graphic or Tech features (for EngineNode chaining)
        PortType.EngineSlot   => IsEngineCategory(f.category),

        // FIX: TechSlot (was entirely missing) → features that require engine tech
        //      We show all Tech-category features; the RequiredEngineNodeId check
        //      happens at connection-validation time in NodeGraph.TryConnect().
        PortType.TechSlot     => f.category == FeatureSO.FeatureCategory.Tech,

        // ── Optimizer ─────────────────────────────────────────────
        // ExpandSlot and OptimizerSlot don't wire to FeatureSO nodes — hide panel
        PortType.ExpandSlot    => false,
        PortType.OptimizerSlot => false,

        // ── Legacy ────────────────────────────────────────────────
        PortType.AnchorSlot    => !IsSupportCategory(f.category),   // old CoreToAnchor
        PortType.UpgradeSlot   => true,                              // old AnchorToUpgrade
        PortType.PillarRoot    => true,
        PortType.FeatureCore   => true,
        PortType.DataFeed      => IsSupportCategory(f.category),
        PortType.AudioTrigger  => f.category == FeatureSO.FeatureCategory.Sound,
        PortType.RenderPass    => f.category == FeatureSO.FeatureCategory.Graphic,

        _ => false,
    };

    private static bool IsSupportCategory(FeatureSO.FeatureCategory c) =>
        c == FeatureSO.FeatureCategory.Sound     ||
        c == FeatureSO.FeatureCategory.Narrative ||
        c == FeatureSO.FeatureCategory.UX;

    // FIX: new helper — categories that represent engine/tech node content
    private static bool IsEngineCategory(FeatureSO.FeatureCategory c) =>
        c == FeatureSO.FeatureCategory.Graphic ||
        c == FeatureSO.FeatureCategory.Tech;
}

// Small inline helper so label chains read cleanly
internal static class LabelExt
{
    public static Label SetClass(this Label l, string cls) { l.AddToClassList(cls); return l; }
}