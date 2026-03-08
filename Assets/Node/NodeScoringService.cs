// ============================================================
//  NodeScoringService.cs  —  NODE-TYCOON  (Redesign v2)
//
//  CHANGES:
//    • GetTier() uses new NodeTierOverride names (CoreModule/Enhancement/Middleware)
//    • GetTierLabel() returns new studio vocabulary
//    • TagFilter() — filter features by GameplayTag
//    • GetSignalTagsText() — human-readable signal/requirement display
//    • Combo pair preview for the sidebar tooltip
// ============================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum NodeTier { CoreModule, Enhancement, Middleware, Unknown }

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
    //  GENRE FIT
    // ════════════════════════════════════════════════════════════════

    public float GenreFit(FeatureSO feature)
    {
        if (_genre == null) return 0.5f;
        float w = _genreWeights.TryGetValue(feature.category, out var v) ? v : 0.2f;
        float depthBonus = feature.prerequisites.Count > 0 ? 0.1f * w : 0f;
        return Mathf.Clamp01(w + depthBonus);
    }

    private static Dictionary<FeatureSO.FeatureCategory, float> BuildWeights(GenreSO genre)
    {
        if (genre == null) return new Dictionary<FeatureSO.FeatureCategory, float>();
        return new Dictionary<FeatureSO.FeatureCategory, float>
        {
            { FeatureSO.FeatureCategory.Gameplay,  genre.gameplayWeight },
            { FeatureSO.FeatureCategory.Graphic,   genre.graphicsWeight },
            { FeatureSO.FeatureCategory.Sound,     genre.soundWeight    },
            { FeatureSO.FeatureCategory.Tech,      genre.techWeight     },
            { FeatureSO.FeatureCategory.Narrative, genre.narrativeWeight},
            { FeatureSO.FeatureCategory.UX,        genre.uxWeight       },
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  SYNERGY BONUS
    // ════════════════════════════════════════════════════════════════

    public float SynergyBonus(FeatureSO feature, IEnumerable<FeatureSO> activeFeatures)
    {
        if (feature.synergyWith == null || !feature.synergyWith.Any()) return 0f;
        var activeSet = new HashSet<FeatureSO>(activeFeatures);
        int matches   = feature.synergyWith.Count(s => activeSet.Contains(s));
        return Mathf.Min(matches * 0.10f, 0.50f);
    }

    public string HasConflict(FeatureSO feature, IEnumerable<FeatureSO> activeFeatures)
    {
        if (feature.conflictsWith == null || !feature.conflictsWith.Any()) return null;
        var activeSet = new HashSet<FeatureSO>(activeFeatures);
        return feature.conflictsWith.FirstOrDefault(c => activeSet.Contains(c))?.featureName;
    }

    // ════════════════════════════════════════════════════════════════
    //  NODE TIER
    // ════════════════════════════════════════════════════════════════

    public NodeTier GetTier(FeatureSO feature)
    {
        return feature.tierOverride switch
        {
            NodeTierOverride.CoreModule   => NodeTier.CoreModule,
            NodeTierOverride.Enhancement  => NodeTier.Enhancement,
            NodeTierOverride.Middleware   => NodeTier.Middleware,
            _ => feature.isMiddleware                           ? NodeTier.Middleware
               : feature.prerequisites.Count == 0 && feature.canExpand ? NodeTier.CoreModule
               : feature.prerequisites.Count > 0               ? NodeTier.Enhancement
               :                                                  NodeTier.Unknown,
        };
    }

    public string GetTierLabel(FeatureSO feature)
    {
        return GetTier(feature) switch
        {
            NodeTier.CoreModule  => "[Core Module]",
            NodeTier.Enhancement => "[Enhancement]",
            NodeTier.Middleware  => "[Middleware]",
            _                   => "[Feature]",
        };
    }

    public string GetTierBadge(FeatureSO feature)
    {
        return GetTier(feature) switch
        {
            NodeTier.CoreModule  => "CORE",
            NodeTier.Enhancement => "ENH.",
            NodeTier.Middleware  => "MW",
            _                   => "FEAT",
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  QUALITY DELTA TEXT  (for sidebar tooltip and inspector)
    // ════════════════════════════════════════════════════════════════

    public string GetQualityDeltaText(FeatureSO feature, IEnumerable<FeatureSO> activeFeatures = null)
    {
        if (_genre == null) return "";

        float    fit     = GenreFit(feature);
        float    delta   = EstimateQualityDelta(feature, fit);
        float    synergy = activeFeatures != null ? SynergyBonus(feature, activeFeatures) : 0f;
        string tierLabel = GetTierLabel(feature);

        string fitLabel  = fit >= 0.7f ? "★ Recommended"
                         : fit <  0.3f ? "✗ Off-genre"
                                       : "○ Neutral";
        string synText   = synergy > 0f ? $" ⬆+{synergy * 100:0}% Cohesion" : "";
        return $"{tierLabel} {fitLabel}  Δ{delta:+0.0;-0.0}{synText}";
    }

    public float EstimateQualityDelta(FeatureSO feature, float fit = -1f)
    {
        if (fit < 0f) fit = GenreFit(feature);
        float tierMult = GetTier(feature) switch
        {
            NodeTier.CoreModule  => 1.5f,
            NodeTier.Enhancement => 0.6f,
            NodeTier.Middleware  => 1.1f,
            _                   => 1.0f,
        };
        return feature.cpuUsage * 0.08f * tierMult * (0.5f + fit);
    }

    // ════════════════════════════════════════════════════════════════
    //  TAG FILTERING
    // ════════════════════════════════════════════════════════════════

    /// <summary>Returns true if the feature has at least one of the provided gameplay tags.</summary>
    public static bool HasAnyTag(FeatureSO feature, IEnumerable<FeatureSO.GameplayTag> tags)
    {
        if (feature.signalTags == null || !feature.signalTags.Any()) return false;
        var tagSet = new HashSet<FeatureSO.GameplayTag>(tags);
        return feature.signalTags.Any(t => tagSet.Contains(t));
    }

    /// <summary>Returns all features that provide a specific GameplayTag signal.</summary>
    public List<FeatureSO> GetBySignalTag(FeatureSO.GameplayTag tag)
        => _db.allFeatures.Where(f => f.signalTags?.Contains(tag) == true).ToList();

    /// <summary>Returns all features that require a specific GameplayTag.</summary>
    public List<FeatureSO> GetByRequirementTag(FeatureSO.GameplayTag tag)
        => _db.allFeatures.Where(f => f.requiresTags?.Contains(tag) == true).ToList();

    // ════════════════════════════════════════════════════════════════
    //  SIGNAL / REQUIREMENT DISPLAY TEXT
    // ════════════════════════════════════════════════════════════════

    public static string GetSignalTagsText(FeatureSO feature)
    {
        if (feature.signalTags == null || feature.signalTags.Count == 0)
            return "—";
        return string.Join("  ·  ", feature.signalTags.Select(TagIcon));
    }

    public static string GetRequirementTagsText(FeatureSO feature)
    {
        if (feature.requiresTags == null || feature.requiresTags.Count == 0)
            return "None";
        return string.Join("  ·  ", feature.requiresTags.Select(TagIcon));
    }

    public static string TagIcon(FeatureSO.GameplayTag tag) => tag switch
    {
        FeatureSO.GameplayTag.Locomotion       => "🚶 Locomotion",
        FeatureSO.GameplayTag.Jump             => "↑ Jump",
        FeatureSO.GameplayTag.Dash             => "→ Dash",
        FeatureSO.GameplayTag.Climb            => "⬆ Climb",
        FeatureSO.GameplayTag.Swim             => "〰 Swim",
        FeatureSO.GameplayTag.Flight           => "✈ Flight",
        FeatureSO.GameplayTag.VehicleControl   => "🚗 Vehicle",
        FeatureSO.GameplayTag.MeleeCombat      => "⚔ Melee",
        FeatureSO.GameplayTag.RangedCombat     => "🏹 Ranged",
        FeatureSO.GameplayTag.Stealth          => "👤 Stealth",
        FeatureSO.GameplayTag.Parry            => "🛡 Parry",
        FeatureSO.GameplayTag.ComboSystem      => "✕ Combo",
        FeatureSO.GameplayTag.AoEAttack        => "💥 AoE",
        FeatureSO.GameplayTag.BossEncounter    => "☠ Boss",
        FeatureSO.GameplayTag.XPSystem         => "⭐ XP",
        FeatureSO.GameplayTag.LevelUp          => "▲ LevelUp",
        FeatureSO.GameplayTag.SkillTree        => "🌿 SkillTree",
        FeatureSO.GameplayTag.Inventory        => "🎒 Inventory",
        FeatureSO.GameplayTag.Crafting         => "🔨 Crafting",
        FeatureSO.GameplayTag.Loot             => "💎 Loot",
        FeatureSO.GameplayTag.OpenWorld        => "🌍 OpenWorld",
        FeatureSO.GameplayTag.LinearLevel      => "➡ Linear",
        FeatureSO.GameplayTag.Procedural       => "⚄ Procedural",
        FeatureSO.GameplayTag.MapSystem        => "🗺 Map",
        FeatureSO.GameplayTag.FastTravel       => "⚡ FastTravel",
        FeatureSO.GameplayTag.Destructible     => "💢 Destructible",
        FeatureSO.GameplayTag.ResourceGathering=> "⛏ Resources",
        FeatureSO.GameplayTag.Trading          => "💱 Trading",
        FeatureSO.GameplayTag.Currency         => "💰 Currency",
        FeatureSO.GameplayTag.Economy          => "📈 Economy",
        FeatureSO.GameplayTag.EnemyAI          => "🤖 EnemyAI",
        FeatureSO.GameplayTag.CompanionAI      => "👥 Companion",
        FeatureSO.GameplayTag.PathfindingAI    => "🧭 Pathfinding",
        FeatureSO.GameplayTag.DynamicDifficulty=> "⚖ DynDiff",
        FeatureSO.GameplayTag.Dialogue         => "💬 Dialogue",
        FeatureSO.GameplayTag.StoryBranching   => "🌿 Branching",
        FeatureSO.GameplayTag.QuestSystem      => "📋 Quests",
        FeatureSO.GameplayTag.Cutscene         => "🎬 Cutscene",
        FeatureSO.GameplayTag.Journal          => "📓 Journal",
        FeatureSO.GameplayTag.SoundFX         => "🔔 SFX",
        FeatureSO.GameplayTag.Music           => "🎵 Music",
        FeatureSO.GameplayTag.VoiceActing     => "🎤 Voice",
        FeatureSO.GameplayTag.DynamicAudio    => "🎚 DynAudio",
        FeatureSO.GameplayTag.Rendering2D     => "🖼 2D",
        FeatureSO.GameplayTag.Rendering3D     => "🧊 3D",
        FeatureSO.GameplayTag.PixelArt        => "🎮 PixelArt",
        FeatureSO.GameplayTag.Particles       => "✨ Particles",
        FeatureSO.GameplayTag.Lighting        => "💡 Lighting",
        FeatureSO.GameplayTag.Shaders         => "🌈 Shaders",
        FeatureSO.GameplayTag.Physics         => "⚙ Physics",
        FeatureSO.GameplayTag.Networking      => "🌐 Network",
        FeatureSO.GameplayTag.SaveSystem      => "💾 Save",
        FeatureSO.GameplayTag.Analytics       => "📊 Analytics",
        FeatureSO.GameplayTag.Localisation    => "🌐 L10n",
        FeatureSO.GameplayTag.HUD             => "📱 HUD",
        FeatureSO.GameplayTag.Minimap         => "🗺 Minimap",
        FeatureSO.GameplayTag.PauseMenu       => "⏸ Pause",
        FeatureSO.GameplayTag.Tutorial        => "📖 Tutorial",
        FeatureSO.GameplayTag.Accessibility   => "♿ Access.",
        FeatureSO.GameplayTag.AchievementSystem => "🏆 Achieve.",
        _ => tag.ToString(),
    };

    // ════════════════════════════════════════════════════════════════
    //  DOMAIN SOCKET LABEL
    // ════════════════════════════════════════════════════════════════

    public static string GetDomainLabel(FeatureSO feature) => feature.domainSocket switch
    {
        FeatureSO.DomainSocket.GP   => "⚡ GP  — Gameplay",
        FeatureSO.DomainSocket.GFX  => "🎨 GFX — Visual",
        FeatureSO.DomainSocket.SND  => "🔊 SND — Audio",
        FeatureSO.DomainSocket.TECH => "⚙ TECH — Technical",
        FeatureSO.DomainSocket.NARR => "📖 NARR — Narrative",
        _                          => "◈ Any Socket",
    };
}