// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Nodes.cs
//  Responsibilities: node card build · port rows · drag · selection · inspector
// ════════════════════════════════════════════════════════════════
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController
{
    // ════════════════════════════════════════════════════════════════
    //  NODE CARD BUILDER
    // ════════════════════════════════════════════════════════════════

    private VisualElement CreateNodeCard(GameNode node)
    {
        var card = new VisualElement();
        card.name = "node_" + node.NodeId;
        card.AddToClassList("graph-node");
        card.AddToClassList("node-" + node.Pillar.ToString().ToLower());
        if (node.Kind == NodeKind.PillarStart) card.AddToClassList("pillar-start-node");
        if (node.Kind == NodeKind.Optimizer)   card.AddToClassList("optimizer-node");
        card.style.position = Position.Absolute;
        card.style.left     = node.CanvasPosition.x;
        card.style.top      = node.CanvasPosition.y;
        card.userData       = node;

        card.Add(BuildNodeHeader(node));
        if (node is FeatureGameNode fn)  card.Add(BuildYearBadge(fn));
        card.Add(BuildPortBody(node));
        if (node is FeatureGameNode fgn && fgn.Feature.prerequisites.Count > 0)
            card.Add(BuildPrereqWarning(fgn));

        MakeNodeDraggable(card, node);
        card.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); SelectNode(node.NodeId); });

        if (node is FeatureGameNode hfn)
        {
            card.RegisterCallback<MouseEnterEvent>(_ => ShowFeatureTooltip(hfn.Feature, card));
            card.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
        }
        return card;
    }

    private VisualElement BuildNodeHeader(GameNode node)
    {
        var header = new VisualElement();
        header.AddToClassList("node-header");
        header.pickingMode = PickingMode.Ignore;

        header.Add(new Label(node.Pillar.ToString().ToUpper())
            { pickingMode = PickingMode.Ignore,
              style = { } }.Also(l => l.AddToClassList("node-pillar-tag")));

        header.Add(new Label(node.DisplayName)
            { pickingMode = PickingMode.Ignore }
            .Also(l => l.AddToClassList("node-title")));

        if (node.Kind != NodeKind.PillarStart)
        {
            var del = new Button(() => _graph.RemoveNode(node.NodeId)) { text = "×" };
            del.AddToClassList("node-delete-btn");
            header.Add(del);
        }
        return header;
    }

    private static VisualElement BuildYearBadge(FeatureGameNode fn)
    {
        var yr = new Label(fn.Feature.releaseYear.ToString());
        yr.AddToClassList("node-year-badge");
        yr.pickingMode = PickingMode.Ignore;
        return yr;
    }

    private VisualElement BuildPortBody(GameNode node)
    {
        var body = new VisualElement();
        body.AddToClassList("node-body");
        body.pickingMode = PickingMode.Ignore;

        var inCol  = ColEl("node-port-column--inputs");
        var outCol = ColEl("node-port-column--outputs");
        foreach (var p in node.InputPorts)  inCol .Add(BuildPortRow(p));
        foreach (var p in node.OutputPorts) outCol.Add(BuildPortRow(p));
        body.Add(inCol); body.Add(outCol);
        return body;
    }

    private static VisualElement ColEl(string cls)
    {
        var el = new VisualElement();
        el.AddToClassList("node-port-column");
        el.AddToClassList(cls);
        el.pickingMode = PickingMode.Ignore;
        return el;
    }

    private static VisualElement BuildPrereqWarning(FeatureGameNode fgn)
    {
        var w = new Label("⚠ " + string.Join(", ", fgn.Feature.prerequisites.Select(p => p.featureName)));
        w.AddToClassList("node-prereq-warn");
        w.pickingMode = PickingMode.Ignore;
        return w;
    }

    // ════════════════════════════════════════════════════════════════
    //  PORT ROWS
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

        var lbl = new Label(port.Label) { pickingMode = PickingMode.Ignore };
        lbl.AddToClassList("port-label");

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
                { if (_isDrawingWire) dot.AddToClassList("port-dot--target"); });
            dot.RegisterCallback<MouseLeaveEvent>(_ =>
                dot.RemoveFromClassList("port-dot--target"));
            dot.RegisterCallback<PointerUpEvent>(evt =>
                { evt.StopPropagation(); TryCompleteWire(port); });
        }
        return row;
    }

    // ════════════════════════════════════════════════════════════════
    //  NODE DRAG
    // ════════════════════════════════════════════════════════════════

    private void MakeNodeDraggable(VisualElement card, GameNode node)
    {
        Vector2 startCard = default, startLocal = default;
        bool dragging = false;

        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || _isDrawingWire) return;
            dragging   = true;
            startLocal = CanvasLocal(evt.position);
            startCard  = new Vector2(card.style.left.value.value, card.style.top.value.value);
            card.CapturePointer(evt.pointerId);
            card.BringToFront();
            evt.StopPropagation();
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || !card.HasPointerCapture(evt.pointerId)) return;
            Vector2 delta = CanvasLocal(evt.position) - startLocal;
            float nx = startCard.x + delta.x, ny = startCard.y + delta.y;
            card.style.left = nx; card.style.top = ny;
            node.CanvasPosition = new Vector2(nx, ny);
            _canvas.MarkDirtyRepaint();
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false; card.ReleasePointer(evt.pointerId);
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  SPAWNING
    // ════════════════════════════════════════════════════════════════

    public void SpawnFeatureNode(FeatureSO feature, Vector2 canvasPos)
    {
        var node = new FeatureGameNode(feature) { CanvasPosition = canvasPos };
        _graph.AddNode(node);
    }

    private void SpawnOptimizer()
    {
        Vector2 pos = new Vector2(300, 200);
        if (_selectedNodeId != null && _nodeViews.TryGetValue(_selectedNodeId, out var v))
            pos = new Vector2(v.style.left.value.value + 250, v.style.top.value.value);
        _graph.AddNode(new OptimizerNode(FeatureSO.FeatureCategory.Tech) { CanvasPosition = pos });
    }

    private void DeleteSelected()   { if (_selectedNodeId != null) _graph.RemoveNode(_selectedNodeId); }

    private void DuplicateSelected()
    {
        if (_selectedNodeId == null) return;
        if (_graph.AllNodes.FirstOrDefault(n => n.NodeId == _selectedNodeId) is FeatureGameNode fn)
            SpawnFeatureNode(fn.Feature, fn.CanvasPosition + new Vector2(20, 20));
    }

    // ════════════════════════════════════════════════════════════════
    //  SELECTION + INSPECTOR
    // ════════════════════════════════════════════════════════════════

    private void SelectNode(string nodeId)
    {
        if (_selectedNodeId != null && _nodeViews.TryGetValue(_selectedNodeId, out var prev))
            prev.RemoveFromClassList("graph-node--selected");

        _selectedNodeId = nodeId;

        if (nodeId != null && _nodeViews.TryGetValue(nodeId, out var card))
        {
            card.AddToClassList("graph-node--selected");
            ShowInspector(nodeId);
        }
        else _inspector?.AddToClassList("hidden");
    }

    private void ShowInspector(string nodeId)
    {
        if (_inspector == null) return;
        _inspector.RemoveFromClassList("hidden");

        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node == null) return;

        IL("InspectorNodeName", node.DisplayName);
        IL("InspectorPillar",   "Pillar: " + node.Pillar);
        IL("InspectorConnIn",   "Eingehend: "  + _graph.AllConnections.Count(c => c.ToNodeId   == nodeId));
        IL("InspectorConnOut",  "Ausgehend: "  + _graph.AllConnections.Count(c => c.FromNodeId == nodeId));

        if (node is FeatureGameNode fn)
        {
            IL("InspectorDesc",    fn.Feature.description.NullIfEmpty() ?? "Keine Beschreibung");
            IL("InspectorCpu",     $"CPU: {fn.Feature.cpuUsage}%  RAM: {fn.Feature.ramUsage}%");
            IL("InspectorPrereqs", fn.Feature.prerequisites.Count > 0
                ? "Benötigt: " + string.Join(", ", fn.Feature.prerequisites.Select(p => p.featureName))
                : "Keine Voraussetzungen");
            IL("InspectorQualityDelta", _scoring.GetQualityDeltaText(fn.Feature));  // NEW
        }
        else
        {
            IL("InspectorDesc", ""); IL("InspectorCpu", "");
            IL("InspectorPrereqs", ""); IL("InspectorQualityDelta", "");
        }
    }

    private void IL(string name, string text)
    { var l = _inspector?.Q<Label>(name); if (l != null) l.text = text; }
}

// ── tiny extension helpers ────────────────────────────────────────
internal static class LabelExt
{
    public static T Also<T>(this T self, System.Action<T> configure)
        where T : UnityEngine.UIElements.VisualElement
    { configure(self); return self; }

    public static string NullIfEmpty(this string s) =>
        string.IsNullOrEmpty(s) ? null : s;
}
