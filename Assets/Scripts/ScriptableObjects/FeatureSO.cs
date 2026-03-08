// ============================================================
//  FeatureSO.cs  —  NODE-TYCOON  (Redesign v2)
//
//  CHANGES:
//    • GameplayTag enum — clear, gameplay-oriented labels
//      for what a feature PROVIDES and REQUIRES
//    • signalTags   — what this feature outputs (Signals)
//    • requiresTags — what this feature needs as input (Requirements)
//    • domainSocket — which Root Project Node socket accepts this
//    • Middleware flag for third-party cost
//    • techDebtRisk — how likely this feature is to cause bugs
// ============================================================
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFeature", menuName = "NodeTycoon/Feature")]
public class FeatureSO : ScriptableObject
{
    // ── IMPORTANT: never reorder or insert — existing .asset files
    //   store the integer index. New values go at the END only.
    public enum FeatureCategory
    {
        Gameplay  = 0,
        Graphic   = 1,
        Sound     = 2,
        Tech      = 3,
        Narrative = 4,
        UX        = 5,
    }

    // ── Gameplay-oriented signal tags ─────────────────────────────
    // These describe what a feature PRODUCES or NEEDS in gameplay terms.
    // Used for filter, Bridge logic, and Cohesion scoring.
    public enum GameplayTag
    {
        // ── Locomotion / Input
        Locomotion,         // walking, running, movement base
        Jump,               // vertical movement
        Dash,               // quick directional burst
        Climb,              // vertical traversal
        Swim,               // liquid traversal
        Flight,             // aerial movement
        VehicleControl,     // driving / piloting

        // ── Combat
        MeleeCombat,        // close-range attack
        RangedCombat,       // projectile / gun attack
        Stealth,            // avoidance / sneak
        Parry,              // defensive counter
        ComboSystem,        // chained attack logic
        AoEAttack,          // area of effect damage
        BossEncounter,      // special enemy logic

        // ── Progression
        XPSystem,           // experience points
        LevelUp,            // character advancement
        SkillTree,          // branching upgrades
        Inventory,          // item management
        Crafting,           // item creation
        Loot,               // drop system

        // ── World / Map
        OpenWorld,          // free exploration
        LinearLevel,        // corridor design
        Procedural,         // generated content
        MapSystem,          // mini/world map
        FastTravel,         // teleport between zones
        Destructible,       // breakable environment

        // ── Economy
        ResourceGathering,  // collecting materials
        Trading,            // buy/sell system
        Currency,           // money / points
        Economy,            // supply/demand simulation

        // ── AI / Enemies
        EnemyAI,            // basic foe behaviour
        CompanionAI,        // ally behaviour
        PathfindingAI,      // navigation mesh
        DynamicDifficulty,  // adapts to player skill

        // ── Narrative
        Dialogue,           // conversation system
        StoryBranching,     // player choices
        QuestSystem,        // mission tracking
        Cutscene,           // cinematic moment
        Journal,            // lore / log

        // ── Audio
        SoundFX,            // game sound effects
        Music,              // background score
        VoiceActing,        // spoken dialogue
        DynamicAudio,       // reactive sound system

        // ── Visual / Rendering
        Rendering2D,        // 2D pipeline
        Rendering3D,        // 3D pipeline
        PixelArt,           // retro visual style
        Particles,          // VFX system
        Lighting,           // dynamic/static light
        Shaders,            // custom material logic

        // ── Tech / Engine
        Physics,            // physics simulation
        Networking,         // multiplayer sync
        SaveSystem,         // persistence
        Analytics,          // telemetry
        Localisation,       // multi-language

        // ── UX / UI
        HUD,                // heads-up display
        Minimap,            // spatial awareness
        PauseMenu,          // game flow control
        Tutorial,           // onboarding
        Accessibility,      // inclusive design
        AchievementSystem,  // reward tracking
    }

    // ── Which Root socket this feature connects to ─────────────────
    public enum DomainSocket
    {
        GP,     // Gameplay Socket
        GFX,    // Visual Socket
        SND,    // Audio Socket
        TECH,   // Technical Socket
        NARR,   // Narrative Socket
        Any,    // flexible (Bridge nodes, etc.)
    }

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ══════════════════════════════════════════════════════════════

    [Header("── Identity ──────────────────────────────────────────")]
    public string featureName;
    public FeatureCategory category;
    [TextArea(2, 4)] public string description;

    [Header("── Domain & Signals ──────────────────────────────────")]
    [Tooltip("Which Root Project Node socket accepts this feature.")]
    public DomainSocket domainSocket = DomainSocket.Any;

    [Tooltip("What this feature PRODUCES — other nodes can consume these.")]
    public List<GameplayTag> signalTags = new List<GameplayTag>();

    [Tooltip("What this feature REQUIRES from connected upstream nodes.")]
    public List<GameplayTag> requiresTags = new List<GameplayTag>();

    [Header("── Costs ─────────────────────────────────────────────")]
    public float cpuUsage;
    public float ramUsage;
    public float researchCostPoints;

    [Range(0f, 1f)]
    [Tooltip("How much technical debt / bug risk this feature introduces (0=clean, 1=risky).")]
    public float techDebtRisk = 0f;

    [Header("── Middleware ────────────────────────────────────────")]
    [Tooltip("Is this a licensed/third-party tool with a royalty cost?")]
    public bool isMiddleware = false;
    [Tooltip("Per-game royalty cost in $ when this feature is used.")]
    public float middlewareCostPerGame = 0f;

    [Header("── Research ─────────────────────────────────────────")]
    public bool isResearched;
    public int  releaseYear;
    public bool canExpand;

    [Header("── Node Tier ────────────────────────────────────────")]
    [Tooltip("Leave Default to auto-infer. Core Module = backbone. Enhancement = add-on. Middleware = licensed.")]
    public NodeTierOverride tierOverride = NodeTierOverride.Default;

    [Header("── Dependencies ────────────────────────────────────")]
    public List<FeatureSO> prerequisites = new List<FeatureSO>();

    [Header("── Synergies & Conflicts ──────────────────────────")]
    [Tooltip("Features that give a quality BONUS when both present in the graph.")]
    public List<FeatureSO> synergyWith   = new List<FeatureSO>();
    [Tooltip("Features that CANNOT coexist — placing both triggers a validation error.")]
    public List<FeatureSO> conflictsWith = new List<FeatureSO>();

    // Legacy — kept so old .asset files don't lose data
    [HideInInspector] public int yearAvailable;
}

// Separate enum — FeatureSO doesn't depend on NodeTier (lives in NodeScoringService)
public enum NodeTierOverride { Default, CoreModule, Enhancement, Middleware }