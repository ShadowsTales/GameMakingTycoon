// ============================================================
//  FeatureCatalogCreator.cs  — UNITY EDITOR SCRIPT
//  Assets/Editor/FeatureCatalogCreator.cs
//
//  Erzeugt alle FeatureSO-Assets für 1972–1980 und fügt sie
//  automatisch in die FeatureDatabase ein.
//
//  Aufruf: Menü → Node-Tycoon → Create Feature Catalog 1972-1980
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FeatureCatalogCreator
{
    private const string OUTPUT_PATH = "Assets/Scripts/ScriptableObjects/Features";
    private const string DB_PATH     = "Assets/Scripts/ScriptableObjects/Database/Feature/FeatureDatabase.asset";

    [MenuItem("Node-Tycoon/Create Feature Catalog 1972-1980")]
    public static void CreateAll()
    {
        var db = AssetDatabase.LoadAssetAtPath<FeatureDatabase>(DB_PATH);
        if (db == null) { Debug.LogError("FeatureDatabase nicht gefunden: " + DB_PATH); return; }

        var all = new List<FeatureEntry>(FeatureCatalog1972To1980());
        int created = 0;

        foreach (var entry in all)
        {
            string dir  = Path.Combine(OUTPUT_PATH, entry.Category.ToString());
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, entry.Name + ".asset");

            // Bereits vorhanden? Überspringen
            if (AssetDatabase.LoadAssetAtPath<FeatureSO>(path) != null) continue;

            var so = ScriptableObject.CreateInstance<FeatureSO>();
            so.featureName        = entry.Name;
            so.category           = entry.Category;
            so.description        = entry.Description;
            so.cpuUsage           = entry.Cpu;
            so.ramUsage           = entry.Ram;
            so.isResearched       = true;
            so.releaseYear        = entry.Year;
            so.canExpand          = entry.CanExpand;
            so.researchCostPoints = entry.ResearchCost;
            so.tierOverride       = entry.Tier;

            AssetDatabase.CreateAsset(so, path);
            if (!db.allFeatures.Contains(so)) db.allFeatures.Add(so);
            created++;
        }

        // Prerequisite-Referenzen setzen (zweiter Pass)
        foreach (var entry in all)
        {
            if (entry.Prerequisites == null || entry.Prerequisites.Length == 0) continue;
            string path = Path.Combine(OUTPUT_PATH, entry.Category.ToString(), entry.Name + ".asset");
            var so = AssetDatabase.LoadAssetAtPath<FeatureSO>(path);
            if (so == null) continue;
            so.prerequisites.Clear();
            foreach (var prereqName in entry.Prerequisites)
            {
                var found = FindFeature(prereqName, all);
                if (found != null) so.prerequisites.Add(found);
            }
            EditorUtility.SetDirty(so);
        }

        // Synergie-Referenzen setzen (dritter Pass)
        foreach (var entry in all)
        {
            if (entry.Synergies == null || entry.Synergies.Length == 0) continue;
            string path = Path.Combine(OUTPUT_PATH, entry.Category.ToString(), entry.Name + ".asset");
            var so = AssetDatabase.LoadAssetAtPath<FeatureSO>(path);
            if (so == null) continue;
            so.synergyWith.Clear();
            foreach (var synName in entry.Synergies)
            {
                var found = FindFeature(synName, all);
                if (found != null) so.synergyWith.Add(found);
            }
            EditorUtility.SetDirty(so);
        }

        // Konflikt-Referenzen setzen
        foreach (var entry in all)
        {
            if (entry.Conflicts == null || entry.Conflicts.Length == 0) continue;
            string path = Path.Combine(OUTPUT_PATH, entry.Category.ToString(), entry.Name + ".asset");
            var so = AssetDatabase.LoadAssetAtPath<FeatureSO>(path);
            if (so == null) continue;
            so.conflictsWith.Clear();
            foreach (var cfName in entry.Conflicts)
            {
                var found = FindFeature(cfName, all);
                if (found != null) so.conflictsWith.Add(found);
            }
            EditorUtility.SetDirty(so);
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FeatureCatalog] {created} neue Features erstellt und in FeatureDatabase eingetragen.");
    }

    private static FeatureSO FindFeature(string name, List<FeatureEntry> all)
    {
        var entry = all.Find(e => e.Name == name);
        if (entry == null) return null;
        string path = Path.Combine(OUTPUT_PATH, entry.Category.ToString(), name + ".asset");
        return AssetDatabase.LoadAssetAtPath<FeatureSO>(path);
    }

    // ════════════════════════════════════════════════════════════════
    //  FEATURE-KATALOG 1972–1980
    //
    //  Designprinzipien:
    //   • Anker (Tier=Core, CPU≥15, canExpand):  grundlegende Mechaniken
    //   • Upgrades (Tier=Enhancement, CPU<15):   verfeinern Anker
    //   • Support (Sound/Narrative/UX):           globale Systeme
    //   • Optimizer (CPU<10):                     reduzieren Last
    //
    //  CPU-Budget Referenz:
    //   1972–1976 = 80 Punkte, 1977–1981 = 100 Punkte
    // ════════════════════════════════════════════════════════════════

    private static IEnumerable<FeatureEntry> FeatureCatalog1972To1980()
    {
        // ── GAMEPLAY ANKER (Hauptmechaniken) ─────────────────────

        yield return new FeatureEntry(
            "Joystick-Steuerung", FeatureSO.FeatureCategory.Gameplay,
            "Direktionale Eingabe per Joystick oder D-Pad. Grundlage jedes Arcade-Spiels.",
            cpu: 20, ram: 2, year: 1972, canExpand: true,
            tier: NodeTierOverride.Core, research: 0,
            synergies: new[] { "Highscore-System", "Extra-Leben" });

        yield return new FeatureEntry(
            "Feststehendes-Spielfeld", FeatureSO.FeatureCategory.Gameplay,
            "Ein statisches, nicht scrollendes Spielfeld. Klassisch für frühe Arcade-Spiele.",
            cpu: 15, ram: 4, year: 1972, canExpand: true,
            tier: NodeTierOverride.Core, research: 0,
            synergies: new[] { "Kollisionserkennung", "Spielfigur-Sprite" });

        yield return new FeatureEntry(
            "Wellen-basiertes-Spawning", FeatureSO.FeatureCategory.Gameplay,
            "Gegner erscheinen in aufeinander folgenden Wellen mit steigender Schwierigkeit.",
            cpu: 18, ram: 3, year: 1973, canExpand: true,
            tier: NodeTierOverride.Core, research: 10,
            synergies: new[] { "Highscore-System", "Schwierigkeitsanstieg" });

        yield return new FeatureEntry(
            "Rundenbasierter-Kampf", FeatureSO.FeatureCategory.Gameplay,
            "Spieler und Gegner wechseln sich in Zügen ab. Basis für Strategy-Spiele.",
            cpu: 22, ram: 5, year: 1974, canExpand: true,
            tier: NodeTierOverride.Core, research: 15,
            synergies: new[] { "Einheitenbewegung", "Ressourcen-Management" },
            conflicts: new[] { "Echtzeit-Kampf" });

        yield return new FeatureEntry(
            "Echtzeit-Kampf", FeatureSO.FeatureCategory.Gameplay,
            "Kämpfe laufen kontinuierlich ab. Reaktionszeit des Spielers entscheidet.",
            cpu: 25, ram: 4, year: 1975, canExpand: true,
            tier: NodeTierOverride.Core, research: 20,
            synergies: new[] { "Joystick-Steuerung", "Feinde-KI" },
            conflicts: new[] { "Rundenbasierter-Kampf" });

        yield return new FeatureEntry(
            "Labyrinth-Navigation", FeatureSO.FeatureCategory.Gameplay,
            "Spieler navigiert durch Gänge und Räume. Basis für Dungeon- und Maze-Spiele.",
            cpu: 20, ram: 6, year: 1976, canExpand: true,
            tier: NodeTierOverride.Core, research: 20,
            synergies: new[] { "Kollisionserkennung", "Map-Anzeige" });

        yield return new FeatureEntry(
            "Platforming", FeatureSO.FeatureCategory.Gameplay,
            "Springen zwischen Plattformen. Schwerkraft und Trägheit beeinflussen Bewegung.",
            cpu: 24, ram: 4, year: 1977, canExpand: true,
            tier: NodeTierOverride.Core, research: 25,
            synergies: new[] { "Joystick-Steuerung", "Physik-Sprung", "Scrolling" });

        yield return new FeatureEntry(
            "Scrolling", FeatureSO.FeatureCategory.Gameplay,
            "Das Spielfeld bewegt sich horizontal oder vertikal. Erweitert Spielraum enorm.",
            cpu: 28, ram: 8, year: 1978, canExpand: true,
            tier: NodeTierOverride.Core, research: 30,
            synergies: new[] { "Platforming", "Parallax-Hintergrund" },
            conflicts: new[] { "Feststehendes-Spielfeld" });

        yield return new FeatureEntry(
            "Text-Adventure-Parser", FeatureSO.FeatureCategory.Gameplay,
            "Spieler gibt Textbefehle ein. Verb-Nomen-Eingabe navigiert durch Spielwelt.",
            cpu: 18, ram: 12, year: 1976, canExpand: true,
            tier: NodeTierOverride.Core, research: 25,
            synergies: new[] { "Branching-Dialoge", "Inventar-System" });

        yield return new FeatureEntry(
            "Ressourcen-Management", FeatureSO.FeatureCategory.Gameplay,
            "Spieler verwaltet begrenzte Ressourcen wie Energie, Punkte oder Vorräte.",
            cpu: 16, ram: 5, year: 1977, canExpand: true,
            tier: NodeTierOverride.Core, research: 20,
            synergies: new[] { "Rundenbasierter-Kampf", "Inventar-System" });

        // ── GAMEPLAY UPGRADES ──────────────────────────────────────

        yield return new FeatureEntry(
            "Highscore-System", FeatureSO.FeatureCategory.Gameplay,
            "Speichert den höchsten Punktestand. Grundlegender Spielanreiz für Wiederholbarkeit.",
            cpu: 5, ram: 2, year: 1972, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 0);

        yield return new FeatureEntry(
            "Extra-Leben", FeatureSO.FeatureCategory.Gameplay,
            "Spieler hat mehrere Versuche. Klassische Arcade-Mechanik die Frust mildert.",
            cpu: 4, ram: 1, year: 1972, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 0,
            prereqs: new[] { "Joystick-Steuerung" });

        yield return new FeatureEntry(
            "Kollisionserkennung", FeatureSO.FeatureCategory.Gameplay,
            "Zuverlässige Erkennung wenn Objekte sich berühren. Kritisch für Schüsse und Treffer.",
            cpu: 8, ram: 2, year: 1972, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 5);

        yield return new FeatureEntry(
            "Schwierigkeitsanstieg", FeatureSO.FeatureCategory.Gameplay,
            "Das Spiel wird mit der Zeit schneller und schwieriger. Hält Spieler engagiert.",
            cpu: 5, ram: 1, year: 1973, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 5,
            prereqs: new[] { "Wellen-basiertes-Spawning" });

        yield return new FeatureEntry(
            "Feinde-KI", FeatureSO.FeatureCategory.Gameplay,
            "Einfache Gegner-Intelligenz: Verfolgen, Ausweichen, Formationen.",
            cpu: 12, ram: 3, year: 1975, canExpand: true,
            tier: NodeTierOverride.Enhancement, research: 15,
            prereqs: new[] { "Wellen-basiertes-Spawning" },
            synergies: new[] { "Echtzeit-Kampf" });

        yield return new FeatureEntry(
            "Physik-Sprung", FeatureSO.FeatureCategory.Gameplay,
            "Parabolische Sprungkurve mit Impuls. Macht Platforming deutlich befriedigender.",
            cpu: 10, ram: 2, year: 1977, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 15,
            prereqs: new[] { "Platforming" },
            synergies: new[] { "Scrolling" });

        yield return new FeatureEntry(
            "Inventar-System", FeatureSO.FeatureCategory.Gameplay,
            "Spieler kann Gegenstände sammeln und verwalten. Essentiell für RPG-Elemente.",
            cpu: 10, ram: 8, year: 1978, canExpand: true,
            tier: NodeTierOverride.Enhancement, research: 20,
            synergies: new[] { "Text-Adventure-Parser", "Ressourcen-Management" });

        yield return new FeatureEntry(
            "Einheitenbewegung", FeatureSO.FeatureCategory.Gameplay,
            "Spieler bewegt Einheiten auf einem Gitter. Basis für Strategy-Spiele.",
            cpu: 12, ram: 4, year: 1974, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 10,
            prereqs: new[] { "Rundenbasierter-Kampf" });

        yield return new FeatureEntry(
            "Map-Anzeige", FeatureSO.FeatureCategory.Gameplay,
            "Zeigt eine Übersichtskarte des Levels oder der Spielwelt an.",
            cpu: 8, ram: 5, year: 1977, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 15,
            prereqs: new[] { "Labyrinth-Navigation" });

        yield return new FeatureEntry(
            "Power-Ups", FeatureSO.FeatureCategory.Gameplay,
            "Sammelbare Items geben temporäre Boni. Klassische Arcade-Spannungskurve.",
            cpu: 7, ram: 2, year: 1975, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 10,
            synergies: new[] { "Wellen-basiertes-Spawning", "Highscore-System" });

        // ── GRAFIK ANKER ──────────────────────────────────────────

        yield return new FeatureEntry(
            "Spielfigur-Sprite", FeatureSO.FeatureCategory.Graphic,
            "Eine einzelne Grafikfigur die den Spieler repräsentiert. Kernvisualisierung.",
            cpu: 8, ram: 4, year: 1972, canExpand: true,
            tier: NodeTierOverride.Core, research: 0,
            synergies: new[] { "Gegner-Sprites", "Sprite-Animation" });

        yield return new FeatureEntry(
            "Tile-basierte-Grafik", FeatureSO.FeatureCategory.Graphic,
            "Die Spielwelt besteht aus wiederholten Grafikblöcken (Tiles). RAM-effizient.",
            cpu: 15, ram: 6, year: 1974, canExpand: true,
            tier: NodeTierOverride.Core, research: 10,
            synergies: new[] { "Labyrinth-Navigation", "Scrolling" });

        yield return new FeatureEntry(
            "Color-Display", FeatureSO.FeatureCategory.Graphic,
            "Farbige statt schwarz-weiße Grafik. Dramatische Verbesserung der Spielerfahrung.",
            cpu: 18, ram: 8, year: 1976, canExpand: true,
            tier: NodeTierOverride.Core, research: 20,
            synergies: new[] { "Tile-basierte-Grafik", "Spielfigur-Sprite" },
            conflicts: new[] { "Monochrom-Grafik" });

        yield return new FeatureEntry(
            "Parallax-Hintergrund", FeatureSO.FeatureCategory.Graphic,
            "Mehrere Hintergrundebenen bewegen sich unterschiedlich schnell. Tiefenillusion.",
            cpu: 22, ram: 10, year: 1979, canExpand: false,
            tier: NodeTierOverride.Core, research: 30,
            prereqs: new[] { "Scrolling" },
            synergies: new[] { "Color-Display" });

        // ── GRAFIK UPGRADES ──────────────────────────────────────

        yield return new FeatureEntry(
            "Gegner-Sprites", FeatureSO.FeatureCategory.Graphic,
            "Verschiedene Grafikfiguren für unterschiedliche Gegnertypen.",
            cpu: 6, ram: 6, year: 1973, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 5,
            prereqs: new[] { "Spielfigur-Sprite" });

        yield return new FeatureEntry(
            "Sprite-Animation", FeatureSO.FeatureCategory.Graphic,
            "Figuren haben mehrere Frames. Lauf-, Sprung- und Trefferanimationen.",
            cpu: 10, ram: 8, year: 1975, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 15,
            prereqs: new[] { "Spielfigur-Sprite" },
            synergies: new[] { "Platforming" });

        yield return new FeatureEntry(
            "Monochrom-Grafik", FeatureSO.FeatureCategory.Graphic,
            "Schwarz-weiße oder einfarbige Darstellung. Sehr CPU-effizient für frühe Hardware.",
            cpu: 5, ram: 2, year: 1972, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 0,
            conflicts: new[] { "Color-Display" });

        yield return new FeatureEntry(
            "Explosions-Effekte", FeatureSO.FeatureCategory.Graphic,
            "Visuelle Effekte bei Treffern und Explosionen. Gibt Spielaktionen Gewicht.",
            cpu: 8, ram: 4, year: 1976, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 10,
            synergies: new[] { "Echtzeit-Kampf", "Wellen-basiertes-Spawning" });

        yield return new FeatureEntry(
            "HUD-Anzeige", FeatureSO.FeatureCategory.Graphic,
            "Heads-Up-Display zeigt Score, Leben und Energie direkt im Bild.",
            cpu: 6, ram: 3, year: 1974, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 8,
            synergies: new[] { "Highscore-System", "Extra-Leben" });

        // ── SOUND SUPPORT ─────────────────────────────────────────

        yield return new FeatureEntry(
            "Einzelton-Effekte", FeatureSO.FeatureCategory.Sound,
            "Einfache Pieptöne bei Treffern, Schüssen und Spielereignissen. Basis-Audio.",
            cpu: 4, ram: 1, year: 1972, canExpand: true,
            tier: NodeTierOverride.Core, research: 0);

        yield return new FeatureEntry(
            "Melodie-Loop", FeatureSO.FeatureCategory.Sound,
            "Eine sich wiederholende Melodie im Hintergrund. Erhöht Immersion.",
            cpu: 8, ram: 3, year: 1975, canExpand: true,
            tier: NodeTierOverride.Core, research: 15,
            synergies: new[] { "Ereignis-Sounds" });

        yield return new FeatureEntry(
            "Ereignis-Sounds", FeatureSO.FeatureCategory.Sound,
            "Verschiedene Töne für verschiedene Spielereignisse: Sprung, Tod, Level-Up.",
            cpu: 6, ram: 2, year: 1974, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 8,
            prereqs: new[] { "Einzelton-Effekte" },
            synergies: new[] { "Melodie-Loop" });

        yield return new FeatureEntry(
            "Dynamische-Musik", FeatureSO.FeatureCategory.Sound,
            "Musik ändert sich je nach Spielsituation (Spannung, Sieg, Gefahr).",
            cpu: 12, ram: 4, year: 1978, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 25,
            prereqs: new[] { "Melodie-Loop" },
            synergies: new[] { "Schwierigkeitsanstieg" });

        // ── TECH SUPPORT ──────────────────────────────────────────

        yield return new FeatureEntry(
            "Spielstand-Speichern", FeatureSO.FeatureCategory.Tech,
            "Fortschritt kann gespeichert werden. Kritisch für längere Spielerfahrungen.",
            cpu: 6, ram: 4, year: 1977, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 20,
            synergies: new[] { "Inventar-System", "Text-Adventure-Parser" });

        yield return new FeatureEntry(
            "Zwei-Spieler-Modus", FeatureSO.FeatureCategory.Tech,
            "Zwei Spieler können gleichzeitig oder abwechselnd spielen. Erhöht Wiederspielwert.",
            cpu: 14, ram: 4, year: 1973, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 20,
            synergies: new[] { "Highscore-System", "Joystick-Steuerung" });

        yield return new FeatureEntry(
            "Assembler-Optimierung", FeatureSO.FeatureCategory.Tech,
            "Kritischer Code in Assembler geschrieben. Drastische CPU-Einsparung.",
            cpu: 4, ram: 0, year: 1972, canExpand: true,
            tier: NodeTierOverride.Enhancement, research: 30);

        yield return new FeatureEntry(
            "RAM-Kompression", FeatureSO.FeatureCategory.Tech,
            "Grafik und Daten werden komprimiert. Mehr Inhalt mit gleichem Speicher.",
            cpu: 7, ram: -4, year: 1976, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 25);

        yield return new FeatureEntry(
            "Hardware-Sprites", FeatureSO.FeatureCategory.Tech,
            "Nutzt Sprite-Chip des Systems. Deutlich schnellere Darstellung vieler Figuren.",
            cpu: 5, ram: 2, year: 1977, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 20,
            synergies: new[] { "Gegner-Sprites", "Sprite-Animation" });

        // ── NARRATIVE SUPPORT ─────────────────────────────────────

        yield return new FeatureEntry(
            "Intro-Sequenz", FeatureSO.FeatureCategory.Narrative,
            "Eine kurze Geschichte oder Erklärung vor dem Spiel. Setzt die Szene.",
            cpu: 5, ram: 6, year: 1976, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 10);

        yield return new FeatureEntry(
            "Branching-Dialoge", FeatureSO.FeatureCategory.Narrative,
            "NPCs bieten Gesprächsoptionen. Spieler-Entscheidungen beeinflussen Verlauf.",
            cpu: 10, ram: 10, year: 1979, canExpand: true,
            tier: NodeTierOverride.Core, research: 30,
            prereqs: new[] { "Text-Adventure-Parser" });

        yield return new FeatureEntry(
            "Spielwelt-Lore", FeatureSO.FeatureCategory.Narrative,
            "Bücher, Texttafeln und Hinweisschilder erzählen die Hintergrundgeschichte.",
            cpu: 4, ram: 8, year: 1978, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 15);

        // ── UX SUPPORT ────────────────────────────────────────────

        yield return new FeatureEntry(
            "Start-Bildschirm", FeatureSO.FeatureCategory.UX,
            "Titelschirm mit dem Spielnamen. Professionelles Erscheinungsbild.",
            cpu: 3, ram: 2, year: 1972, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 0);

        yield return new FeatureEntry(
            "Game-Over-Schirm", FeatureSO.FeatureCategory.UX,
            "Deutlicher Game-Over-Schirm gibt Spieler Abschluss und Neustartoptionen.",
            cpu: 3, ram: 2, year: 1972, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 0,
            synergies: new[] { "Highscore-System" });

        yield return new FeatureEntry(
            "Pause-Funktion", FeatureSO.FeatureCategory.UX,
            "Spieler kann das Spiel pausieren. Grundlegendes Quality-of-Life-Feature.",
            cpu: 2, ram: 0, year: 1975, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 5);

        yield return new FeatureEntry(
            "Schwierigkeitsauswahl", FeatureSO.FeatureCategory.UX,
            "Spieler wählt am Anfang Easy/Normal/Hard. Macht Spiel zugänglicher.",
            cpu: 3, ram: 1, year: 1977, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 10,
            prereqs: new[] { "Schwierigkeitsanstieg" });

        yield return new FeatureEntry(
            "Anleitungs-Tutorial", FeatureSO.FeatureCategory.UX,
            "Einführungstext oder Demo erklärt die Steuerung. Senkt Einstiegshürde.",
            cpu: 5, ram: 4, year: 1979, canExpand: false,
            tier: NodeTierOverride.Enhancement, research: 15);
    }

    // ════════════════════════════════════════════════════════════════
    //  DATENKLASSE
    // ════════════════════════════════════════════════════════════════

    private class FeatureEntry
    {
        public string                       Name;
        public FeatureSO.FeatureCategory    Category;
        public string                       Description;
        public float                        Cpu;
        public float                        Ram;
        public int                          Year;
        public bool                         CanExpand;
        public NodeTierOverride             Tier;
        public float                        ResearchCost;
        public string[]                     Prerequisites;
        public string[]                     Synergies;
        public string[]                     Conflicts;

        public FeatureEntry(
            string name, FeatureSO.FeatureCategory cat, string desc,
            float cpu, float ram, int year, bool canExpand,
            NodeTierOverride tier, float research,
            string[] prereqs    = null,
            string[] synergies  = null,
            string[] conflicts  = null)
        {
            Name          = name;
            Category      = cat;
            Description   = desc;
            Cpu           = cpu;
            Ram           = ram;
            Year          = year;
            CanExpand     = canExpand;
            Tier          = tier;
            ResearchCost  = research;
            Prerequisites = prereqs;
            Synergies     = synergies;
            Conflicts     = conflicts;
        }
    }
}
#endif
