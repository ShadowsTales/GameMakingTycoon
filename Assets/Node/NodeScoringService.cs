// ════════════════════════════════════════════════════════════════
//  NodeScoringService.cs
//
//  CHANGES vs previous version:
//    • BuildWeights() now maps all 6 FeatureCategory values
//    • SynergyBonus()  — quality multiplier when synergy pairs present
//    • HasConflict()   — used by NodeGraph validation
//    • GetTier() now respects FeatureSO.tierOverride
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NodeScoringService
{
    private readonly FeatureDatabase _db;
    private readonly GenreSO         _genre;
    private readonly Dictionary<FeatureSO.FeatureCategory, float> _genreWeights;

    public NodeScoringService(FeatureDatabase db, GenreSO genre)
    {
        _db           = db;
        _genre        = genre;
        _genreWeights = BuildWeights(genre);
    }

    // ════════════════════════════════════════════════════════════════
    //  GENRE FIT  (soft filter — never locks the player out)
    // ════════════════════════════════════════════════════════════════

    public float GenreFit(FeatureSO feature)
    {
        if (_genre == null) return 0.5f;
        float w = _genreWeights.TryGetValue(feature.category, out var v) ? v : 0.2f;
        float depthBonus = feature.prerequisites.Count > 0 ? 0.1f * w : 0f;
        return Mathf.Clamp01(w + depthBonus);
    }

    // ════════════════════════════════════════════════════════════════
    //  SYNERGY BONUS
    //  Extra multiplier (0–0.5) when synergy partners are both active.
    // ════════════════════════════════════════════════════════════════

    public float SynergyBonus(FeatureSO feature, IEnumerable<FeatureSO> activeFeatures)
    {
        if (feature.synergyWith == null || !feature.synergyWith.Any()) return 0f;
        var activeSet = new HashSet<FeatureSO>(activeFeatures);
        int matches   = feature.synergyWith.Count(s => activeSet.Contains(s));
        return Mathf.Min(matches * 0.10f, 0.50f);
    }

    // ════════════════════════════════════════════════════════════════
    //  CONFLICT CHECK
    //  Returns name of first conflicting active feature, or null.
    // ════════════════════════════════════════════════════════════════

    public string HasConflict(FeatureSO feature, IEnumerable<FeatureSO> activeFeatures)
    {
        if (feature.conflictsWith == null || !feature.conflictsWith.Any()) return null;
        var activeSet = new HashSet<FeatureSO>(activeFeatures);
        return feature.conflictsWith.FirstOrDefault(c => activeSet.Contains(c))?.featureName;
    }

    // ════════════════════════════════════════════════════════════════
    //  QUALITY DELTA  (inspector + node card preview)
    // ════════════════════════════════════════════════════════════════

    public string GetQualityDeltaText(FeatureSO feature, IEnumerable<FeatureSO> activeFeatures = null)
    {
        if (_genre == null) return "";

        float    fit     = GenreFit(feature);
        float    delta   = EstimateQualityDelta(feature, fit);
        float    synergy = activeFeatures != null ? SynergyBonus(feature, activeFeatures) : 0f;
        NodeTier tier    = GetTier(feature);

        string fitLabel  = fit >= 0.7f ? "★ Empfohlen"
                         : fit <  0.3f ? "✗ Schwach"
                                       : "○ Neutral";
        string tierLabel = tier switch
        {
            NodeTier.Core        => "[Kern]",
            NodeTier.Enhancement => "[Erw.]",
            _                    => "[Feature]",
        };
        string synText = synergy > 0f ? $" ⬆+{synergy*100:0}%" : "";
        return $"{tierLabel} {fitLabel}  Δ{delta:+0.0;-0.0}{synText}";
    }

    public float EstimateQualityDelta(FeatureSO feature, float fit = -1f)
    {
        if (fit < 0f) fit = GenreFit(feature);
        float tierMult = GetTier(feature) switch
        {
            NodeTier.Core        => 1.5f,
            NodeTier.Enhancement => 0.6f,
            _                    => 1.0f,
        };
        return feature.cpuUsage * 0.08f * tierMult * (0.5f + fit);
    }

    // ════════════════════════════════════════════════════════════════
    //  NODE TIER — designer override wins, otherwise inferred
    // ════════════════════════════════════════════════════════════════

    public NodeTier GetTier(FeatureSO feature)
    {
        return feature.tierOverride switch
        {
            NodeTierOverride.Core        => NodeTier.Core,
            NodeTierOverride.Feature     => NodeTier.Feature,
            NodeTierOverride.Enhancement => NodeTier.Enhancement,
            _ => feature.prerequisites.Count == 0 && feature.canExpand ? NodeTier.Core
               : feature.prerequisites.Count > 0                       ? NodeTier.Enhancement
               :                                                          NodeTier.Feature,
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  BUILD WEIGHTS — all 6 categories
    // ════════════════════════════════════════════════════════════════

    private static Dictionary<FeatureSO.FeatureCategory, float> BuildWeights(GenreSO genre)
    {
        if (genre == null)
            return new Dictionary<FeatureSO.FeatureCategory, float>
            {
                { FeatureSO.FeatureCategory.Gameplay,  0.20f },
                { FeatureSO.FeatureCategory.Graphic,   0.20f },
                { FeatureSO.FeatureCategory.Sound,     0.20f },
                { FeatureSO.FeatureCategory.Tech,      0.20f },
                { FeatureSO.FeatureCategory.Narrative, 0.10f },
                { FeatureSO.FeatureCategory.UX,        0.10f },
            };

        return new Dictionary<FeatureSO.FeatureCategory, float>
        {
            { FeatureSO.FeatureCategory.Gameplay,  genre.gameplayWeight  },
            { FeatureSO.FeatureCategory.Graphic,   genre.graphicsWeight  },
            { FeatureSO.FeatureCategory.Sound,     genre.soundWeight     },
            { FeatureSO.FeatureCategory.Tech,      genre.techWeight      },
            { FeatureSO.FeatureCategory.Narrative, genre.narrativeWeight },
            { FeatureSO.FeatureCategory.UX,        genre.uxWeight        },
        };
    }
}

public enum NodeTier { Core, Feature, Enhancement }