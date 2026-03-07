// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Canvas.cs  — NODE-TYCOON (QoL Update)
//
//  NEU:
//   • Gepunktetes Grid als Canvas-Hintergrund
//   • Wire-Drop-Suggestion Panel (loslassen auf leerem Canvas)
//   • Feature-Popup bei Klick auf Node (Doppelklick)
//   • Zoom (Scroll-Rad, Ctrl+0 zum Resetten)
//   • Verbesserte Draht-Farben + animierter Glow
// ════════════════════════════════════════════════════════════════
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public partial class NodeGraphController
{
    // ── Pan / Zoom state ──────────────────────────────────────────
    private bool    _isPanning;
    private Vector2 _panStartMouse;
    private Vector2 _panOffset;
    private float   _zoom = 1f;
    private const float ZoomMin = 0.4f;
    private const float ZoomMax = 2.0f;

    // ── Wire state ────────────────────────────────────────────────
    private NodePort _wireSource;
    private Vector2  _wireSrcWorld;
    private Vector2  _wireEndWorld;
    private bool     _isDrawingWire;

    // ── Popup + Wire-Drop ─────────────────────────────────────────
    private FeaturePopupController   _featurePopup;
    private WireDropSuggestionPanel  _wireDrop;

    // ════════════════════════════════════════════════════════════════
    //  CANVAS LAYER INIT
    // ════════════════════════════════════════════════════════════════

    private void InitCanvasLayer()
    {
        // Grid-Hintergrund
        var grid = new VisualElement { name = "CanvasGrid" };
        grid.AddToClassList("canvas-grid");
        grid.pickingMode = PickingMode.Ignore;
        grid.generateVisualContent += DrawGrid;
        _canvas.Add(grid);
        grid.StretchToParentSize();

        // Content-Layer (für Nodes, pannable)
        _canvasContent = new VisualElement { name = "CanvasContent" };
        _canvasContent.style.position      = Position.Absolute;
        _canvasContent.style.width         = 4000;
        _canvasContent.style.height        = 3000;
        _canvasContent.pickingMode         = PickingMode.Ignore;
        _canvas.Add(_canvasContent);

        // Tooltip
        _tooltip = BuildTooltipElement();
        _tooltip.style.position = Position.Absolute;
        _tooltip.pickingMode    = PickingMode.Ignore;
        _canvas.Add(_tooltip);

        // Ghost Card (Drag-Vorschau)
        _ghostCard = new VisualElement { name = "GhostCard" };
        _ghostCard.AddToClassList("ghost-drag-card");
        _ghostCard.style.position = Position.Absolute;
        _ghostCard.style.display  = DisplayStyle.None;
        _ghostCard.pickingMode    = PickingMode.Ignore;
        _canvas.Add(_ghostCard);

        // Feature Popup
        _featurePopup = FeaturePopupController.Build(_canvas.parent ?? _canvas, _scoring);

        // Wire-Drop Suggestion Panel
        _wireDrop = WireDropSuggestionPanel.Build(_canvas);

        // Wire Painter
        _canvas.generateVisualContent += DrawAllWires;
    }

    private void InitCanvasEvents()
    {
        _canvas.focusable = true;
        _canvas.RegisterCallback<PointerDownEvent>(OnCanvasDown, TrickleDown.TrickleDown);
        _canvas.RegisterCallback<PointerMoveEvent>(OnCanvasMove, TrickleDown.TrickleDown);
        _canvas.RegisterCallback<PointerUpEvent>  (OnCanvasUp,   TrickleDown.TrickleDown);
        _canvas.RegisterCallback<WheelEvent>      (OnCanvasWheel);
        _canvas.RegisterCallback<KeyDownEvent>    (OnKeyDown);
    }

    // ════════════════════════════════════════════════════════════════
    //  GRID PAINTER
    // ════════════════════════════════════════════════════════════════

    private void DrawGrid(MeshGenerationContext mgc)
    {
        var p    = mgc.painter2D;
        var rect = _canvas.contentRect;
        if (rect.width < 1 || rect.height < 1) return;

        float gridStep   = 28f * _zoom;
        float offsetX    = _panOffset.x % gridStep;
        float offsetY    = _panOffset.y % gridStep;
        var   dotColor   = new Color(0.14f, 0.22f, 0.32f, 0.9f);
        float dotRadius  = 1.2f;

        p.strokeColor = dotColor;
        p.lineWidth   = dotRadius * 2f;

        for (float x = offsetX; x < rect.width; x += gridStep)
        {
            for (float y = offsetY; y < rect.height; y += gridStep)
            {
                p.BeginPath();
                p.Arc(new Vector2(x, y), dotRadius, 0f, 360f);
                p.Fill();
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  POINTER EVENTS
    // ════════════════════════════════════════════════════════════════

    private void OnCanvasDown(PointerDownEvent evt)
    {
        // Mittlere Maustaste oder Alt+Links = Pan
        if (evt.button == 2 || (evt.button == 0 && evt.altKey))
        {
            _isPanning = true;
            _panStartMouse = evt.position;
            _canvas.CapturePointer(evt.pointerId);
            evt.StopPropagation();
            return;
        }

        // Linksklick auf leeren Canvas → Deselektieren + Wire abbrechen
        if (evt.button == 0 && evt.target == _canvas)
        {
            SelectNode(null);
            CancelWire();
            if (_wireDrop.IsVisible) _wireDrop.Hide();
        }
        _canvas.Focus();
    }

    private void OnCanvasMove(PointerMoveEvent evt)
    {
        if (_isPanning && _canvas.HasPointerCapture(evt.pointerId))
        {
            Vector2 delta  = (Vector2)evt.position - _panStartMouse;
            _panStartMouse = evt.position;
            _panOffset    += delta;
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

        // Wire wurde auf leerem Canvas losgelassen → Vorschläge zeigen
        if (_isDrawingWire && evt.target == _canvas && _wireSource != null)
        {
            var dropCanvasPos  = CanvasLocal(evt.position);
            var activeFeatures = _graph.AllNodes
                .OfType<AnchorNode>().Select(n => n.FeatureData)
                .Concat(_graph.AllNodes.OfType<UpgradeNode>().Select(n => n.FeatureData))
                .Concat(_graph.AllNodes.OfType<SupportNode>().Select(n => n.FeatureData));

            var srcPort = _wireSource;
            CancelWire();

            _wireDrop.Show(
                srcPort, featureDB, _scoring,
                activeFeatures, dropCanvasPos, evt.position,
                (feat, pos) =>
                {
                    // Feature spawnen + sofort verbinden
                    GameNode newNode = IsSupportFeature(feat)
                        ? (GameNode)new SupportNode(feat) { CanvasPosition = pos }
                        : IsAnchorFeature(feat)
                            ? new AnchorNode(feat)  { CanvasPosition = pos }
                            : new UpgradeNode(feat) { CanvasPosition = pos };
                    _graph.AddNode(newNode);

                    // Verbindung versuchen
                    if (newNode.InputPorts.Count > 0)
                    {
                        var targetPort = newNode.InputPorts[0];
                        _graph.TryConnect(srcPort, targetPort);
                    }
                    ShowToast($"✓ '{feat.featureName}' angehängt.");
                });
        }
    }

    private void OnCanvasWheel(WheelEvent evt)
    {
        if (_wireDrop.IsVisible) { _wireDrop.Hide(); return; }

        float delta = -evt.delta.y * 0.05f;
        float newZoom = Mathf.Clamp(_zoom + delta, ZoomMin, ZoomMax);

        // Zoom um Mausposition
        Vector2 mouseLocal = _canvas.WorldToLocal(evt.mousePosition);
        _panOffset = mouseLocal - (mouseLocal - _panOffset) * (newZoom / _zoom);
        _zoom = newZoom;

        ApplyPanZoom();
        evt.StopPropagation();
    }

    private void ApplyPanZoom()
    {
        _canvasContent.style.left        = _panOffset.x;
        _canvasContent.style.top         = _panOffset.y;
        _canvasContent.style.scale       = new Scale(new Vector3(_zoom, _zoom, 1f));
        _canvasContent.style.transformOrigin = new TransformOrigin(0, 0, 0);
        _canvas.MarkDirtyRepaint();
    }

    // ════════════════════════════════════════════════════════════════
    //  KEYBOARD
    // ════════════════════════════════════════════════════════════════

    private void OnKeyDown(KeyDownEvent evt)
    {
        switch (evt.keyCode)
        {
            case KeyCode.Delete:
            case KeyCode.Backspace:
                DeleteSelected(); break;
            case KeyCode.Escape:
                CancelWire();
                _wireDrop.Hide();
                _featurePopup.Hide();
                break;
            case KeyCode.D when evt.ctrlKey:
                DuplicateSelected(); break;
            case KeyCode.F when evt.ctrlKey:
                FocusCenterCanvas(); break;
            case KeyCode.Alpha0 when evt.ctrlKey:
                ResetZoom(); break;
        }
    }

    private void FocusCenterCanvas()
    {
        if (!_nodeViews.Any()) return;
        float avgX = _graph.AllNodes.Average(n => n.CanvasPosition.x);
        float avgY = _graph.AllNodes.Average(n => n.CanvasPosition.y);
        var rect   = _canvas.contentRect;
        _panOffset = new Vector2(rect.width / 2f - avgX * _zoom, rect.height / 2f - avgY * _zoom);
        ApplyPanZoom();
        ShowToast("⌖ Ansicht zentriert");
    }

    private void ResetZoom()
    {
        _zoom = 1f;
        ApplyPanZoom();
        ShowToast("Zoom: 100%");
    }

    // ════════════════════════════════════════════════════════════════
    //  WIRE LOGIC
    // ════════════════════════════════════════════════════════════════

    private void BeginWire(NodePort port)
    {
        _wireSource = port; _isDrawingWire = true;
        _wireDrop.Hide();
        if (_nodeViews.TryGetValue(port.OwnerNode.NodeId, out var card))
        {
            var dot       = FindPortDot(card, port.PortId);
            _wireSrcWorld = dot?.worldBound.center ?? card.worldBound.center;
            _wireEndWorld = _wireSrcWorld;
        }
        _canvas.MarkDirtyRepaint();
        ShowToast("Ziehe zu einem Port — ESC zum Abbrechen | Loslassen für Vorschläge");
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
    //  WIRE PAINTER
    // ════════════════════════════════════════════════════════════════

    private static readonly Dictionary<PortType, Color> WireColors = new()
    {
        // Required core outputs — colour matches port dot colour
        { PortType.GameplaySlot,  new Color(0.98f, 0.57f, 0.24f) },  // Orange  — Core→Gameplay
        { PortType.GraphicSlot,   new Color(0.97f, 0.44f, 0.44f) },  // Red     — Core→Graphic
        { PortType.SoundSlot,     new Color(0.29f, 0.87f, 0.50f) },  // Green   — Core→Sound
        // Optional core outputs
        { PortType.AnchorSlot,    new Color(0.98f, 0.75f, 0.14f) },  // Amber   — Core→Anchor
        { PortType.SupportSlot,   new Color(0.65f, 0.55f, 0.98f) },  // Purple  — Core→Support
        // Feature chain
        { PortType.UpgradeSlot,   new Color(0.29f, 0.87f, 0.50f) },  // Green   — Anchor→Upgrade
        // Optimizer
        { PortType.ExpandSlot,    new Color(0.95f, 0.82f, 0.10f) },  // Gold    — expand
        { PortType.OptimizerSlot, new Color(0.95f, 0.35f, 0.35f) },  // Red     — optimizer
        // Legacy
        { PortType.PillarRoot,    new Color(0.98f, 0.57f, 0.24f) },
        { PortType.FeatureCore,   new Color(0.29f, 0.87f, 0.50f) },
        { PortType.DataFeed,      new Color(0.65f, 0.55f, 0.98f) },
        { PortType.Expandable,    new Color(0.95f, 0.82f, 0.10f) },
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

            var fromPort = fromCard.userData is GameNode gn
                ? gn.OutputPorts.FirstOrDefault(op => op.PortId == conn.FromPortId)
                : null;
            Color col = fromPort != null && WireColors.TryGetValue(fromPort.Type, out var wc)
                ? wc : new Color(0.5f, 0.5f, 0.6f);

            DrawWire(p, start, end, col);
        }

        // Laufende Verbindung (beim Ziehen)
        if (_isDrawingWire)
        {
            Color dragColor = _wireSource != null && WireColors.TryGetValue(_wireSource.Type, out var dc)
                ? new Color(dc.r, dc.g, dc.b, 0.7f)
                : new Color(1f, 1f, 1f, 0.55f);
            DrawWire(p,
                _canvas.WorldToLocal(_wireSrcWorld),
                _canvas.WorldToLocal(_wireEndWorld),
                dragColor, glow: false, dashed: true);
        }
    }

    private static void DrawWire(Painter2D p, Vector2 a, Vector2 b, Color col,
                                  bool glow = true, bool dashed = false)
    {
        if (glow)
        {
            p.strokeColor = new Color(col.r, col.g, col.b, 0.12f);
            p.lineWidth   = 12f;
            Bezier(p, a, b);
        }
        p.strokeColor = col;
        p.lineWidth   = dashed ? 1.5f : 2f;
        Bezier(p, a, b);
        Arrow(p, a, b, col);
    }

    private static void Bezier(Painter2D p, Vector2 a, Vector2 b)
    {
        float dx = Mathf.Max(Mathf.Abs(b.x - a.x) * 0.55f, 50f);
        p.BeginPath();
        p.MoveTo(a);
        p.BezierCurveTo(a + new Vector2(dx, 0), b - new Vector2(dx, 0), b);
        p.Stroke();
    }

    private static void Arrow(Painter2D p, Vector2 from, Vector2 to, Color col)
    {
        Vector2 dir  = (to - from).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        p.strokeColor = col;
        p.lineWidth   = 2f;
        p.BeginPath();
        p.MoveTo(to - dir * 9f + perp * 4f);
        p.LineTo(to);
        p.LineTo(to - dir * 9f - perp * 4f);
        p.Stroke();
    }

    // ════════════════════════════════════════════════════════════════
    //  KOORDINATEN-HILFSMETHODEN
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

    // Canvas-content local position (berücksichtigt Pan + Zoom)
    private Vector2 CanvasLocal(Vector2 screenPos) =>
        ((Vector2)_canvas.WorldToLocal(screenPos) - _panOffset) / _zoom;

    // ── Klassifizierungs-Helfer (auch in Canvas gebraucht) ────────
    private static bool IsAnchorFeature(FeatureSO f) =>
        (f.category == FeatureSO.FeatureCategory.Gameplay ||
         f.category == FeatureSO.FeatureCategory.Graphic  ||
         f.category == FeatureSO.FeatureCategory.Tech)
        && f.cpuUsage >= 15f && f.prerequisites.Count == 0;

    private static bool IsSupportFeature(FeatureSO f) =>
        f.category == FeatureSO.FeatureCategory.Sound     ||
        f.category == FeatureSO.FeatureCategory.Narrative ||
        f.category == FeatureSO.FeatureCategory.UX;

    // ── Popup für Double-Click auf Node-Karte ─────────────────────
    internal void OpenFeaturePopupForNode(string nodeId)
    {
        var node = _graph.AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
        FeatureSO feat = null;
        if (node is AnchorNode  an) feat = an.FeatureData;
        if (node is UpgradeNode un) feat = un.FeatureData;
        if (node is SupportNode sn) feat = sn.FeatureData;
        if (feat != null)
            _featurePopup.Show(feat, onAdd: null, maxCpu: maxCpu);
    }

    // ── Popup für Klick auf Sidebar-Karte ─────────────────────────
    internal void OpenFeaturePopupForSidebar(FeatureSO feat, Vector2 canvasSpawnPos)
    {
        _featurePopup.Show(feat, onAdd: () => SpawnFeatureNode(feat, canvasSpawnPos), maxCpu: maxCpu);
    }
}