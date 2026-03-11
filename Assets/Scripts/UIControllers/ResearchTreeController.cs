using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Baut den CIV-style Research Tree auf.
///
/// BUG-FIX 1 (Nodes unsichtbar):
///   GridContent bekommt jetzt eine explizite Pixel-Größe per Code
///   statt height:100% im UXML. height:100% funktioniert nicht wenn
///   der Parent position:absolute hat und kein festes Maß kennt.
///
/// BUG-FIX 2 (Linien alle bei 0,0):
///   NodeConnectionService zeichnet erst nach GeometryChangedEvent,
///   also nachdem Unity das Layout berechnet hat und worldBound korrekt ist.
///
/// ══ KONFIGURATION ══ Werte hier anpassen ══════════════════════════
/// </summary>
public class ResearchTreeController : MonoBehaviour
{
    [Header("Data")]
    public FeatureDatabase featureDB;

    // ══ KONFIGURATION ════════════════════════════════════════════════
    private const float X_PER_YEAR       = 220f;    // Pixel pro Jahr
    private const float X_ORIGIN         = 80f;    // Startabstand links
    private const int   MIN_YEAR         = 1972;
    private const int   MAX_YEAR         = 2025;
    private const float NODE_W           = 200f;
    private const float NODE_H           = 120f;
    private const float LANE_H           = 480f;   // Höhe jeder Lane
    // Wenn zwei Features in derselben Lane + gleichem Jahr: um diesen Wert nach unten
    private const float SAME_YEAR_OFFSET = 114f;
    // Pixelabstand unterhalb dessen zwei Nodes als "gleiche Spalte" gelten
    private const float COLLISION_RANGE  = 12f;
    // ════════════════════════════════════════════════════════════════

    private static readonly string[] CatKeys   = { "gameplay", "graphic", "sound", "tech" , "UX", "Narrative" };
    private static readonly string[] CatLabels = { "GAMEPLAY", "GRAFIK", "SOUND", "TECHNIK", "UX", "NARRATIVE" };

    private NodeBoardView         _boardView;
    private NodeConnectionService _connService;
    private VisualElement         _spawnArea;

    // ── Entry Point ──────────────────────────────────────────────────
    public void SetupResearchTree(VisualElement researchRoot)
    {
        var viewport = researchRoot.Q<VisualElement>("ResearchViewport");
        var thumb    = researchRoot.Q<VisualElement>("ScrollbarThumb");
        var track    = researchRoot.Q<VisualElement>("ScrollbarTrack");

        _boardView = new NodeBoardView(viewport, thumb, track);
        _spawnArea = _boardView.GetSpawnArea();

        // ── BUG-FIX 1: Explizite Größe für GridContent ──────────────
        // NICHT height:100% im UXML, sondern feste Pixel-Werte per Code.
        // Höhe = 4 Lanes. Breite = bis zum letzten Jahr + Rand.
        float totalW = NodeX(MAX_YEAR) + NODE_W + 120f;
        float totalH = LANE_H * 6f;

        _spawnArea.style.width  = totalW;
        _spawnArea.style.height = totalH;
        _boardView.TotalContentWidth = totalW;

        // Verbindungs-Service (zeichnet erst nach GeometryChangedEvent)
        _connService = new NodeConnectionService(_spawnArea);
        _connService.AllNodes.Clear();

        // Aufbau
        SetupLanes();
        BuildYearMarkers();
        BuildLaneLabels();
        var nodeDict = BuildNodes();
        WireConnections(nodeDict);

        // Scrollbar nach Layout-Pass initialisieren
        _spawnArea.schedule.Execute(() => _boardView.UpdateScrollbar()).StartingIn(80);
    }

    // ── Lanes ────────────────────────────────────────────────────────
    private void SetupLanes()
    {
        string[] names = { "Lane_Gameplay", "Lane_Graphic", "Lane_Sound", "Lane_Tech", "Lane_UX", "Lane_Narrative" };
        for (int i = 0; i < 6; i++)
        {
            var lane = _spawnArea.Q(names[i]);
            if (lane == null) continue;
            lane.style.top    = i * LANE_H;
            lane.style.height = LANE_H;
        }
    }

    // ── Nodes erstellen ──────────────────────────────────────────────
    private Dictionary<FeatureSO, FeatureNode> BuildNodes()
    {
        var nodeDict = new Dictionary<FeatureSO, FeatureNode>();
        // Belegte Y-Positionen pro (catIdx, spalteX)
        var occupied = new Dictionary<(int, float), List<float>>();

        foreach (var f in featureDB.allFeatures.OrderBy(x => x.releaseYear))
        {
            int   cat   = (int)f.category;
            float x     = NodeX(f.releaseYear);
            float baseY = cat * LANE_H + (LANE_H - NODE_H) / 2f;
            float y     = ResolveY(occupied, cat, x, baseY);

            var el = CreateNode(f);
            el.style.left = x;
            el.style.top  = y;
            _spawnArea.Add(el);

            var fn = new FeatureNode(el, f);
            _connService.AllNodes.Add(fn);
            nodeDict[f] = fn;
        }
        return nodeDict;
    }

