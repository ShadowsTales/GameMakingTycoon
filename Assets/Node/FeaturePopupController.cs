// ============================================================
//  FeaturePopupController.cs  — NODE-TYCOON
//
//  Öffnet ein modales Detail-Fenster wenn der Spieler:
//    • auf eine Sidebar-Karte klickt (ohne zu ziehen)
//    • auf einen Canvas-Node doppelklickt
//
//  Das Popup zeigt:
//    Name, Jahr, Kategorie, Typ (Anker/Upgrade/Support)
//    CPU-Kosten, RAM, Beschreibung, Tags
//    Synergie-Liste, Konflikt-Liste, Voraussetzungen
//    Genre-Fit-Balken
//    "Zum Canvas hinzufügen" Button (wenn in Sidebar)
//
//  Usage (in NodeGraphController):
//    _popup.Show(feature, onAdd: pos => SpawnFeatureNode(feature, pos));
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class FeaturePopupController
{
    private readonly VisualElement _root;       // Das Popup-Element selbst
    private readonly VisualElement _overlay;    // Dunkler Hintergrund
    private NodeScoringService     _scoring;

    // ── Callback wenn "Hinzufügen" gedrückt wird ──────────────────
    private Action _onAddClicked;

    // ════════════════════════════════════════════════════════════════
    //  BAUEN
    // ════════════════════════════════════════════════════════════════

    public static FeaturePopupController Build(VisualElement canvasRoot, NodeScoringService scoring)
    {
        var controller = new FeaturePopupController(canvasRoot, scoring);
        return controller;
    }

    private FeaturePopupController(VisualElement canvasRoot, NodeScoringService scoring)
    {
        _scoring = scoring;

        // Overlay (schließt Popup bei Klick)
        _overlay = new VisualElement { name = "PopupOverlay" };
        _overlay.AddToClassList("popup-overlay");
        _overlay.AddToClassList("hidden");
        _overlay.pickingMode = PickingMode.Position;
        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay) Hide();
        });

        // Popup-Box
        _root = new VisualElement { name = "FeaturePopup" };
        _root.AddToClassList("feature-popup");

        // Header-Zeile (Titel + Schließen)
        var header = new VisualElement();
        header.AddToClassList("popup-header");
        var closeBtn = new Button(Hide) { text = "×" };
        closeBtn.AddToClassList("popup-close-btn");
        var titleLabel = new Label("Feature-Details") { name = "PopupTitle" };
        titleLabel.AddToClassList("popup-title");
        header.Add(titleLabel);
        header.Add(closeBtn);
        _root.Add(header);

        // Meta-Zeile (Kategorie · Jahr · Typ)
        var meta = new Label("") { name = "PopupMeta" };
        meta.AddToClassList("popup-meta");
        _root.Add(meta);

        // CPU/RAM-Anzeige
        var resourceRow = new VisualElement();
        resourceRow.AddToClassList("popup-resource-row");
        resourceRow.Add(BuildResourceBar("CPU", "PopupCpuBar", "PopupCpuLabel", new Color(0.13f, 0.83f, 0.93f)));
        resourceRow.Add(BuildResourceBar("RAM", "PopupRamBar", "PopupRamLabel", new Color(0.55f, 0.35f, 0.95f)));
        _root.Add(resourceRow);

        // Trennlinie
        _root.Add(Divider());

        // Beschreibung
        var desc = new Label("") { name = "PopupDesc" };
        desc.AddToClassList("popup-desc");
        _root.Add(desc);

        // Tags-Zeile
        var tags = new Label("") { name = "PopupTags" };
        tags.AddToClassList("popup-tags");
        _root.Add(tags);

        _root.Add(Divider());

        // Genre-Fit-Balken
        var fitRow = new VisualElement();
        fitRow.AddToClassList("popup-fit-row");
        var fitTitle = new Label("GENRE-FIT");
        fitTitle.AddToClassList("popup-section-label");
        var fitBarOuter = new VisualElement();
        fitBarOuter.AddToClassList("popup-fit-bar-outer");
        var fitBarFill = new VisualElement { name = "PopupFitFill" };
        fitBarFill.AddToClassList("popup-fit-bar-fill");
        fitBarOuter.Add(fitBarFill);
        var fitLabel = new Label("") { name = "PopupFitLabel" };
        fitLabel.AddToClassList("popup-fit-label");
        fitRow.Add(fitTitle);
        fitRow.Add(fitBarOuter);
        fitRow.Add(fitLabel);
        _root.Add(fitRow);

        // Synergie-Liste
        var synTitle = new Label("SYNERGIEN") { };
        synTitle.AddToClassList("popup-section-label");
        _root.Add(synTitle);
        var synList = new VisualElement { name = "PopupSynList" };
        synList.AddToClassList("popup-tag-list");
        _root.Add(synList);

        // Konflikt-Liste
        var cfTitle = new Label("KONFLIKTE");
        cfTitle.AddToClassList("popup-section-label");
        _root.Add(cfTitle);
        var cfList = new VisualElement { name = "PopupCfList" };
        cfList.AddToClassList("popup-tag-list");
        _root.Add(cfList);

        // Voraussetzungen
        var preTitle = new Label("VORAUSSETZUNGEN");
        preTitle.AddToClassList("popup-section-label");
        _root.Add(preTitle);
        var preList = new Label("") { name = "PopupPrereqs" };
        preList.AddToClassList("popup-prereqs");
        _root.Add(preList);

        _root.Add(Divider());

        // Footer-Buttons
        var footer = new VisualElement();
        footer.AddToClassList("popup-footer");
        var addBtn = new Button(() => { _onAddClicked?.Invoke(); Hide(); }) { text = "+ AUF CANVAS HINZUFÜGEN", name = "PopupAddBtn" };
        addBtn.AddToClassList("popup-add-btn");
        var cancelBtn = new Button(Hide) { text = "SCHLIESSEN" };
        cancelBtn.AddToClassList("popup-cancel-btn");
        footer.Add(addBtn);
        footer.Add(cancelBtn);
        _root.Add(footer);

        _overlay.Add(_root);
        canvasRoot.Add(_overlay);
    }

    // ════════════════════════════════════════════════════════════════
    //  SHOW / HIDE
    // ════════════════════════════════════════════════════════════════

    public void Show(FeatureSO feature, Action onAdd = null, float maxCpu = 120f)
    {
        _onAddClicked = onAdd;

        // Titel + Meta
        Set("PopupTitle", feature.featureName);

        string kind = IsAnchor(feature) ? "▲ ANKER" : IsSupport(feature) ? "◉ SUPPORT" : "◆ UPGRADE";
        Set("PopupMeta", $"{feature.category.ToString().ToUpper()}  ·  {feature.releaseYear}  ·  {kind}");

        // CPU-Bar
        float cpuPct = Mathf.Clamp01(feature.cpuUsage / maxCpu);
        SetBar("PopupCpuBar", cpuPct);
        Set("PopupCpuLabel", $"{feature.cpuUsage:0} CPU");

        float ramPct = Mathf.Clamp01(feature.ramUsage / 64f);
        SetBar("PopupRamBar", ramPct);
        Set("PopupRamLabel", $"{feature.ramUsage:0} RAM");

        // Beschreibung
        Set("PopupDesc", string.IsNullOrWhiteSpace(feature.description)
            ? "Keine Beschreibung vorhanden."
            : feature.description);

        // Tags
        var tagList = new List<string> { feature.category.ToString() };
        if (feature.canExpand)     tagList.Add("Erweiterbar");
        if (feature.cpuUsage > 20) tagList.Add("CPU-intensiv");
        if (feature.prerequisites.Count == 0) tagList.Add("Basis-Feature");
        Set("PopupTags", "◈ " + string.Join("  ·  ", tagList));

        // Genre-Fit
        float fit = _scoring?.GenreFit(feature) ?? 0.5f;
        SetBar("PopupFitFill", fit);
        string fitText = fit >= 0.7f ? $"★ Empfohlen ({fit:0%})"
                       : fit < 0.3f  ? $"✗ Schwach ({fit:0%})"
                                     : $"◦ Neutral ({fit:0%})";
        Set("PopupFitLabel", fitText);

        // Synergie-Tags
        var synRoot = _root.Q<VisualElement>("PopupSynList");
        if (synRoot != null)
        {
            synRoot.Clear();
            if (feature.synergyWith?.Count > 0)
                foreach (var s in feature.synergyWith)
                    synRoot.Add(TagChip(s.featureName, TagChipStyle.Synergy));
            else
                synRoot.Add(TagChip("Keine", TagChipStyle.Neutral));
        }

        // Konflikt-Tags
        var cfRoot = _root.Q<VisualElement>("PopupCfList");
        if (cfRoot != null)
        {
            cfRoot.Clear();
            if (feature.conflictsWith?.Count > 0)
                foreach (var c in feature.conflictsWith)
                    cfRoot.Add(TagChip(c.featureName, TagChipStyle.Conflict));
            else
                cfRoot.Add(TagChip("Keine", TagChipStyle.Neutral));
        }

        // Voraussetzungen
        Set("PopupPrereqs", feature.prerequisites.Count > 0
            ? "⚠ " + string.Join("  ›  ", feature.prerequisites.Select(p => p.featureName))
            : "✓ Keine Voraussetzungen");

        // Add-Button: sichtbar nur wenn onAdd gesetzt
        var addBtn = _root.Q<Button>("PopupAddBtn");
        if (addBtn != null)
            addBtn.EnableInClassList("hidden", onAdd == null);

        // Kategorie-Farb-Klasse
        foreach (var cat in Enum.GetValues(typeof(FeatureSO.FeatureCategory)))
            _root.RemoveFromClassList("popup-pillar-" + cat.ToString().ToLower());
        _root.AddToClassList("popup-pillar-" + feature.category.ToString().ToLower());

        _overlay.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        _overlay.AddToClassList("hidden");
    }

    public void UpdateScoring(NodeScoringService scoring) => _scoring = scoring;

    // ════════════════════════════════════════════════════════════════
    //  HILFSMETHODEN
    // ════════════════════════════════════════════════════════════════

    private void Set(string name, string text)
    {
        var el = _root.Q<Label>(name);
        if (el != null) el.text = text;
    }

    private void SetBar(string name, float pct)
    {
        var el = _root.Q<VisualElement>(name);
        if (el != null) el.style.width = Length.Percent(Mathf.Clamp01(pct) * 100f);
    }

    private enum TagChipStyle { Synergy, Conflict, Neutral }

    private static VisualElement TagChip(string label, TagChipStyle style)
    {
        var chip = new Label(label);
        chip.AddToClassList("popup-chip");
        chip.AddToClassList(style switch
        {
            TagChipStyle.Synergy  => "popup-chip--synergy",
            TagChipStyle.Conflict => "popup-chip--conflict",
            _                     => "popup-chip--neutral",
        });
        chip.pickingMode = PickingMode.Ignore;
        return chip;
    }

    private static VisualElement Divider()
    {
        var d = new VisualElement();
        d.AddToClassList("popup-divider");
        return d;
    }

    private static VisualElement BuildResourceBar(string label, string fillName, string lblName, Color color)
    {
        var col = new VisualElement();
        col.AddToClassList("popup-res-col");

        var title = new Label(label);
        title.AddToClassList("popup-res-title");
        col.Add(title);

        var barOuter = new VisualElement();
        barOuter.AddToClassList("popup-res-bar-outer");
        var barFill = new VisualElement { name = fillName };
        barFill.AddToClassList("popup-res-bar-fill");
        barFill.style.backgroundColor = color;
        barOuter.Add(barFill);
        col.Add(barOuter);

        var lbl = new Label("") { name = lblName };
        lbl.AddToClassList("popup-res-value");
        col.Add(lbl);

        return col;
    }

    private static bool IsAnchor(FeatureSO f) =>
        (f.category == FeatureSO.FeatureCategory.Gameplay ||
         f.category == FeatureSO.FeatureCategory.Graphic  ||
         f.category == FeatureSO.FeatureCategory.Tech)
        && f.cpuUsage >= 15f && f.prerequisites.Count == 0;

    private static bool IsSupport(FeatureSO f) =>
        f.category == FeatureSO.FeatureCategory.Sound     ||
        f.category == FeatureSO.FeatureCategory.Narrative ||
        f.category == FeatureSO.FeatureCategory.UX;
}