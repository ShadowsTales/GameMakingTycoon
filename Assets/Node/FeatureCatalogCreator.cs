// ============================================================
//  FeatureCatalogCreator.cs  —  NODE-TYCOON  (Redesign v2)
//
//  Editor tool — creates all FeatureSO assets with:
//    • DomainSocket routing
//    • signalTags (what this feature produces)
//    • requiresTags (what this feature needs upstream)
//    • tierOverride (CoreModule / Enhancement / Middleware)
//    • techDebtRisk, isMiddleware, middlewareCostPerGame
//    • Synergies and conflicts
//
//  Run via: Tools → NodeTycoon → Rebuild Feature Catalog
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FeatureCatalogCreator : EditorWindow
{
    private const string OUTPUT_PATH = "Assets/Data/Features";

    [MenuItem("Tools/NodeTycoon/Rebuild Feature Catalog")]
    public static void RebuildAll()
    {
        var window = GetWindow<FeatureCatalogCreator>("Rebuild Feature Catalog");
        window.DoRebuild();
        window.Close();
    }

    private void DoRebuild()
    {
        if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
            AssetDatabase.CreateFolder("Assets/Data", "Features");

        // Create category sub-folders
        foreach (var cat in System.Enum.GetNames(typeof(FeatureSO.FeatureCategory)))
        {
            string sub = Path.Combine(OUTPUT_PATH, cat);
            if (!AssetDatabase.IsValidFolder(sub))
                AssetDatabase.CreateFolder(OUTPUT_PATH, cat);
        }

        var all     = FeatureCatalog().ToList();
        int created = 0;

        // Pass 1: create all assets
        foreach (var entry in all)
        {
            string dir  = Path.Combine(OUTPUT_PATH, entry.Category.ToString());
            string path = Path.Combine(dir, entry.Name + ".asset");

            var so = AssetDatabase.LoadAssetAtPath<FeatureSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<FeatureSO>();
                AssetDatabase.CreateAsset(so, path);
                created++;
            }

            so.featureName          = entry.Name;
            so.category             = entry.Category;
            so.description          = entry.Description;
            so.cpuUsage             = entry.Cpu;
            so.ramUsage             = entry.Ram;
            so.releaseYear          = entry.Year;
            so.canExpand            = entry.CanExpand;
            so.tierOverride         = entry.Tier;
            so.researchCostPoints   = entry.Research;
            so.isResearched         = entry.Year <= 1977; // early era auto-unlocked
            so.domainSocket         = entry.Domain;
            so.techDebtRisk         = entry.TechDebt;
            so.isMiddleware         = entry.IsMiddleware;
            so.middlewareCostPerGame= entry.MiddlewareCost;

            so.signalTags    = new List<FeatureSO.GameplayTag>(entry.SignalTags ?? System.Array.Empty<FeatureSO.GameplayTag>());
            so.requiresTags  = new List<FeatureSO.GameplayTag>(entry.RequiresTags ?? System.Array.Empty<FeatureSO.GameplayTag>());

            EditorUtility.SetDirty(so);
        }

        AssetDatabase.SaveAssets();

        // Pass 2: wire prerequisites
        foreach (var entry in all.Where(e => e.Prerequisites?.Length > 0))
        {
            var so = LoadFeature(entry.Name, entry.Category);
            if (so == null) continue;
            so.prerequisites.Clear();
            foreach (var pn in entry.Prerequisites)
            {
                var found = FindAnyFeature(pn, all);
                if (found != null) so.prerequisites.Add(found);
            }
            EditorUtility.SetDirty(so);
        }

        // Pass 3: wire synergies
        foreach (var entry in all.Where(e => e.Synergies?.Length > 0))
        {
            var so = LoadFeature(entry.Name, entry.Category);
            if (so == null) continue;
            so.synergyWith.Clear();
            foreach (var sn in entry.Synergies)
            {
                var found = FindAnyFeature(sn, all);
                if (found != null) so.synergyWith.Add(found);
            }
            EditorUtility.SetDirty(so);
        }

        // Pass 4: wire conflicts
        foreach (var entry in all.Where(e => e.Conflicts?.Length > 0))
        {
            var so = LoadFeature(entry.Name, entry.Category);
            if (so == null) continue;
            so.conflictsWith.Clear();
            foreach (var cn in entry.Conflicts)
            {
                var found = FindAnyFeature(cn, all);
                if (found != null) so.conflictsWith.Add(found);
            }
            EditorUtility.SetDirty(so);
        }

        // Update FeatureDatabase
        var db = AssetDatabase.LoadAssetAtPath<FeatureDatabase>("Assets/Data/FeatureDatabase.asset");
        if (db != null)
        {
            db.allFeatures = AssetDatabase.FindAssets("t:FeatureSO", new[] { OUTPUT_PATH })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(f => f != null).ToList();
            EditorUtility.SetDirty(db);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FeatureCatalog] ✓ {created} new features created. Total: {all.Count}.");
    }

    private FeatureSO LoadFeature(string name, FeatureSO.FeatureCategory cat)
    {
        string path = Path.Combine(OUTPUT_PATH, cat.ToString(), name + ".asset");
        return AssetDatabase.LoadAssetAtPath<FeatureSO>(path);
    }

    private FeatureSO FindAnyFeature(string name, List<FeatureEntry> all)
    {
        var entry = all.Find(e => e.Name == name);
        if (entry == null) return null;
        return LoadFeature(name, entry.Category);
    }

    // ════════════════════════════════════════════════════════════════
    //  ENTRY DATA CLASS
    // ════════════════════════════════════════════════════════════════
    private class FeatureEntry
    {
        public string                    Name;
        public FeatureSO.FeatureCategory Category;
        public string                    Description;
        public float                     Cpu;
        public float                     Ram;
        public int                       Year;
        public bool                      CanExpand;
        public NodeTierOverride          Tier;
        public float                     Research;
        public FeatureSO.DomainSocket    Domain;
        public float                     TechDebt;
        public bool                      IsMiddleware;
        public float                     MiddlewareCost;
        public FeatureSO.GameplayTag[]   SignalTags;
        public FeatureSO.GameplayTag[]   RequiresTags;
        public string[]                  Prerequisites;
        public string[]                  Synergies;
        public string[]                  Conflicts;
    }

    // ════════════════════════════════════════════════════════════════
    //  THE CATALOG
    // ════════════════════════════════════════════════════════════════
    private static IEnumerable<FeatureEntry> FeatureCatalog()
    {
        // ── GAMEPLAY — LOCOMOTION / CORE MODULES ─────────────────

        yield return new FeatureEntry
        {
            Name = "Basic Locomotion", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Player walks and runs in 2D or 3D space. The foundational movement system.",
            Cpu = 18, Ram = 3, Year = 1972, CanExpand = true, Research = 0,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.Locomotion },
            Synergies    = new[] { "Jump System", "Dash System", "Platformer Level Design" },
        };

        yield return new FeatureEntry
        {
            Name = "Jump System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Player can jump. Supports variable height and coyote time.",
            Cpu = 10, Ram = 1, Year = 1972, CanExpand = true, Research = 0,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Jump },
            RequiresTags = new[] { FeatureSO.GameplayTag.Locomotion },
            Prerequisites = new[] { "Basic Locomotion" },
            Synergies    = new[] { "Dash System", "Double Jump", "Wall Jump" },
        };

        yield return new FeatureEntry
        {
            Name = "Double Jump", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Player can jump a second time while airborne. Classic platformer feel.",
            Cpu = 5, Ram = 0, Year = 1985, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Jump },
            RequiresTags = new[] { FeatureSO.GameplayTag.Jump },
            Prerequisites = new[] { "Jump System" },
            Synergies    = new[] { "Dash System", "Wall Jump" },
        };

        yield return new FeatureEntry
        {
            Name = "Wall Jump", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Player can jump off walls. Adds vertical traversal and expressive movement.",
            Cpu = 7, Ram = 1, Year = 1987, CanExpand = false, Research = 10,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Jump, FeatureSO.GameplayTag.Climb },
            RequiresTags = new[] { FeatureSO.GameplayTag.Jump },
            Prerequisites = new[] { "Jump System" },
        };

        yield return new FeatureEntry
        {
            Name = "Dash System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Quick directional burst movement. Adds tempo and expression to traversal.",
            Cpu = 8, Ram = 1, Year = 1992, CanExpand = true, Research = 10,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Dash },
            RequiresTags = new[] { FeatureSO.GameplayTag.Locomotion },
            Prerequisites = new[] { "Basic Locomotion" },
            Synergies    = new[] { "Combo System", "Jump System", "Dodge Roll" },
        };

        yield return new FeatureEntry
        {
            Name = "Dodge Roll", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Invincibility frames during a roll. Iconic in action-RPGs.",
            Cpu = 8, Ram = 1, Year = 1994, CanExpand = false, Research = 12,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Dash },
            RequiresTags = new[] { FeatureSO.GameplayTag.Locomotion },
            Prerequisites = new[] { "Basic Locomotion" },
            Synergies    = new[] { "Melee Combat", "Dash System" },
        };

        yield return new FeatureEntry
        {
            Name = "Climbing System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Player can climb ledges, ladders, and walls. Adds vertical world design.",
            Cpu = 14, Ram = 2, Year = 1986, CanExpand = true, Research = 15,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Climb },
            RequiresTags = new[] { FeatureSO.GameplayTag.Locomotion },
            Prerequisites = new[] { "Basic Locomotion" },
            Synergies    = new[] { "Open World", "Wall Jump" },
        };

        yield return new FeatureEntry
        {
            Name = "Vehicle Control", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Drive cars, boats or spaceships. Needs a dedicated physics model.",
            Cpu = 22, Ram = 4, Year = 1983, CanExpand = true, Research = 20,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.VehicleControl, FeatureSO.GameplayTag.Locomotion },
            Synergies    = new[] { "Physics Engine", "Open World" },
            Conflicts    = new[] { "Basic Locomotion" },
        };

        yield return new FeatureEntry
        {
            Name = "Flight System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Full 3D flight. Ideal for space sims or aerial adventure games.",
            Cpu = 20, Ram = 3, Year = 1984, CanExpand = true, Research = 20,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.Flight, FeatureSO.GameplayTag.Locomotion },
            Synergies    = new[] { "Open World", "Physics Engine" },
        };

        // ── GAMEPLAY — COMBAT ─────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "Melee Combat", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Sword, fist, or blunt weapons in close range. Core action system.",
            Cpu = 20, Ram = 3, Year = 1980, CanExpand = true, Research = 5,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.MeleeCombat },
            RequiresTags = new[] { FeatureSO.GameplayTag.Locomotion },
            Synergies    = new[] { "Combo System", "Parry & Block", "Enemy AI", "Boss Encounter" },
        };

        yield return new FeatureEntry
        {
            Name = "Ranged Combat", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Bows, guns or magic projectiles. Requires aiming input.",
            Cpu = 18, Ram = 3, Year = 1981, CanExpand = true, Research = 8,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.RangedCombat },
            Synergies    = new[] { "Enemy AI", "Cover System", "Ammo System" },
        };

        yield return new FeatureEntry
        {
            Name = "Combo System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Chained attack sequences. Reward skill with increasing damage or style.",
            Cpu = 12, Ram = 2, Year = 1991, CanExpand = true, Research = 15,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.ComboSystem, FeatureSO.GameplayTag.MeleeCombat },
            RequiresTags = new[] { FeatureSO.GameplayTag.MeleeCombat },
            Prerequisites = new[] { "Melee Combat" },
            Synergies    = new[] { "Dash System", "Boss Encounter", "Style Meter" },
        };

        yield return new FeatureEntry
        {
            Name = "Parry & Block", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Perfect timing block or parry. Creates high-skill counterplay moments.",
            Cpu = 10, Ram = 1, Year = 1993, CanExpand = false, Research = 12,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Parry, FeatureSO.GameplayTag.MeleeCombat },
            RequiresTags = new[] { FeatureSO.GameplayTag.MeleeCombat },
            Prerequisites = new[] { "Melee Combat" },
            Synergies    = new[] { "Combo System", "Boss Encounter" },
        };

        yield return new FeatureEntry
        {
            Name = "Stealth System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Player can hide in shadows and sneak past enemies. Needs enemy AI.",
            Cpu = 16, Ram = 3, Year = 1987, CanExpand = true, Research = 18,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.Stealth },
            RequiresTags = new[] { FeatureSO.GameplayTag.EnemyAI },
            Prerequisites = new[] { "Enemy AI" },
            Synergies    = new[] { "Enemy AI", "Pathfinding AI" },
            Conflicts    = new[] { "Melee Combat" },
        };

        yield return new FeatureEntry
        {
            Name = "AoE Attack", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Area-of-effect explosion, shockwave, or spell. Hits multiple targets.",
            Cpu = 10, Ram = 2, Year = 1994, CanExpand = false, Research = 14,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.AoEAttack },
            RequiresTags = new[] { FeatureSO.GameplayTag.MeleeCombat, FeatureSO.GameplayTag.RangedCombat },
            Synergies    = new[] { "Enemy AI", "Boss Encounter" },
        };

        yield return new FeatureEntry
        {
            Name = "Boss Encounter", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Unique boss enemy with custom phase logic and special attacks.",
            Cpu = 22, Ram = 4, Year = 1981, CanExpand = true, Research = 18,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.BossEncounter, FeatureSO.GameplayTag.EnemyAI },
            RequiresTags = new[] { FeatureSO.GameplayTag.MeleeCombat },
            Synergies    = new[] { "Combo System", "Parry & Block", "Dynamic Music" },
        };

        yield return new FeatureEntry
        {
            Name = "Style Meter", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Ranks player performance in combat. Higher rank = more power. (Devil May Cry feel.)",
            Cpu = 8, Ram = 1, Year = 2001, CanExpand = false, Research = 20,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.ComboSystem },
            RequiresTags = new[] { FeatureSO.GameplayTag.ComboSystem },
            Prerequisites = new[] { "Combo System" },
        };

        yield return new FeatureEntry
        {
            Name = "Cover System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Snap to cover and peek to shoot. Tactical third-person gunplay.",
            Cpu = 14, Ram = 2, Year = 2006, CanExpand = false, Research = 18,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.RangedCombat, FeatureSO.GameplayTag.Stealth },
            RequiresTags = new[] { FeatureSO.GameplayTag.RangedCombat },
            Prerequisites = new[] { "Ranged Combat" },
        };

        yield return new FeatureEntry
        {
            Name = "Ammo System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Limited ammunition that requires reloading or finding pickups.",
            Cpu = 5, Ram = 1, Year = 1993, CanExpand = false, Research = 5,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.RangedCombat, FeatureSO.GameplayTag.Inventory },
            RequiresTags = new[] { FeatureSO.GameplayTag.RangedCombat },
            Prerequisites = new[] { "Ranged Combat" },
        };

        // ── GAMEPLAY — PROGRESSION ───────────────────────────────

        yield return new FeatureEntry
        {
            Name = "XP & Leveling", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Gain experience and level up to improve stats. Backbone of RPG progression.",
            Cpu = 15, Ram = 2, Year = 1975, CanExpand = true, Research = 10,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.XPSystem, FeatureSO.GameplayTag.LevelUp },
            Synergies  = new[] { "Skill Tree", "Inventory System", "Quest System" },
        };

        yield return new FeatureEntry
        {
            Name = "Skill Tree", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Branching upgrade paths. Player invests points into abilities.",
            Cpu = 12, Ram = 2, Year = 1997, CanExpand = true, Research = 15,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.SkillTree },
            RequiresTags = new[] { FeatureSO.GameplayTag.XPSystem },
            Prerequisites = new[] { "XP & Leveling" },
            Synergies    = new[] { "XP & Leveling", "Quest System" },
        };

        yield return new FeatureEntry
        {
            Name = "Inventory System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Item slots, weight management, equipment. Core RPG infrastructure.",
            Cpu = 14, Ram = 4, Year = 1980, CanExpand = true, Research = 10,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Inventory, FeatureSO.GameplayTag.Loot },
            Synergies  = new[] { "Crafting System", "Loot System", "Trading System" },
        };

        yield return new FeatureEntry
        {
            Name = "Loot System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Enemies and chests drop randomised loot. The core dopamine loop.",
            Cpu = 10, Ram = 2, Year = 1985, CanExpand = true, Research = 12,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Loot },
            RequiresTags = new[] { FeatureSO.GameplayTag.Inventory },
            Prerequisites = new[] { "Inventory System" },
            Synergies    = new[] { "Enemy AI", "Inventory System" },
        };

        yield return new FeatureEntry
        {
            Name = "Crafting System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Combine gathered materials into weapons, potions, or gear.",
            Cpu = 16, Ram = 3, Year = 1998, CanExpand = true, Research = 18,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Crafting },
            RequiresTags = new[] { FeatureSO.GameplayTag.Inventory, FeatureSO.GameplayTag.ResourceGathering },
            Prerequisites = new[] { "Inventory System", "Resource Gathering" },
            Synergies    = new[] { "Resource Gathering", "Inventory System", "Trading System" },
        };

        yield return new FeatureEntry
        {
            Name = "Resource Gathering", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Harvest wood, ore, herbs from the world. Core survival/crafting loop.",
            Cpu = 8, Ram = 2, Year = 1991, CanExpand = false, Research = 10,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.ResourceGathering },
            Synergies    = new[] { "Crafting System", "Open World" },
        };

        // ── GAMEPLAY — WORLD / MAP ───────────────────────────────

        yield return new FeatureEntry
        {
            Name = "Open World", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Non-linear exploration map with player freedom. High CPU and RAM cost.",
            Cpu = 30, Ram = 10, Year = 1984, CanExpand = true, Research = 25,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule, TechDebt = 0.2f,
            SignalTags = new[] { FeatureSO.GameplayTag.OpenWorld, FeatureSO.GameplayTag.MapSystem },
            Synergies  = new[] { "Fast Travel", "World Map", "Resource Gathering", "Enemy AI" },
            Conflicts  = new[] { "Linear Level Design" },
        };

        yield return new FeatureEntry
        {
            Name = "Linear Level Design", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Corridor-based progression. Focuses player attention and narrative pacing.",
            Cpu = 15, Ram = 4, Year = 1972, CanExpand = true, Research = 0,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.LinearLevel },
            Conflicts  = new[] { "Open World" },
        };

        yield return new FeatureEntry
        {
            Name = "Procedural Generation", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Algorithmically generated levels or worlds. High replayability.",
            Cpu = 28, Ram = 8, Year = 1980, CanExpand = true, Research = 25, TechDebt = 0.25f,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Procedural, FeatureSO.GameplayTag.OpenWorld },
            Synergies  = new[] { "Enemy AI", "Loot System" },
        };

        yield return new FeatureEntry
        {
            Name = "World Map", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Displays the game world at large scale. Allows strategic navigation.",
            Cpu = 8, Ram = 2, Year = 1980, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.MapSystem },
            Synergies    = new[] { "Open World", "Quest System" },
        };

        yield return new FeatureEntry
        {
            Name = "Fast Travel", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Teleport between discovered locations. Quality of life in large worlds.",
            Cpu = 5, Ram = 1, Year = 1986, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.FastTravel },
            RequiresTags = new[] { FeatureSO.GameplayTag.MapSystem },
            Prerequisites = new[] { "World Map" },
        };

        yield return new FeatureEntry
        {
            Name = "Destructible Environment", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Walls, crates, and terrain can be broken. Needs physics simulation.",
            Cpu = 20, Ram = 5, Year = 1999, CanExpand = false, Research = 20, TechDebt = 0.1f,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Destructible },
            RequiresTags = new[] { FeatureSO.GameplayTag.Physics },
            Prerequisites = new[] { "Physics Engine" },
            Synergies    = new[] { "Physics Engine", "AoE Attack" },
        };

        // ── GAMEPLAY — ECONOMY ───────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "Trading System", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Buy and sell items with NPCs. Economy backbone for RPGs.",
            Cpu = 12, Ram = 2, Year = 1982, CanExpand = true, Research = 12,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Trading, FeatureSO.GameplayTag.Currency },
            RequiresTags = new[] { FeatureSO.GameplayTag.Inventory },
            Prerequisites = new[] { "Inventory System" },
            Synergies    = new[] { "Economy Simulation", "Inventory System" },
        };

        yield return new FeatureEntry
        {
            Name = "Economy Simulation", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Supply and demand. Prices fluctuate based on player actions.",
            Cpu = 18, Ram = 3, Year = 1993, CanExpand = true, Research = 20,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Economy, FeatureSO.GameplayTag.Trading },
            RequiresTags = new[] { FeatureSO.GameplayTag.Trading },
            Prerequisites = new[] { "Trading System" },
        };

        // ── GAMEPLAY — AI ────────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "Enemy AI", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Basic enemy behaviour: patrol, chase, attack. Needs pathfinding.",
            Cpu = 20, Ram = 4, Year = 1980, CanExpand = true, Research = 10,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.EnemyAI },
            Synergies  = new[] { "Pathfinding AI", "Boss Encounter", "Stealth System" },
        };

        yield return new FeatureEntry
        {
            Name = "Pathfinding AI", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Navigation mesh for enemies to find paths around obstacles.",
            Cpu = 18, Ram = 3, Year = 1987, CanExpand = false, Research = 15,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.PathfindingAI, FeatureSO.GameplayTag.EnemyAI },
            RequiresTags = new[] { FeatureSO.GameplayTag.EnemyAI },
            Prerequisites = new[] { "Enemy AI" },
        };

        yield return new FeatureEntry
        {
            Name = "Companion AI", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Friendly NPC follows the player and assists in combat or exploration.",
            Cpu = 16, Ram = 3, Year = 1994, CanExpand = true, Research = 18,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.CompanionAI },
            RequiresTags = new[] { FeatureSO.GameplayTag.EnemyAI },
            Prerequisites = new[] { "Enemy AI" },
            Synergies    = new[] { "Dialogue System", "Quest System" },
        };

        yield return new FeatureEntry
        {
            Name = "Dynamic Difficulty", Category = FeatureSO.FeatureCategory.Gameplay,
            Description = "Adapts enemy health/damage based on player performance.",
            Cpu = 8, Ram = 1, Year = 1998, CanExpand = false, Research = 15,
            Domain = FeatureSO.DomainSocket.GP, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.DynamicDifficulty },
            RequiresTags = new[] { FeatureSO.GameplayTag.EnemyAI },
            Prerequisites = new[] { "Enemy AI" },
        };

        // ── TECH ─────────────────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "Physics Engine", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Rigid body simulation, collision response, ragdolls.",
            Cpu = 25, Ram = 6, Year = 1989, CanExpand = true, Research = 20,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Physics },
            Synergies  = new[] { "Destructible Environment", "Vehicle Control", "Ragdoll Death" },
        };

        yield return new FeatureEntry
        {
            Name = "Ragdoll Death", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Enemies collapse with physical simulation on death. Satisfying feedback.",
            Cpu = 12, Ram = 3, Year = 1998, CanExpand = false, Research = 15,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Physics },
            RequiresTags = new[] { FeatureSO.GameplayTag.Physics },
            Prerequisites = new[] { "Physics Engine" },
        };

        yield return new FeatureEntry
        {
            Name = "Platformer Level Design", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Tile-based or segment-based level construction tooling for 2D platformers.",
            Cpu = 10, Ram = 2, Year = 1977, CanExpand = true, Research = 5,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.CoreModule,
            SignalTags   = new[] { FeatureSO.GameplayTag.LinearLevel },
            Synergies    = new[] { "Basic Locomotion", "Jump System" },
        };

        yield return new FeatureEntry
        {
            Name = "Networking Layer", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Client-server or P2P network stack for multiplayer synchronisation.",
            Cpu = 30, Ram = 8, Year = 1995, CanExpand = true, Research = 30, TechDebt = 0.3f,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Networking },
        };

        yield return new FeatureEntry
        {
            Name = "Save System", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Serialises game state to disk. Supports checkpoints and manual saves.",
            Cpu = 6, Ram = 2, Year = 1980, CanExpand = false, Research = 5,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.Enhancement,
            SignalTags = new[] { FeatureSO.GameplayTag.SaveSystem },
        };

        yield return new FeatureEntry
        {
            Name = "Analytics SDK", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Tracks player behaviour and funnel data. Third-party middleware.",
            Cpu = 4, Ram = 1, Year = 2005, CanExpand = false, Research = 5,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.Middleware,
            IsMiddleware = true, MiddlewareCost = 500f,
            SignalTags = new[] { FeatureSO.GameplayTag.Analytics },
        };

        yield return new FeatureEntry
        {
            Name = "Localisation System", Category = FeatureSO.FeatureCategory.Tech,
            Description = "Multi-language text and audio support. Expands market reach.",
            Cpu = 5, Ram = 2, Year = 1990, CanExpand = false, Research = 10,
            Domain = FeatureSO.DomainSocket.TECH, Tier = NodeTierOverride.Enhancement,
            SignalTags = new[] { FeatureSO.GameplayTag.Localisation },
        };

        // ── GRAPHICS ─────────────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "2D Sprite Renderer", Category = FeatureSO.FeatureCategory.Graphic,
            Description = "Renders 2D sprites with palette-based colouring. Lightweight and sharp.",
            Cpu = 15, Ram = 4, Year = 1972, CanExpand = true, Research = 0,
            Domain = FeatureSO.DomainSocket.GFX, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Rendering2D, FeatureSO.GameplayTag.PixelArt },
            Synergies  = new[] { "Pixel Art Style", "8-Bit Sound Engine" },
        };

        yield return new FeatureEntry
        {
            Name = "Pixel Art Style", Category = FeatureSO.FeatureCategory.Graphic,
            Description = "Low-res pixel aesthetic. Cohesion bonus with retro audio.",
            Cpu = 8, Ram = 2, Year = 1972, CanExpand = false, Research = 0,
            Domain = FeatureSO.DomainSocket.GFX, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.PixelArt },
            RequiresTags = new[] { FeatureSO.GameplayTag.Rendering2D },
            Prerequisites = new[] { "2D Sprite Renderer" },
            Synergies    = new[] { "8-Bit Sound Engine", "2D Sprite Renderer" },
        };

        yield return new FeatureEntry
        {
            Name = "3D Renderer", Category = FeatureSO.FeatureCategory.Graphic,
            Description = "Full 3D polygon rendering pipeline with depth buffer and transforms.",
            Cpu = 28, Ram = 8, Year = 1992, CanExpand = true, Research = 25,
            Domain = FeatureSO.DomainSocket.GFX, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Rendering3D },
            Synergies  = new[] { "Dynamic Lighting", "Particle System", "Realistic Audio" },
            Conflicts  = new[] { "2D Sprite Renderer" },
        };

        yield return new FeatureEntry
        {
            Name = "Dynamic Lighting", Category = FeatureSO.FeatureCategory.Graphic,
            Description = "Real-time dynamic shadow and light calculation.",
            Cpu = 22, Ram = 6, Year = 1995, CanExpand = true, Research = 20, TechDebt = 0.1f,
            Domain = FeatureSO.DomainSocket.GFX, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Lighting },
            RequiresTags = new[] { FeatureSO.GameplayTag.Rendering3D },
            Prerequisites = new[] { "3D Renderer" },
        };

        yield return new FeatureEntry
        {
            Name = "Particle System", Category = FeatureSO.FeatureCategory.Graphic,
            Description = "Spawn and simulate thousands of VFX particles (fire, explosions, magic).",
            Cpu = 16, Ram = 4, Year = 1990, CanExpand = true, Research = 15,
            Domain = FeatureSO.DomainSocket.GFX, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Particles },
            Synergies    = new[] { "AoE Attack", "Boss Encounter", "3D Renderer" },
        };

        yield return new FeatureEntry
        {
            Name = "Custom Shaders", Category = FeatureSO.FeatureCategory.Graphic,
            Description = "GLSL/HLSL shaders for unique visual effects: water, glow, outline.",
            Cpu = 18, Ram = 3, Year = 1996, CanExpand = false, Research = 18, TechDebt = 0.15f,
            Domain = FeatureSO.DomainSocket.GFX, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Shaders },
            Synergies    = new[] { "3D Renderer", "Dynamic Lighting" },
        };

        // ── SOUND ─────────────────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "8-Bit Sound Engine", Category = FeatureSO.FeatureCategory.Sound,
            Description = "Chiptune / PSG sound generation. The retro sound identity.",
            Cpu = 10, Ram = 2, Year = 1972, CanExpand = true, Research = 0,
            Domain = FeatureSO.DomainSocket.SND, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.SoundFX, FeatureSO.GameplayTag.Music },
            Synergies  = new[] { "Pixel Art Style", "2D Sprite Renderer" },
        };

        yield return new FeatureEntry
        {
            Name = "Realistic Audio Engine", Category = FeatureSO.FeatureCategory.Sound,
            Description = "High-fidelity stereo or surround audio with streaming.",
            Cpu = 20, Ram = 8, Year = 1995, CanExpand = true, Research = 20,
            Domain = FeatureSO.DomainSocket.SND, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.SoundFX, FeatureSO.GameplayTag.Music },
            Synergies  = new[] { "3D Renderer", "Voice Acting", "Dynamic Music" },
            Conflicts  = new[] { "8-Bit Sound Engine" },
        };

        yield return new FeatureEntry
        {
            Name = "Dynamic Music", Category = FeatureSO.FeatureCategory.Sound,
            Description = "Music adapts in real-time to gameplay state (combat, exploration, tension).",
            Cpu = 14, Ram = 3, Year = 1999, CanExpand = false, Research = 18,
            Domain = FeatureSO.DomainSocket.SND, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Music, FeatureSO.GameplayTag.DynamicAudio },
            Synergies    = new[] { "Boss Encounter", "Stealth System", "Voice Acting" },
        };

        yield return new FeatureEntry
        {
            Name = "Voice Acting", Category = FeatureSO.FeatureCategory.Sound,
            Description = "Recorded dialogue lines for NPCs and protagonist. High royalty risk.",
            Cpu = 6, Ram = 12, Year = 1994, CanExpand = false, Research = 15,
            Domain = FeatureSO.DomainSocket.SND, Tier = NodeTierOverride.Middleware,
            IsMiddleware = true, MiddlewareCost = 2000f,
            SignalTags   = new[] { FeatureSO.GameplayTag.VoiceActing },
            Synergies    = new[] { "Dialogue System", "Story Branching", "Dynamic Music" },
        };

        yield return new FeatureEntry
        {
            Name = "3D Spatial Audio", Category = FeatureSO.FeatureCategory.Sound,
            Description = "Sound positioned in 3D space. Footsteps fade, enemies heard around corners.",
            Cpu = 12, Ram = 4, Year = 1997, CanExpand = false, Research = 15,
            Domain = FeatureSO.DomainSocket.SND, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.DynamicAudio },
            RequiresTags = new[] { FeatureSO.GameplayTag.Rendering3D },
            Synergies    = new[] { "Stealth System", "Enemy AI" },
        };

        // ── NARRATIVE ─────────────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "Dialogue System", Category = FeatureSO.FeatureCategory.Narrative,
            Description = "Conversation trees with NPCs. The foundation of narrative games.",
            Cpu = 14, Ram = 3, Year = 1978, CanExpand = true, Research = 10,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.Dialogue },
            Synergies  = new[] { "Story Branching", "Quest System", "Voice Acting", "Companion AI" },
        };

        yield return new FeatureEntry
        {
            Name = "Story Branching", Category = FeatureSO.FeatureCategory.Narrative,
            Description = "Player choices change the story outcome. Variable ending support.",
            Cpu = 12, Ram = 2, Year = 1985, CanExpand = true, Research = 15,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.StoryBranching },
            RequiresTags = new[] { FeatureSO.GameplayTag.Dialogue },
            Prerequisites = new[] { "Dialogue System" },
            Synergies    = new[] { "Quest System", "Voice Acting", "Journal" },
        };

        yield return new FeatureEntry
        {
            Name = "Quest System", Category = FeatureSO.FeatureCategory.Narrative,
            Description = "Track objectives, rewards, and story missions. Core RPG backbone.",
            Cpu = 10, Ram = 2, Year = 1982, CanExpand = true, Research = 10,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.QuestSystem },
            Synergies  = new[] { "Dialogue System", "World Map", "XP & Leveling" },
        };

        yield return new FeatureEntry
        {
            Name = "Cutscene System", Category = FeatureSO.FeatureCategory.Narrative,
            Description = "Director-controlled camera sequences that tell story beats.",
            Cpu = 10, Ram = 3, Year = 1987, CanExpand = false, Research = 12,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Cutscene },
            Synergies    = new[] { "Voice Acting", "Boss Encounter", "Dynamic Music" },
        };

        yield return new FeatureEntry
        {
            Name = "Journal", Category = FeatureSO.FeatureCategory.Narrative,
            Description = "In-game codex of lore entries, map notes, and item descriptions.",
            Cpu = 5, Ram = 2, Year = 1987, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Journal },
            Synergies    = new[] { "Quest System", "Story Branching" },
        };

        yield return new FeatureEntry
        {
            Name = "Inkle Narrative SDK", Category = FeatureSO.FeatureCategory.Narrative,
            Description = "Licensed Ink scripting runtime for branching narrative. Royalty-free after buy-in.",
            Cpu = 6, Ram = 2, Year = 2014, CanExpand = false, Research = 10,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Middleware,
            IsMiddleware = true, MiddlewareCost = 1000f,
            SignalTags   = new[] { FeatureSO.GameplayTag.StoryBranching, FeatureSO.GameplayTag.Dialogue },
            Synergies    = new[] { "Voice Acting", "Dialogue System" },
        };

        // ── UX ────────────────────────────────────────────────────

        yield return new FeatureEntry
        {
            Name = "HUD System", Category = FeatureSO.FeatureCategory.UX,
            Description = "Health bar, stamina, ammo counter and compass overlay.",
            Cpu = 8, Ram = 2, Year = 1972, CanExpand = true, Research = 0,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.CoreModule,
            SignalTags = new[] { FeatureSO.GameplayTag.HUD },
        };

        yield return new FeatureEntry
        {
            Name = "Minimap", Category = FeatureSO.FeatureCategory.UX,
            Description = "Small corner map showing nearby terrain, enemies, and objectives.",
            Cpu = 7, Ram = 2, Year = 1982, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.Minimap, FeatureSO.GameplayTag.MapSystem },
            Synergies    = new[] { "Open World", "World Map" },
        };

        yield return new FeatureEntry
        {
            Name = "Tutorial System", Category = FeatureSO.FeatureCategory.UX,
            Description = "Contextual or pop-up tutorials that teach controls and mechanics.",
            Cpu = 6, Ram = 1, Year = 1977, CanExpand = false, Research = 5,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags = new[] { FeatureSO.GameplayTag.Tutorial },
        };

        yield return new FeatureEntry
        {
            Name = "Pause Menu", Category = FeatureSO.FeatureCategory.UX,
            Description = "Full pause with settings, save/load, and inventory access.",
            Cpu = 4, Ram = 1, Year = 1975, CanExpand = false, Research = 0,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags = new[] { FeatureSO.GameplayTag.PauseMenu },
        };

        yield return new FeatureEntry
        {
            Name = "Accessibility Options", Category = FeatureSO.FeatureCategory.UX,
            Description = "Colourblind mode, subtitle options, remappable controls, reduced motion.",
            Cpu = 4, Ram = 1, Year = 2010, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags = new[] { FeatureSO.GameplayTag.Accessibility },
        };

        yield return new FeatureEntry
        {
            Name = "Achievement System", Category = FeatureSO.FeatureCategory.UX,
            Description = "Tracks and awards trophies/achievements. Steam/console integration.",
            Cpu = 5, Ram = 1, Year = 2005, CanExpand = false, Research = 8,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Enhancement,
            SignalTags   = new[] { FeatureSO.GameplayTag.AchievementSystem },
            Synergies    = new[] { "Quest System", "Loot System" },
        };

        yield return new FeatureEntry
        {
            Name = "Haptic Feedback SDK", Category = FeatureSO.FeatureCategory.UX,
            Description = "PlayStation DualSense or Xbox haptics SDK. Royalty per unit shipped.",
            Cpu = 3, Ram = 1, Year = 2020, CanExpand = false, Research = 5,
            Domain = FeatureSO.DomainSocket.NARR, Tier = NodeTierOverride.Middleware,
            IsMiddleware = true, MiddlewareCost = 800f,
            SignalTags = new[] { FeatureSO.GameplayTag.Accessibility },
        };
    }
}
#endif