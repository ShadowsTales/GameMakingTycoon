// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Canvas.cs  — NODE-TYCOON
//
//  Handles:
//    • Canvas pan / zoom
//    • Wire drawing (port → port)
//    • Wire-drop suggestion (drag onto empty canvas)
//    • Ghost card drag preview
//    • Node selection / deletion / duplication
//    • Canvas hint visibility
// ════════════════════════════════════════════════════════════════
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController
{
    // ── Ghost card ────────────────────────────────────────────────
    private VisualElement _ghostCard;

    // ════════════════════════════════════════════════════════════════
    //  CANVAS SETUP
    // ════════════════════════════════════════════════════════════════

    private void SetupCanvas(VisualElement root)
    {
        _canvas.RegisterCallback<WheelEvent>(OnScroll);
        _canvas.RegisterCallback<PointerDownEvent>(OnCanvasDown);
        _canvas.RegisterCallback<PointerMoveEvent>(OnCanvasMove);
        _canvas.RegisterCallback<PointerUpEvent>(OnCanvasUp);
        _canvas.generateVisualContent += DrawWires;

        root.Q<Button>("BtnFinalize")?.RegisterCallback<ClickEvent>(_ => FinalizeGame());

        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                DeleteSelected();
            if (evt.keyCode == KeyCode.D && evt.ctrlKey)
                DuplicateSelected();
            if (evt.keyCode == KeyCode.F && evt.ctrlKey)
                CenterCanvas();
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  CANVAS REBUILD
    // ════════════════════════════════════════════════════════════════

    private void RebuildCanvas()
    {
        (_canvasContent ?? _canvas).Clear();
        _nodeViews.Clear();

        foreach (var node in _graph.AllNodes)
        {
            var card = BuildNodeCard(node);
            _nodeViews[node.NodeId] = card;
            ApplyCanvasTransform(card, node.CanvasPosition);
            (_canvasContent ?? _canvas).Add(card);
        }

        _canvas.MarkDirtyRepaint();
        UpdateCanvasHint();
        UpdateStats();
        RefreshScore();
    }

    private void ApplyCanvasTransform(VisualElement card, Vector2 pos)
    {
        card.style.position = Position.Absolute;
        card.style.left     = pos.x;
        card.style.top      = pos.y;
    }

    // ════════════════════════════════════════════════════════════════
    //  PAN / ZOOM
    // ════════════════════════════════════════════════════════════════

    private void ApplyPanZoom()
    {
        var c = _canvasContent ?? _canvas;
        c.style.scale          = new Scale(new Vector3(_zoom, _zoom, 1f));
        c.style.left           = _panOffset.x;
        c.style.top            = _panOffset.y;
        c.style.transformOrigin = new TransformOrigin(0, 0, 0);
        _canvas.MarkDirtyRepaint();
    }

    private void CenterCanvas()
    {
        _panOffset = Vector2.zero;
        _zoom      = 1f;
        ApplyPanZoom();
    }

    private void OnScroll(WheelEvent evt)
    {
        float delta  = evt.delta.y > 0 ? 0.9f : 1.1f;
        _zoom        = Mathf.Clamp(_zoom * delta, ZoomMin, ZoomMax);
        ApplyPanZoom();
        evt.StopPropagation();
    }

    // ════════════════════════════════════════════════════════════════
    //  POINTER EVENTS
    // ════════════════════════════════════════════════════════════════

    private void OnCanvasDown(PointerDownEvent evt)
    {
        bool alt = evt.altKey;
        if (alt && evt.button == 0)
        {
            _isPanning = true;
            _canvas.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }
    }

    private void OnCanvasMove(PointerMoveEvent evt)
    {
        if (_isPanning && _canvas.HasPointerCapture(evt.pointerId))
        {
            _panOffset += (Vector2)evt.deltaPosition;
            ApplyPanZoom();
            return;
        }

        if (_isDrawingWire)
        {
            _wireEndWorld = evt.position;
            _canvas.MarkDirtyRepaint();
        }
    }

    private void OnCanvasUp(PointerUpEvent evt)
    {
        if (_isPanning && _canvas.HasPointerCapture(evt.pointerId))
        {
            _isPanning = false;
            _canvas.ReleasePointer(evt.pointerId);
            return;
        }

        // Wire dropped on empty canvas → show suggestions
        if (_isDrawingWire && evt.target == _canvas && _wireSource != null)
        {
            var dropPos        = CanvasLocal(evt.position);
            var activeFeatures = _graph.AllNodes
                .Select(n => n.GetFeatureData())
                .Where(f => f != null);

            var srcPort = _wireSource;
            CancelWire();

            _wireDrop.Show(
                srcPort, featureDB, _scoring,
                activeFeatures, dropPos, evt.position,
                (feat, pos) =>
                {
                    SpawnFeatureNode(feat, pos);

                    // Immediately try to connect the wire
                    var newNode = _graph.AllNodes.LastOrDefault(n => n.GetFeatureData() == feat);
                    if (newNode?.InputPorts.Count > 0)
                        _graph.TryConnect(srcPort, newNode.InputPorts[0]);

                    ShowToast($"✓ '{feat.featureName}' angehängt.");
                });
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  WIRE DRAWING
    // ════════════════════════════════════════════════════════════════

    private void DrawWires(MeshGenerationContext ctx)
    {
        // Bezier wires for all connections
        foreach (var conn in _graph.AllConnections)
        {
            var fromDot = FindPortDot(conn.FromPortId);
            var toDot   = FindPortDot(conn.ToPortId);
            if (fromDot == null || toDot == null) continue;

            var from = (Vector2)fromDot.worldBound.center;
            var to   = (Vector2)toDot.worldBound.center;

            DrawBezier(ctx, from, to, new Color(0.13f, 0.83f, 0.93f, 0.8f));
        }

        // In-progress wire
        if (_isDrawingWire && _wireSource != null)
        {
            var srcDot = FindPortDot(_wireSource.PortId);
            if (srcDot != null)
            {
                var from = (Vector2)srcDot.worldBound.center;
                DrawBezier(ctx, from, _wireEndWorld, new Color(1f, 1f, 1f, 0.4f));
            }
        }
    }

    private static void DrawBezier(MeshGenerationContext ctx, Vector2 from, Vector2 to, Color color)
    {
        float dx  = Mathf.Abs(to.x - from.x) * 0.5f;
        var   cp1 = new Vector2(from.x + dx, from.y);
        var   cp2 = new Vector2(to.x   - dx, to.y);
        var   painter = ctx.painter2D;
        painter.strokeColor = color;
        painter.lineWidth   = 2f;
        painter.BeginPath();
        painter.MoveTo(from);
        painter.BezierCurveTo(cp1, cp2, to);
        painter.Stroke();
    }

    // ════════════════════════════════════════════════════════════════
    //  PORT WIRE INTERACTION
    // ════════════════════════════════════════════════════════════════

    internal void StartWire(NodePort port)
    {
        _isDrawingWire = true;
        _wireSource    = port;
    }

    private void CancelWire()
    {
        _isDrawingWire = false;
        _wireSource    = null;
        _canvas.MarkDirtyRepaint();
    }

    // ════════════════════════════════════════════════════════════════
    //  GHOST CARD
    // ════════════════════════════════════════════════════════════════

    private void ShowGhostCard(FeatureSO feature, Vector2 screenPos)
    {
        if (_ghostCard == null)
        {
            _ghostCard = new VisualElement();
            _ghostCard.AddToClassList("ghost-card");
            _ghostCard.pickingMode = PickingMode.Ignore;
        }

        _ghostCard.Q<Label>()?.RemoveFromHierarchy();
        _ghostCard.Add(new Label(feature.featureName) { pickingMode = PickingMode.Ignore });
        _canvas.Add(_ghostCard);
        MoveGhostCard(screenPos);
    }

    private void MoveGhostCard(Vector2 screenPos)
    {
        if (_ghostCard == null) return;
        var local = _canvas.WorldToLocal(screenPos);
        _ghostCard.style.left = local.x + 8f;
        _ghostCard.style.top  = local.y + 8f;
    }

    private void HideGhostCard()
    {
        _ghostCard?.RemoveFromHierarchy();
    }

    // ════════════════════════════════════════════════════════════════
    //  SELECTION
    // ════════════════════════════════════════════════════════════════

    private void SelectNode(string nodeId)
    {
        // Deselect previous
        if (_selectedNodeId != null && _nodeViews.TryGetValue(_selectedNodeId, out var prev))
            prev.RemoveFromClassList("node-selected");

        _selectedNodeId = nodeId;

        if (nodeId != null && _nodeViews.TryGetValue(nodeId, out var next))
            next.AddToClassList("node-selected");

        RefreshInspector(nodeId);
    }

    private void RefreshInspector(string nodeId)
    {
        if (_inspector == null) return;

        if (nodeId == null)
        {
            _inspector.AddToClassList("hidden");
            return;
        }

        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node == null) return;

        _inspector.RemoveFromClassList("hidden");

        _inspector.Q<Label>("InspectorNodeName")?.SetText(node.DisplayName);
        _inspector.Q<Label>("InspectorPillar")?.SetText(node.Pillar.ToString());

        var feat = node.GetFeatureData();
        _inspector.Q<Label>("InspectorCpu")?.SetText(feat != null ? $"CPU: {feat.cpuUsage:0}" : "—");
        _inspector.Q<Label>("InspectorDev")?.SetText($"Dev: {node.DevWeeks:0.0}w");
    }

    // ════════════════════════════════════════════════════════════════
    //  NODE ACTIONS
    // ════════════════════════════════════════════════════════════════

    private void DeleteSelected()
    {
        if (_selectedNodeId == null) { ShowToast("Kein Node ausgewählt.", isError: true); return; }
        _graph.RemoveNode(_selectedNodeId);
        _selectedNodeId = null;
    }

    private void DuplicateSelected()
    {
        if (_selectedNodeId == null) return;
        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == _selectedNodeId);
        var feat = node?.GetFeatureData();
        if (feat == null) return;
        SpawnFeatureNode(feat, node.CanvasPosition + new Vector2(24, 24));
    }

    private void FinalizeGame()
    {
        var result = _graph.EvaluateGame(maxCpu);
        if (!result.IsValid)
        {
            ShowToast("⚠ Graph ungültig: " + string.Join(", ", result.MissingRequiredSlots), isError: true);
            return;
        }
        Debug.Log($"[NodeGraph] Finalisiert — {result.Summary}");
        // TODO: hand result to GameManager
    }

    // ════════════════════════════════════════════════════════════════
    //  POPUP OPENERS
    // ════════════════════════════════════════════════════════════════

    internal void OpenFeaturePopupForNode(string nodeId)
    {
        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
        var feat = node?.GetFeatureData();
        if (feat != null)
            _featurePopup.Show(feat, onAdd: null, maxCpu: maxCpu);
    }

    internal void OpenFeaturePopupForSidebar(FeatureSO feat, Vector2 canvasSpawnPos)
    {
        _featurePopup.Show(feat, onAdd: () => SpawnFeatureNode(feat, canvasSpawnPos), maxCpu: maxCpu);
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════

    private Vector2 CanvasLocal(Vector2 screenPos) =>
        ((Vector2)_canvas.WorldToLocal(screenPos) - _panOffset) / _zoom;

    private VisualElement FindPortDot(string portId)
    {
        foreach (var view in _nodeViews.Values)
        {
            var dot = FindPortDotInTree(view, portId);
            if (dot != null) return dot;
        }
        return null;
    }

    private static VisualElement FindPortDotInTree(VisualElement el, string portId)
    {
        if (el.userData is NodePort p && p.PortId == portId) return el;
        foreach (var child in el.Children())
        {
            var found = FindPortDotInTree(child, portId);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateCanvasHint()
    {
        var hint = (_canvasContent ?? _canvas).Q<VisualElement>("CanvasHint");
        hint?.EnableInClassList("hidden", _graph?.AllNodes.Any() == true);
    }

    private void UpdateStats()
    {
        if (_nodeCountLabel != null)
            _nodeCountLabel.text = $"{_graph.AllNodes.Count()} Nodes";
        if (_connCountLabel != null)
            _connCountLabel.text = $"{_graph.AllConnections.Count()} Verbindungen";
    }

    private void MakeNodeDraggable(VisualElement card, GameNode node)
    {
        bool dragging    = false;
        Vector2 dragStart = default;
        Vector2 nodeStart = default;

        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || evt.altKey) return;
            dragging  = true;
            dragStart = evt.position;
            nodeStart = node.CanvasPosition;
            card.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || !card.HasPointerCapture(evt.pointerId)) return;
            var delta          = ((Vector2)evt.position - dragStart) / _zoom;
            node.CanvasPosition = nodeStart + delta;
            ApplyCanvasTransform(card, node.CanvasPosition);
            _canvas.MarkDirtyRepaint();
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false;
            card.ReleasePointer(evt.pointerId);
        });
    }
}