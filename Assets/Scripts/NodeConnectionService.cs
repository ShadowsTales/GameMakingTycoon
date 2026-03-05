using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// Zeichnet Bezier-Verbindungen zwischen Research-Nodes.
///
/// BUG-FIX: generateVisualContent wird aufgerufen bevor Unity das Layout
/// berechnet hat → worldBound wäre (0,0). Deshalb hört dieser Service
/// auf GeometryChangedEvent des SpawnArea und löst erst dann ein Redraw
/// aus. Dadurch sind alle Positionen garantiert korrekt.
/// </summary>
public class NodeConnectionService
{
    public List<FeatureNode> AllNodes = new List<FeatureNode>();

    private readonly VisualElement _spawnArea;
    private bool _layoutReady = false;

    private static readonly Color ColGameplay = new Color(0.961f, 0.620f, 0.043f);
    private static readonly Color ColGraphic  = new Color(0.937f, 0.267f, 0.267f);
    private static readonly Color ColSound    = new Color(0.133f, 0.773f, 0.369f);
    private static readonly Color ColTech     = new Color(0.231f, 0.510f, 0.965f);
    private static readonly Color ColLocked   = new Color(0.22f,  0.22f,  0.27f, 0.55f);

    public NodeConnectionService(VisualElement spawnArea)
    {
        _spawnArea = spawnArea;
        _spawnArea.generateVisualContent += Draw;

        // Warten bis das Layout fertig ist — erst dann ist worldBound korrekt
        _spawnArea.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // Nur beim ersten Mal registrieren, dann deregistrieren
        // (Spätere Redraws per RequestRedraw())
        if (!_layoutReady)
        {
            _layoutReady = true;
            _spawnArea.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
        _spawnArea.MarkDirtyRepaint();
    }

    // ── Zeichnen ──────────────────────────────────────────────────────
    private void Draw(MeshGenerationContext mgc)
    {
        if (!_layoutReady) return;

        var p = mgc.painter2D;

        foreach (var node in AllNodes)
        {
            if (node.Parent == null) continue;

            Vector2 start = GetCenter(node.Parent.Element);
            Vector2 end   = GetCenter(node.Element);

            // Wenn beide Punkte (0,0) → Layout noch nicht bereit, überspringen
            if (start == Vector2.zero && end == Vector2.zero) continue;

            bool unlocked = node.Feature.isResearched && node.Parent.Feature.isResearched;
            bool sameLane = node.Feature.category == node.Parent.Feature.category;

            Color lineColor = unlocked ? GetColor(node.Feature.category) : ColLocked;

            // Glow-Pass
            if (unlocked)
            {
                p.strokeColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.10f);
                p.lineWidth   = 7f;
                DrawBezier(p, start, end, sameLane);
            }

            // Haupt-Linie
            p.strokeColor = new Color(lineColor.r, lineColor.g, lineColor.b,
                                       unlocked ? 0.85f : 0.55f);
            p.lineWidth = unlocked ? 2.5f : 1.5f;

            if (unlocked)
                DrawBezier(p, start, end, sameLane);
            else
                DrawDashed(p, start, end, sameLane);

            // Endpunkt-Dot
            if (unlocked)
            {
                p.fillColor = lineColor;
                p.BeginPath();
                p.Arc(end + new Vector2(-73f, 0f), 3.5f, 0f, 360f);
                p.Fill();
            }
        }
    }

    // ── Bezier ───────────────────────────────────────────────────────
    private static void DrawBezier(Painter2D p, Vector2 s, Vector2 e, bool sameLane)
    {
        Vector2 c1, c2;
        if (sameLane)
        {
            float t = Mathf.Abs(e.x - s.x) * 0.40f;
            c1 = s + new Vector2(t,  0f);
            c2 = e - new Vector2(t,  0f);
        }
        else
        {
            float mx = s.x + (e.x - s.x) * 0.55f;
            c1 = new Vector2(mx, s.y);
            c2 = new Vector2(mx, e.y);
        }
        p.BeginPath();
        p.MoveTo(s);
        p.BezierCurveTo(c1, c2, e);
        p.Stroke();
    }

    // ── Gestrichelter Pfad ────────────────────────────────────────────
    private static void DrawDashed(Painter2D p, Vector2 s, Vector2 e, bool sameLane,
                                    float dashLen = 7f, float gapLen = 5f)
    {
        const int STEPS = 60;
        Vector2 prev    = s;
        bool drawing    = true;
        float budget    = dashLen;

        for (int i = 1; i <= STEPS; i++)
        {
            float   t  = i / (float)STEPS;
            Vector2 pt = SampleBezier(s, e, sameLane, t);
            float   seg = Vector2.Distance(prev, pt);

            while (seg > 0.001f)
            {
                if (seg <= budget)
                {
                    if (drawing)
                    {
                        p.BeginPath();
                        p.MoveTo(prev);
                        p.LineTo(pt);
                        p.Stroke();
                    }
                    budget -= seg;
                    seg     = 0f;
                }
                else
                {
                    float frac = budget / seg;
                    Vector2 mid = Vector2.Lerp(prev, pt, frac);
                    if (drawing)
                    {
                        p.BeginPath();
                        p.MoveTo(prev);
                        p.LineTo(mid);
                        p.Stroke();
                    }
                    prev    = mid;
                    seg    -= budget;
                    drawing = !drawing;
                    budget  = drawing ? dashLen : gapLen;
                }
            }
            prev = pt;
        }
    }

    private static Vector2 SampleBezier(Vector2 s, Vector2 e, bool sameLane, float t)
    {
        Vector2 c1, c2;
        if (sameLane)
        {
            float ten = Mathf.Abs(e.x - s.x) * 0.40f;
            c1 = s + new Vector2(ten, 0f);
            c2 = e - new Vector2(ten, 0f);
        }
        else
        {
            float mx = s.x + (e.x - s.x) * 0.55f;
            c1 = new Vector2(mx, s.y);
            c2 = new Vector2(mx, e.y);
        }
        float u = 1f - t;
        return u*u*u*s + 3f*u*u*t*c1 + 3f*u*t*t*c2 + t*t*t*e;
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────
    private Vector2 GetCenter(VisualElement el)
        => _spawnArea.WorldToLocal(el.worldBound.center);

    private static Color GetColor(FeatureSO.FeatureCategory cat) => cat switch
    {
        FeatureSO.FeatureCategory.Gameplay => ColGameplay,
        FeatureSO.FeatureCategory.Graphic  => ColGraphic,
        FeatureSO.FeatureCategory.Sound    => ColSound,
        FeatureSO.FeatureCategory.Tech     => ColTech,
        _                                  => Color.white,
    };

    public void RequestRedraw() => _spawnArea.MarkDirtyRepaint();
}