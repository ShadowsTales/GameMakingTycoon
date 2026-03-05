using UnityEngine.UIElements;
using System.Collections.Generic;

public class FeatureNode
{
    public VisualElement Element;
    public FeatureSO Feature;
    public FeatureNode Parent;
    public List<FeatureNode> Children = new List<FeatureNode>();
    public VisualElement ConnectionLine;

    public FeatureNode(VisualElement element, FeatureSO feature)
    {
        Element = element;
        Feature = feature;
    }
}