// ============================================================
//  FeatureNode.cs  —  NODE-TYCOON
//
//  UI tree node used by NodeConnectionService and
//  ResearchTreeController to track the visual research tree.
//
//  This is NOT a graph node — it wraps a VisualElement and
//  FeatureSO for drawing Bezier connections in the research
//  board. Completely separate from GameNode / GameFeatureNode.
// ============================================================
using UnityEngine.UIElements;
using System.Collections.Generic;

public class FeatureNode
{
    public VisualElement        Element;
    public FeatureSO            Feature;
    public FeatureNode          Parent;
    public List<FeatureNode>    Children       = new List<FeatureNode>();
    public VisualElement        ConnectionLine;

    public FeatureNode(VisualElement element, FeatureSO feature)
    {
        Element = element;
        Feature = feature;
    }
}
