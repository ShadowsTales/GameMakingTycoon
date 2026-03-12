// ════════════════════════════════════════════════════════════════
//  NodeGraphController.Nodes.cs  — NODE-TYCOON
//
//  Builds the VisualElement card for each GameNode.
//  Uses GetFeatureData() throughout to avoid type-switches
//  on legacy aliases.
// ════════════════════════════════════════════════════════════════
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class NodeGraphController
{
    // ════════════════════════════════════════════════════════════════
    //  CARD BUILDER  (entry point)
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildNodeCard(GameNode node)
    {
        var card = new VisualElement();
        card.AddToClassList("node-card");
        card.AddToClassList("node-kind-" + node.Kind.ToString().ToLower());
        card.userData = node;

        card.Add(BuildNodeHeader(node));
        card.Add(BuildKindBadge(node));

        // Kind-specific stats block
        switch (node)
        {
            case GenreNode   gn:  card.Add(BuildGenreStats(gn));    break;
            case OptimizeNode on: card.Add(BuildOptimizerStats(on)); break;
            default:
                var feat = node.GetFeatureData();
                if (feat != null)
                    card.Add(BuildFeatureStats(feat, node.DevWeeks, node.PrimaryStatBonus));
                break;
        }

        card.Add(BuildPortBody(node));

        // Prerequisite warning
        var featureData = node.GetFeatureData();
        if (featureData?.prerequisites.Count > 0)
            card.Add(BuildPrereqWarning(featureData));

        // Events
        MakeNodeDraggable(card, node);
        card.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.clickCount == 2 && featureData != null)
                OpenFeaturePopupForNode(node.NodeId);
            else
            {
                evt.StopPropagation();
                SelectNode(node.NodeId);
            }
        });

        if (featureData != null)
        {
            card.RegisterCallback<MouseEnterEvent>(_ => ShowFeatureTooltip(featureData, card));
            card.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
        }

        return card;
    }

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

        if (node.Kind != NodeKind.Genre)
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
            NodeKind.Genre    => "● GENRE",
            NodeKind.System   => "■ SYSTEM",
            NodeKind.Feature  => "◆ FEATURE",
            NodeKind.Engine   => "▶ ENGINE",
            NodeKind.Support  => "◉ SUPPORT",
            NodeKind.Optimize => "⚙ OPTIMIZER",
            _                 => "",
        };

        var b = new Label(text) { pickingMode = PickingMode.Ignore };
        b.AddToClassList("node-kind-badge");
        b.AddToClassList("node-kind-badge-" + node.Kind.ToString().ToLower());
        return b;
    }

    // ════════════════════════════════════════════════════════════════
    //  STATS BLOCKS
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildGenreStats(GenreNode node)
    {
        var block = StatsBlock();
        block.Add(StatRow("Platform",  node.Platform));
        block.Add(StatRow("CPU Budget", $"{node.CpuBudget:0}"));
        block.Add(StatRow("Trigger",    node.TriggerLabel));
        block.Add(StatRow("Resolution", node.ResolutionLabel));

        var req = new Label(node.AllRequiredConnected ? "✓ Vollständig" : "⚠ Pflicht-Ports fehlen")
            { pickingMode = PickingMode.Ignore };
        req.AddToClassList(node.AllRequiredConnected ? "node-stat-ok" : "node-stat-warn");
        block.Add(req);

        return block;
    }

    private VisualElement BuildOptimizerStats(OptimizeNode node)
    {
        var block = StatsBlock();
        block.Add(StatRow("CPU Reduction", $"-{node.CpuReductionPercent:0}%"));
        block.Add(StatRow("Quality Bonus", $"+{node.QualityBonus * 100f:0}%"));
        block.Add(StatRow("Dev",           $"{node.DevTimeCost:0.0}w"));
        return block;
    }

    private VisualElement BuildFeatureStats(FeatureSO feat, float devWeeks, StatBonus bonus)
    {
        var block = StatsBlock();
        block.Add(StatRow("CPU",      $"{feat.cpuUsage:0}"));
        block.Add(StatRow("Dev",      $"{devWeeks:0.0}w"));
        block.Add(StatRow("Stat",     bonus.Label));
        block.Add(StatRow("TechDebt", $"{feat.techDebtRisk * 100f:0}%"));


        return block;
    }

    // ════════════════════════════════════════════════════════════════
    //  PORT BODY
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildPortBody(GameNode node)
    {
        var body = new VisualElement();
        body.AddToClassList("node-port-body");

        foreach (var port in node.InputPorts)
            body.Add(BuildPortRow(port));

        foreach (var port in node.OutputPorts)
            body.Add(BuildPortRow(port));

        return body;
    }

    private VisualElement BuildPortRow(NodePort port)
    {
        var row = new VisualElement();
        row.AddToClassList("port-row");
        row.AddToClassList(port.IsOutput ? "port-row--output" : "port-row--input");

        var dot = new VisualElement();
        dot.AddToClassList("port-dot");
        dot.AddToClassList(port.IsOutput ? "port-dot--output" : "port-dot--input");
        dot.userData = port;

        dot.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (!port.IsOutput) return;
            StartWire(port);
            evt.StopPropagation();
        });

        dot.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (port.IsOutput || !_isDrawingWire || _wireSource == null) return;
            _graph.TryConnect(_wireSource, port);
            CancelWire();
            evt.StopPropagation();
        });

        var label = new Label(port.Label) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("port-label");

        if (port.IsOutput) { row.Add(label); row.Add(dot); }
        else               { row.Add(dot);   row.Add(label); }

        return row;
    }

    // ════════════════════════════════════════════════════════════════
    //  PREREQUISITE WARNING
    // ════════════════════════════════════════════════════════════════

    private VisualElement BuildPrereqWarning(FeatureSO feat)
    {
        bool allMet = feat.prerequisites.All(req =>
            _graph.AllNodes.Any(n => n.GetFeatureData() == req));

        if (allMet) return new VisualElement(); // empty — no warning needed

        var names = feat.prerequisites
            .Where(req => !_graph.AllNodes.Any(n => n.GetFeatureData() == req))
            .Select(req => req.featureName);

        var warn = new Label($"⚠ Benötigt: {string.Join(", ", names)}")
            { pickingMode = PickingMode.Ignore };
        warn.AddToClassList("node-prereq-warning");
        return warn;
    }

    // ════════════════════════════════════════════════════════════════
    //  SMALL HELPERS
    // ════════════════════════════════════════════════════════════════

    private static VisualElement StatsBlock()
    {
        var b = new VisualElement();
        b.AddToClassList("node-stats-block");
        b.pickingMode = PickingMode.Ignore;
        return b;
    }

    private static VisualElement StatRow(string key, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("node-stat-row");
        row.pickingMode = PickingMode.Ignore;

        var k = new Label(key)   { pickingMode = PickingMode.Ignore };
        var v = new Label(value) { pickingMode = PickingMode.Ignore };
        k.AddToClassList("node-stat-key");
        v.AddToClassList("node-stat-val");
        row.Add(k);
        row.Add(v);
        return row;
    }
}