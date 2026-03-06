// ============================================================
//  NodeGraphConnectionPainter.cs
//  Attach to the same VisualElement as your NodeCanvas.
//  Draws Bezier wires between connected nodes using
//  generateVisualContent (same pattern as NodeConnectionService).
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class NodeGraphConnectionPainter
{
    private readonly VisualElement _canvas;
    private NodeGraph              _graph;
    private Dictionary<string, VisualElement> _nodeViews;

    // Port dot colours per PortType
    private static readonly Dictionary<PortType, Color> _portColors = new()
    {
        { PortType.PillarRoot,   new Color(1f,   1f,   1f,   1f) },
        { PortType.FeatureCore,  new Color(0.2f, 0.8f, 1f,   1f) },
        { PortType.Expandable,   new Color(0.2f, 1f,   0.4f, 1f) },
        { PortType.Optimizer,    new Color(1f,   0.8f, 0.1f, 1f) },
        { PortType.Prerequisite, new Color(1f,   0.3f, 0.3f, 1f) },
        { PortType.AudioTrigger, new Color(1f,   0.5f, 0.1f, 1f) },
        { PortType.RenderPass,   new Color(0.8f, 0.2f, 1f,   1f) },
        { PortType.DataFeed,     new Color(0.2f, 0.6f, 1f,   1f) },
    };

    private static readonly Dictionary<FeatureSO.FeatureCategory, Color> _pillarColors = new()
    {
        { FeatureSO.FeatureCategory.Gameplay, new Color(0.96f, 0.62f, 0.04f) },
        { FeatureSO.FeatureCategory.Graphic,  new Color(0.94f, 0.27f, 0.27f) },
        { FeatureSO.FeatureCategory.Sound,    new Color(0.13f, 0.77f, 0.37f) },
        { FeatureSO.FeatureCategory.Tech,     new Color(0.23f, 0.51f, 0.96f) },
    };

    public NodeGraphConnectionPainter(
        VisualElement canvas,
        NodeGraph graph,
        Dictionary<string, VisualElement> nodeViews)
    {
        _canvas    = canvas;
        _graph     = graph;
        _nodeViews = nodeViews;

        _canvas.generateVisualContent += Draw;
    }

    public void RequestRedraw() => _canvas.MarkDirtyRepaint();

    // ─────────────────────────────────────────────────────────────
    //  DRAW
    // ─────────────────────────────────────────────────────────────

    private void Draw(MeshGenerationContext mgc)
    {
        var painter = mgc.painter2D;

        foreach (var conn in _graph.AllConnections)
        {
            if (!_nodeViews.TryGetValue(conn.FromNodeId, out var fromCard)) continue;
            if (!_nodeViews.TryGetValue(conn.ToNodeId,   out var toCard))   continue;

            Vector2 start = GetPortWorldPos(fromCard, conn.FromPortId, isOutput: true);
            Vector2 end   = GetPortWorldPos(toCard,   conn.ToPortId,   isOutput: false);

            // Convert from world to canvas-local
            start = _canvas.WorldToLocal(start);
            end   = _canvas.WorldToLocal(end);

            Color lineColor = GetConnectionColor(conn);

            // Glow pass
            painter.strokeColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.12f);
            painter.lineWidth   = 8f;
            DrawBezier(painter, start, end);

            // Main line
            painter.strokeColor = lineColor;
            painter.lineWidth   = 2.5f;
            DrawBezier(painter, start, end);
        }
    }

    private void DrawBezier(Painter2D p, Vector2 start, Vector2 end)
    {
        float dx = Mathf.Abs(end.x - start.x) * 0.55f;

        p.BeginPath();
        p.MoveTo(start);
        p.BezierCurveTo(
            new Vector2(start.x + dx, start.y),
            new Vector2(end.x   - dx, end.y),
            end);
        p.Stroke();
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the port dot element inside a node card and returns its world centre.
    /// </summary>
    private Vector2 GetPortWorldPos(VisualElement card, string portId, bool isOutput)
    {
        // We look for a child element whose userData == the NodePort with matching PortId
        VisualElement dot = FindPortDot(card, portId);
        if (dot == null)
        {
            // Fallback: left/right edge of card
            var r = card.worldBound;
            return isOutput
                ? new Vector2(r.xMax, r.center.y)
                : new Vector2(r.xMin, r.center.y);
        }
        return dot.worldBound.center;
    }

    private VisualElement FindPortDot(VisualElement root, string portId)
    {
        if (root.userData is NodePort p && p.PortId == portId)
            return root;

        foreach (var child in root.Children())
        {
            var found = FindPortDot(child, portId);
            if (found != null) return found;
        }
        return null;
    }

    private Color GetConnectionColor(NodeConnection conn)
    {
        // Use the source node's pillar colour
        var fromNode = GetNodeById(conn.FromNodeId);
        if (fromNode != null && _pillarColors.TryGetValue(fromNode.Pillar, out var c))
            return c;

        // Fallback: port type colour
        if (conn.FromPort != null && _portColors.TryGetValue(conn.FromPort.Type, out var pc))
            return pc;

        return Color.white;
    }

    private GameNode GetNodeById(string id)
    {
        foreach (var n in _graph.AllNodes)
            if (n.NodeId == id) return n;
        return null;
    }

    public static Color GetPortColor(PortType type)
    {
        return _portColors.TryGetValue(type, out var c) ? c : Color.white;
    }
}
