// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Nodes.cs  — NODE-TYCOON
//
//  Builds visual cards for all 5 node types.
//  Each feature card shows:
//    • Header: name + × delete button
//    • Kind badge  (CORE / ANKER / UPGRADE / SUPPORT / OPTIMIZER)
//    • Stats block: CPU bar, dev-time, stat-bonus
//    • Port rows with clear directional labels
//    • Required-slot status lights on CoreNode (✓ / ✗)
//    • Prerequisite warning if prereqs not yet placed
// ════════════════════════════════════════════════════════════════
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController
{
    // ════════════════════════════════════════════════════════════════
    //  CARD FACTORY
    // ════════════════════════════════════════════════════════════════

    private VisualElement CreateNodeCard(GameNode node)
    {
        var card = new VisualElement();
        card.name = "node_" + node.NodeId;
        card.AddToClassList("graph-node");
        card.AddToClassList("node-kind-" + node.Kind.ToString().ToLower());

        // Pillar accent only for feature nodes
        if (node.Kind != NodeKind.Core && node.Kind != NodeKind.Optimizer)
            card.AddToClassList("node-pillar-" + node.Pillar.ToString().ToLower());

        card.style.position = Position.Absolute;
        card.style.left     = node.CanvasPosition.x;
        card.style.top      = node.CanvasPosition.y;
        card.userData       = node;

        card.Add(BuildNodeHeader(node));
        card.Add(BuildKindBadge(node));

        // Stats block varies by type
        switch (node)
        {
            case CoreNode      cn:  card.Add(BuildCoreStats(cn));                                       break;
            case OptimizerNode opt: card.Add(BuildOptimizerStats(opt));                                 break;
            case AnchorNode    an:  card.Add(BuildFeatureStats(an.FeatureData, an.DevWeeks, an.PrimaryStatBonus));  break;
            case UpgradeNode   un:  card.Add(BuildFeatureStats(un.FeatureData, un.DevWeeks, un.PrimaryStatBonus));  break;
            case SupportNode   sn:  card.Add(BuildFeatureStats(sn.FeatureData, sn.DevWeeks, sn.PrimaryStatBonus));  break;
        }

        card.Add(BuildPortBody(node));

        // Prereq warning
        FeatureSO feat = GetFeature(node);
        if (feat?.prerequisites.Count > 0)
            card.Add(BuildPrereqWarning(feat));

        // Events
        MakeNodeDraggable(card, node);
        card.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.clickCount == 2 && feat != null)
                OpenFeaturePopupForNode(node.NodeId);
            else
            {
                evt.StopPropagation();
                SelectNode(node.NodeId);
            }
        });

        if (feat != null)
        {
            card.RegisterCallback<MouseEnterEvent>(_ => ShowFeatureTooltip(feat, card));
            card.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
        }
        return card;
    }

    private static FeatureSO GetFeature(GameNode node) => node switch
    {
        AnchorNode  a => a.FeatureData,
        UpgradeNode u => u.FeatureData,
        SupportNode s => s.FeatureData,
        _             => null
    };

    // ════════════════════════════════════════════════════════════════
    //  HEADER
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildNodeHeader(GameNode node)
    {
        var h = new VisualElement();
        h.AddToClassList("node-header");
        h.pickingMode = PickingMode.Ignore;

        var title = new Label(node.DisplayName) { pickingMode = PickingMode.Ignore };
        title.AddToClassList("node-title");
        h.Add(title);

        if (node.Kind != NodeKind.Core)
        {
            var del = new Button(() => _graph.RemoveNode(node.NodeId)) { text = "×" };
            del.AddToClassList("node-delete-btn");
            h.Add(del);
        }
        return h;
    }

    // ════════════════════════════════════════════════════════════════
    //  KIND BADGE
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildKindBadge(GameNode node)
    {
        string text = node.Kind switch
        {
            NodeKind.Core      => "● CORE",
            NodeKind.Anchor    => "▲ ANKER",
            NodeKind.Upgrade   => "◆ UPGRADE",
            NodeKind.Support   => "◉ SUPPORT",
            NodeKind.Optimizer => "⚙ OPTIMIZER",
            _                  => "",
        };
        var b = new Label(text) { pickingMode = PickingMode.Ignore };
        b.AddToClassList("node-kind-badge");
        b.AddToClassList("node-kind-badge-" + node.Kind.ToString().ToLower());
        return b;
    }

    // ════════════════════════════════════════════════════════════════
    //  STATS BLOCKS
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildFeatureStats(FeatureSO feat, float devWeeks, StatBonus stat)
    {
        var box = new VisualElement();
        box.AddToClassList("node-stats-box");
        box.pickingMode = PickingMode.Ignore;

        // ── CPU bar ──────────────────────────────────────────────
        var cpuRow = new VisualElement();
        cpuRow.AddToClassList("node-cpu-row");
        cpuRow.pickingMode = PickingMode.Ignore;

        var fill = new VisualElement() { pickingMode = PickingMode.Ignore };
        fill.AddToClassList("node-cpu-fill");
        float pct = Mathf.Clamp01(feat.cpuUsage / Mathf.Max(1f, maxCpu));
        fill.style.width = Length.Percent(pct * 100f);
        if (pct > 0.65f) fill.AddToClassList("node-cpu-fill--high");
        if (pct > 0.85f) fill.AddToClassList("node-cpu-fill--critical");

        var cpuLbl = new Label($"CPU {feat.cpuUsage:0}") { pickingMode = PickingMode.Ignore };
        cpuLbl.AddToClassList("node-cpu-label");
        cpuRow.Add(fill); cpuRow.Add(cpuLbl);
        box.Add(cpuRow);

        // ── Dev-time + Stat-bonus row ────────────────────────────
        var metaRow = new VisualElement();
        metaRow.AddToClassList("node-meta-row");
        metaRow.pickingMode = PickingMode.Ignore;

        var devLbl = new Label($"⏱ {devWeeks:0.0} Wo.") { pickingMode = PickingMode.Ignore };
        devLbl.AddToClassList("node-devtime-label");
        metaRow.Add(devLbl);

        if (stat.Value > 0)
        {
            string icon = stat.Category switch
            {
                FeatureSO.FeatureCategory.Gameplay  => "🎮",
                FeatureSO.FeatureCategory.Graphic   => "🎨",
                FeatureSO.FeatureCategory.Sound     => "🎵",
                FeatureSO.FeatureCategory.Tech      => "⚙",
                FeatureSO.FeatureCategory.Narrative => "📖",
                FeatureSO.FeatureCategory.UX        => "✦",
                _                                   => "+",
            };
            var statLbl = new Label($"{icon} +{stat.Value}") { pickingMode = PickingMode.Ignore };
            statLbl.AddToClassList("node-stat-label");
            statLbl.AddToClassList("node-stat-" + stat.Category.ToString().ToLower());
            metaRow.Add(statLbl);
        }
        box.Add(metaRow);
        return box;
    }

    private VisualElement BuildCoreStats(CoreNode cn)
    {
        var box = new VisualElement();
        box.AddToClassList("core-stats-box");
        box.pickingMode = PickingMode.Ignore;

        box.Add(MiniStat(cn.Platform, "stat-platform"));
        box.Add(MiniStat($"Budget: {cn.CpuBudget:0} CPU", "stat-budget"));

        // Required slot status lights
        box.Add(BuildSlotStatus("⚡ GAMEPLAY", cn.GameplayOut.IsConnected));
        box.Add(BuildSlotStatus("🎨 GRAFIK",   cn.GraphicOut.IsConnected));
        box.Add(BuildSlotStatus("🔊 SOUND",    cn.SoundOut.IsConnected));
        return box;
    }

    private VisualElement BuildOptimizerStats(OptimizerNode opt)
    {
        var box = new VisualElement();
        box.AddToClassList("optimizer-stats-box");
        box.pickingMode = PickingMode.Ignore;
        box.Add(MiniStat($"-{opt.CpuReductionPercent:0}% CPU-Last", "stat-cpu"));
        box.Add(MiniStat($"+{opt.QualityBonus * 100:0}% Qualität",  "stat-quality"));
        box.Add(MiniStat($"⏱ +{opt.DevTimeCost:0} Wo.",            "stat-time"));
        return box;
    }

    private Label BuildSlotStatus(string label, bool ok)
    {
        var l = new Label($"{(ok ? "✓" : "✗")} {label}") { pickingMode = PickingMode.Ignore };
        l.AddToClassList("node-slot-status");
        l.AddToClassList(ok ? "slot-ok" : "slot-missing");
        return l;
    }

    private Label MiniStat(string text, string css = null)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.AddToClassList("node-mini-stat");
        if (css != null) l.AddToClassList(css);
        return l;
    }

    private Label BuildPrereqWarning(FeatureSO feat)
    {
        var w = new Label("⚠ " + string.Join(", ", feat.prerequisites.Select(p => p.featureName)));
        w.AddToClassList("node-prereq-warn");
        w.pickingMode = PickingMode.Ignore;
        return w;
    }

    // ════════════════════════════════════════════════════════════════
    //  PORT BODY
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildPortBody(GameNode node)
    {
        var body = new VisualElement();
        body.AddToClassList("node-body");
        body.pickingMode = PickingMode.Ignore;

        var inCol  = ColEl("node-port-column--inputs");
        var outCol = ColEl("node-port-column--outputs");
        foreach (var p in node.InputPorts)  inCol.Add(BuildPortRow(p));
        foreach (var p in node.OutputPorts) outCol.Add(BuildPortRow(p));
        body.Add(inCol); body.Add(outCol);
        return body;
    }

    private static VisualElement ColEl(string extra)
    {
        var c = new VisualElement();
        c.AddToClassList("node-port-column");
        c.AddToClassList(extra);
        c.pickingMode = PickingMode.Ignore;
        return c;
    }

    // ════════════════════════════════════════════════════════════════
    //  PORT ROW
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildPortRow(NodePort port)
    {
        var row = new VisualElement();
        row.AddToClassList("port-row");
        row.AddToClassList(port.IsOutput ? "port-row--output" : "port-row--input");

        var dot = new VisualElement();
        dot.AddToClassList("port-dot");
        dot.AddToClassList("port-type-" + port.Type.ToString().ToLower());
        dot.userData = port;

        // Required ports get an orange ring
        if (port.IsOutput && PortCompatibility.IsRequiredCoreOutput(port.Type))
            dot.AddToClassList("port-dot--required");
        if (port.IsOutput && PortCompatibility.IsRequiredCoreOutput(port.Type) && !port.IsConnected)
            dot.AddToClassList("port-dot--required-missing");

        var lbl = new Label(port.Label) { pickingMode = PickingMode.Ignore };
        lbl.AddToClassList("port-label");
        // Highlight unconnected required labels
        if (port.IsOutput && PortCompatibility.IsRequiredCoreOutput(port.Type) && !port.IsConnected)
            lbl.AddToClassList("port-label--missing");

        if (port.IsOutput)
        {
            row.Add(lbl); row.Add(dot);
            dot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                evt.StopPropagation();
                BeginWire(port);
            });
        }
        else
        {
            row.Add(dot); row.Add(lbl);
            dot.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_isDrawingWire) dot.AddToClassList("port-dot--target");
            });
            dot.RegisterCallback<MouseLeaveEvent>(_ =>
                dot.RemoveFromClassList("port-dot--target"));
            dot.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                TryCompleteWire(port);
            });
        }
        return row;
    }

    // ════════════════════════════════════════════════════════════════
    //  NODE DRAG
    // ════════════════════════════════════════════════════════════════

    private void MakeNodeDraggable(VisualElement card, GameNode node)
    {
        Vector2 startCard  = default;
        Vector2 startLocal = default;
        bool    dragging   = false;

        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || _isDrawingWire) return;
            // Don't start drag when clicking a port dot or delete button
            if (evt.target is VisualElement ve &&
                (ve.ClassListContains("port-dot") || ve.ClassListContains("node-delete-btn"))) return;
            dragging   = true;
            startLocal = CanvasLocal(evt.position);
            startCard  = node.CanvasPosition;
            card.CapturePointer(evt.pointerId);
            card.BringToFront();
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || !card.HasPointerCapture(evt.pointerId)) return;
            Vector2 delta       = CanvasLocal(evt.position) - startLocal;
            node.CanvasPosition = startCard + delta;
            card.style.left     = node.CanvasPosition.x;
            card.style.top      = node.CanvasPosition.y;
            _canvas.MarkDirtyRepaint();
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false;
            card.ReleasePointer(evt.pointerId);
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  SELECTION + INSPECTOR
    // ════════════════════════════════════════════════════════════════

    private void SelectNode(string nodeId)
    {
        if (_selectedNodeId != null && _nodeViews.TryGetValue(_selectedNodeId, out var old))
            old.RemoveFromClassList("graph-node--selected");

        _selectedNodeId = nodeId;
        if (nodeId == null) { HideInspector(); return; }

        if (_nodeViews.TryGetValue(nodeId, out var card))
            card.AddToClassList("graph-node--selected");

        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node != null) ShowInspector(node);
    }

    private void ShowInspector(GameNode node)
    {
        if (_inspector == null) return;
        _inspector.RemoveFromClassList("hidden");

        var feat = GetFeature(node);
        SetInsp("InspectorNodeName", node.DisplayName);
        SetInsp("InspectorPillar", feat != null ? $"{feat.category}  ·  {node.Kind}" : node.Kind.ToString());
        SetInsp("InspectorDesc",   feat?.description?.NullIfEmpty() ?? "");
        SetInsp("InspectorPrereqs", feat?.prerequisites.Any() == true
            ? "⚠ " + string.Join(", ", feat.prerequisites.Select(p => p.featureName))
            : "✓ Keine Voraussetzungen");
        SetInsp("InspectorCpu", $"CPU {feat?.cpuUsage ?? 0:0}  ·  ⏱ {node.DevWeeks:0.0} Wo.");
        SetInsp("InspectorConnIn",  $"Eingehend: {_graph.AllConnections.Count(c => c.ToNodeId   == node.NodeId)}");
        SetInsp("InspectorConnOut", $"Ausgehend: {_graph.AllConnections.Count(c => c.FromNodeId == node.NodeId)}");
    }

    private void HideInspector() => _inspector?.AddToClassList("hidden");

    private void SetInsp(string name, string text)
    {
        var l = _inspector?.Q<Label>(name);
        if (l != null) l.text = text;
    }

    // ════════════════════════════════════════════════════════════════
    //  DUPLICATE / DELETE / HINTS
    // ════════════════════════════════════════════════════════════════

    private void DeleteSelected()
    {
        if (_selectedNodeId == null) return;
        if (_graph.AllNodes.FirstOrDefault(n => n.NodeId == _selectedNodeId) is CoreNode)
        { ShowToast("Core-Node kann nicht gelöscht werden.", isError: true); return; }
        _graph.RemoveNode(_selectedNodeId);
    }

    private void DuplicateSelected()
    {
        if (_selectedNodeId == null) return;
        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == _selectedNodeId);
        var feat = GetFeature(node);
        if (feat == null) return;
        SpawnFeatureNode(feat, node.CanvasPosition + new Vector2(24, 24));
    }

    private void UpdateCanvasHint()
    {
        var hint = _canvasContent?.Q<VisualElement>("CanvasHint")
                ?? _canvas?.Q<VisualElement>("CanvasHint");
        hint?.EnableInClassList("hidden", _graph?.AllNodes.Any() == true);
    }

    private void UpdateStats()
    {
        if (_nodeCountLabel != null)
            _nodeCountLabel.text = $"{_graph.AllNodes.Count()} Nodes";
        if (_connCountLabel != null)
            _connCountLabel.text = $"{_graph.AllConnections.Count()} Verbindungen";
    }
}