    private float ResolveY(Dictionary<(int, float), List<float>> occ,
                            int cat, float x, float baseY)
    {
        // Runde X auf COLLISION_RANGE-Raster damit Nachbarn erkannt werden
        var key = (cat, Mathf.Round(x / COLLISION_RANGE) * COLLISION_RANGE);
        if (!occ.TryGetValue(key, out var list)) { list = new List<float>(); occ[key] = list; }

        float candidate = baseY;
        for (int safety = 0; safety < 10; safety++)
        {
            bool clash = false;
            foreach (float used in list)
            {
                if (Mathf.Abs(used - candidate) < NODE_H - 4f) { clash = true; break; }
            }
            if (!clash) break;
            candidate += SAME_YEAR_OFFSET;
        }
        list.Add(candidate);
        return candidate;
    }

    // ── Node-Element ─────────────────────────────────────────────────
    private VisualElement CreateNode(FeatureSO f)
    {
        string cat = CatKeys[(int)f.category];

        var root = new VisualElement();
        root.AddToClassList("research-node");
        root.AddToClassList("cat-" + cat);
        root.AddToClassList(f.isResearched ? "researched" : "locked");

        var accent = new VisualElement();
        accent.AddToClassList("node-accent-bar");
        accent.AddToClassList("accent-" + cat);
        root.Add(accent);

        var icon = new Label(CatEmoji(f.category));
        icon.AddToClassList("node-icon");
        icon.AddToClassList("border-" + cat);
        root.Add(icon);

        var name = new Label(f.featureName);
        name.AddToClassList("node-name");
        root.Add(name);

        var year = new Label(f.releaseYear.ToString());
        year.AddToClassList("node-year");
        root.Add(year);

        return root;
    }

    // ── Verbindungen verdrahten ──────────────────────────────────────
    private void WireConnections(Dictionary<FeatureSO, FeatureNode> nodeDict)
    {
        foreach (var f in featureDB.allFeatures)
        {
            if (!nodeDict.TryGetValue(f, out var child)) continue;
            bool first = true;
            foreach (var pre in f.prerequisites)
            {
                if (!nodeDict.TryGetValue(pre, out var parent)) continue;
                if (first) { child.Parent = parent; first = false; }
                else
                {
                    // Proxy für jeden weiteren Prereq → extra Linie
                    _connService.AllNodes.Add(new FeatureNode(child.Element, f) { Parent = parent });
                }
            }
        }
    }

    // ── Jahres-Marker ────────────────────────────────────────────────
    private void BuildYearMarkers()
    {
        var layer = _spawnArea.Q("YearMarkers") ?? _spawnArea;
        for (int y = MIN_YEAR; y <= MAX_YEAR; y++)
        {
            bool is10 = y % 10 == 0;
            bool is5  = y % 5  == 0 && !is10;

            var line = new VisualElement();
            line.pickingMode = PickingMode.Ignore;
            line.AddToClassList("year-line");
            line.AddToClassList(is10 ? "year-line-10" : is5 ? "year-line-5" : "year-line-1");
            line.style.left = NodeCX(y);
            layer.Add(line);

            if (!is5 && !is10) continue;

            var lbl = new Label(y.ToString());
            lbl.pickingMode = PickingMode.Ignore;
            lbl.AddToClassList("year-label");
            lbl.AddToClassList(is10 ? "year-label-10" : "year-label-5");
            lbl.style.left = NodeCX(y) - 18f;
            layer.Add(lbl);
        }
    }

    // ── Lane-Labels ──────────────────────────────────────────────────
    private void BuildLaneLabels()
    {
        for (int i = 0; i < 4; i++)
        {
            var lbl = new Label(CatLabels[i]);
            lbl.pickingMode = PickingMode.Ignore;
            lbl.AddToClassList("lane-label");
            lbl.AddToClassList("lane-label-" + CatKeys[i]);
            lbl.style.top  = i * LANE_H + LANE_H * 0.5f - 8f;
            lbl.style.left = 10f;
            _spawnArea.Add(lbl);
        }
    }

    // ── Koordinaten ──────────────────────────────────────────────────
    private static float NodeX(int year)  => X_ORIGIN + (year - MIN_YEAR) * X_PER_YEAR;
    private static float NodeCX(int year) => NodeX(year) + NODE_W / 2f;

    private static string CatEmoji(FeatureSO.FeatureCategory c) => c switch
    {
        FeatureSO.FeatureCategory.Gameplay => "🎮",
        FeatureSO.FeatureCategory.Graphic  => "🎨",
        FeatureSO.FeatureCategory.Sound    => "🎵",
        FeatureSO.FeatureCategory.Tech     => "⚙",
        _                                  => "★",
    };
}