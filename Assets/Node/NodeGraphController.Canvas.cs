// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Canvas.cs
//  Responsibilities: canvas layer init · pan · wire draw · keyboard
// ════════════════════════════════════════════════════════════════
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public partial class NodeGraphController
{
    // ── Pan state ──────────────────────────────────────────────────
    private bool    _isPanning;
    private Vector2 _panStartMouse;
    private Vector2 _panOffset;

    // ── Wire state ─────────────────────────────────────────────────
    private NodePort _wireSource;
    private Vector2  _wireSrcWorld;
    private Vector2  _wireEndWorld;
    private bool     _isDrawingWire;

    // ════════════════════════════════════════════════════════════════
    //  CANVAS LAYER INIT
    // ════════════════════════════════════════════════════════════════

    private void InitCanvasLayer()
    {
        _canvasContent = new VisualElement { name = "CanvasContent" };
        _canvasContent.style.position = Position.Absolute;
        _canvasContent.style.width    = 4000;
        _canvasContent.style.height   = 3000;
        _canvasContent.pickingMode    = PickingMode.Ignore;
        _canvas.Add(_canvasContent);

        _tooltip = BuildTooltipElement();
        _tooltip.style.position = Position.Absolute;
        //_tooltip.style.zIndex   = 999;
        _tooltip.pickingMode    = PickingMode.Ignore;
        _canvas.Add(_tooltip);

        _ghostCard = new VisualElement { name = "GhostCard" };
        _ghostCard.AddToClassList("ghost-drag-card");
        _ghostCard.style.position = Position.Absolute;
        _ghostCard.style.display  = DisplayStyle.None;
        _ghostCard.pickingMode    = PickingMode.Ignore;
        _canvas.Add(_ghostCard);

        _canvas.generateVisualContent += DrawAllWires;
    }

    private void InitCanvasEvents()
    {
        _canvas.focusable = true;
        _canvas.RegisterCallback<PointerDownEvent>(OnCanvasDown, TrickleDown.TrickleDown);
        _canvas.RegisterCallback<PointerMoveEvent>(OnCanvasMove, TrickleDown.TrickleDown);
        _canvas.RegisterCallback<PointerUpEvent>  (OnCanvasUp,   TrickleDown.TrickleDown);
        _canvas.RegisterCallback<KeyDownEvent>    (OnKeyDown);
    }

    // ════════════════════════════════════════════════════════════════
    //  POINTER EVENTS
    // ════════════════════════════════════════════════════════════════

    private void OnCanvasDown(PointerDownEvent evt)
    {
        if (evt.button == 2 || (evt.button == 0 && evt.altKey))
        {
            _isPanning = true; _panStartMouse = evt.position;
            _canvas.CapturePointer(evt.pointerId);
            evt.StopPropagation(); return;
        }
        if (evt.button == 0 && evt.target == _canvas) { SelectNode(null); CancelWire(); }
        _canvas.Focus();
    }

    private void OnCanvasMove(PointerMoveEvent evt)
    {
        if (_isPanning && _canvas.HasPointerCapture(evt.pointerId))
        {
            Vector2 delta  = (Vector2)evt.position - _panStartMouse;
            _panStartMouse = evt.position;
            _panOffset    += delta;
            _canvasContent.style.left = _panOffset.x;
            _canvasContent.style.top  = _panOffset.y;
            _canvas.MarkDirtyRepaint();
            return;
        }
        if (_isDrawingWire) { _wireEndWorld = evt.position; _canvas.MarkDirtyRepaint(); }
    }

    private void OnCanvasUp(PointerUpEvent evt)
    {
        if (_isPanning && _canvas.HasPointerCapture(evt.pointerId))
        { _isPanning = false; _canvas.ReleasePointer(evt.pointerId); }

        if (_isDrawingWire && evt.target == _canvas) CancelWire();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if      (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) DeleteSelected();
        else if (evt.keyCode == KeyCode.Escape)                                     CancelWire();
        else if (evt.keyCode == KeyCode.D && evt.ctrlKey)                           DuplicateSelected();
    }

    // ════════════════════════════════════════════════════════════════
    //  WIRE LOGIC
    // ════════════════════════════════════════════════════════════════

    private void BeginWire(NodePort port)
    {
        _wireSource = port; _isDrawingWire = true;
        if (_nodeViews.TryGetValue(port.OwnerNode.NodeId, out var card))
        {
            var dot       = FindPortDot(card, port.PortId);
            _wireSrcWorld = dot?.worldBound.center ?? card.worldBound.center;
            _wireEndWorld = _wireSrcWorld;
        }
        _canvas.MarkDirtyRepaint();
        ShowToast("Ziehe zu einem Eingabe-Port — ESC zum Abbrechen");
    }

    private void CancelWire()
    {
        _wireSource = null; _isDrawingWire = false;
        _canvas.MarkDirtyRepaint();
    }

    private void TryCompleteWire(NodePort target)
    {
        if (!_isDrawingWire || _wireSource == null) return;
        _graph.TryConnect(_wireSource, target);
        CancelWire();
    }

    // ════════════════════════════════════════════════════════════════
    //  WIRE PAINTER  (generateVisualContent callback)
    //
    //  Wire colour = PortType, matching the Copilot doc's intent
    //  but mapped onto your existing PortType enum:
    //    FeatureCore / PillarRoot   → pillar colour   (hard requirement)
    //    Prerequisite               → amber           (soft requirement)
    //    AudioTrigger/RenderPass/DataFeed → cyan      (cross-pillar synergy)
    //    Expandable / Optimizer     → gold            (enhancement)
    // ════════════════════════════════════════════════════════════════

    private static readonly Dictionary<PortType, Color> WireColors = new()
    {
        { PortType.PillarRoot,   new Color(0.60f, 0.60f, 0.65f) },
        { PortType.FeatureCore,  Color.white                     },
        { PortType.Prerequisite, new Color(0.95f, 0.55f, 0.10f) },   // amber  — soft req
        { PortType.AudioTrigger, new Color(0.20f, 0.85f, 0.85f) },   // cyan   — synergy
        { PortType.RenderPass,   new Color(0.20f, 0.85f, 0.85f) },
        { PortType.DataFeed,     new Color(0.20f, 0.85f, 0.85f) },
        { PortType.Expandable,   new Color(0.95f, 0.82f, 0.10f) },   // gold   — enhancement
        { PortType.Optimizer,    new Color(0.95f, 0.82f, 0.10f) },
    };

    private void DrawAllWires(MeshGenerationContext mgc)
    {
        var p = mgc.painter2D;

        foreach (var conn in _graph.AllConnections)
        {
            if (!_nodeViews.TryGetValue(conn.FromNodeId, out var fromCard)) continue;
            if (!_nodeViews.TryGetValue(conn.ToNodeId,   out var toCard))   continue;

            Vector2 start = _canvas.WorldToLocal(GetPortWorldPos(fromCard, conn.FromPortId));
            Vector2 end   = _canvas.WorldToLocal(GetPortWorldPos(toCard,   conn.ToPortId));

            // Resolve wire colour from the output port's type
            var fromPort = fromCard.userData is GameNode gn
                ? gn.OutputPorts.FirstOrDefault(op => op.PortId == conn.FromPortId)
                : null;
            Color col = fromPort != null && WireColors.TryGetValue(fromPort.Type, out var wc)
                ? wc : Color.white;

            DrawWire(p, start, end, col);
        }

        if (_isDrawingWire)
        {
            DrawWire(p, _canvas.WorldToLocal(_wireSrcWorld),
                        _canvas.WorldToLocal(_wireEndWorld),
                        new Color(1f, 1f, 1f, 0.55f), glow: false);
        }
    }

    private static void DrawWire(Painter2D p, Vector2 a, Vector2 b, Color col, bool glow = true)
    {
        if (glow) { p.strokeColor = new Color(col.r, col.g, col.b, 0.15f); p.lineWidth = 10f; Bezier(p, a, b); }
        p.strokeColor = col;  p.lineWidth = 2.5f; Bezier(p, a, b);
        Arrow(p, a, b, col);
    }

    private static void Bezier(Painter2D p, Vector2 a, Vector2 b)
    {
        float dx = Mathf.Max(Mathf.Abs(b.x - a.x) * 0.55f, 40f);
        p.BeginPath(); p.MoveTo(a);
        p.BezierCurveTo(a + new Vector2(dx, 0), b - new Vector2(dx, 0), b);
        p.Stroke();
    }

    private static void Arrow(Painter2D p, Vector2 from, Vector2 to, Color col)
    {
        Vector2 dir = (to - from).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        p.strokeColor = col; p.lineWidth = 2f;
        p.BeginPath();
        p.MoveTo(to - dir * 8f + perp * 3.6f);
        p.LineTo(to);
        p.LineTo(to - dir * 8f - perp * 3.6f);
        p.Stroke();
    }

    // ════════════════════════════════════════════════════════════════
    //  COORDINATE HELPERS  (used by Nodes.cs and Sidebar.cs too)
    // ════════════════════════════════════════════════════════════════

    private Vector2 GetPortWorldPos(VisualElement card, string portId)
    {
        var dot = FindPortDot(card, portId);
        return dot != null ? dot.worldBound.center : card.worldBound.center;
    }

    private VisualElement FindPortDot(VisualElement el, string portId)
    {
        if (el.userData is NodePort p && p.PortId == portId) return el;
        foreach (var child in el.Children())
        { var f = FindPortDot(child, portId); if (f != null) return f; }
        return null;
    }

    // Canvas-content local position (accounts for pan)
    private Vector2 CanvasLocal(Vector2 screenPos) =>
        (Vector2)_canvas.WorldToLocal(screenPos) - _panOffset;
}
