// ============================================================
//  UIController.cs  —  NODE-TYCOON
//
//  Master page router.  Handles:
//    • Nav bar button → page routing
//    • 4-step New Game wizard
//    • NodeGraph screen hand-off
//    • ENGINE LAB tab (new)
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
    // ── Templates ─────────────────────────────────────────────────
    [Header("UXML Templates")]
    public VisualTreeAsset financeTemplate;
    [FormerlySerializedAs("newGameTemplate")]
    public VisualTreeAsset gameCreatorTemplate;
    public VisualTreeAsset researchTemplate;
    public VisualTreeAsset nodeGraphTemplate;
    public VisualTreeAsset engineTabTemplate;      // NEW — EngineTab.uxml

    // ── Controllers ───────────────────────────────────────────────
    [Header("Controller Referenzen")]
    public ResearchTreeController researchController;
    public GameCreatorController  creatorController;
    public NodeGraphController    nodeGraphController;
    public EngineTabController    engineTabController;  // NEW

    // ── Runtime ───────────────────────────────────────────────────
    private VisualElement _contentArea;
    private VisualElement _popup;
    private GameData      _pendingGameData = new GameData();
    private int           _currentStep     = 1;
    private const int     TOTAL_STEPS      = 4;

    // ══════════════════════════════════════════════════════════════
    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _contentArea = root.Q<VisualElement>("ActivePage");
        _popup       = root.Q<VisualElement>("PopupOverlay");

        root.Q<Button>("BtnDashboard")?.RegisterCallback<ClickEvent>(_ => ShowDashboard());
        root.Q<Button>("BtnNewGame")  ?.RegisterCallback<ClickEvent>(_ => ShowNewGame());
        root.Q<Button>("BtnFinanzen") ?.RegisterCallback<ClickEvent>(_ => ShowFinanzen());
        root.Q<Button>("BtnResearch") ?.RegisterCallback<ClickEvent>(_ => ShowResearch());
        root.Q<Button>("BtnPersonal") ?.RegisterCallback<ClickEvent>(_ => ShowPersonal());
        root.Q<Button>("R")   ?.RegisterCallback<ClickEvent>(_ => ShowEngineTab()); // NEW
        root.Q<Button>("BtnClosePopup")?.RegisterCallback<ClickEvent>(_ =>
            _popup.AddToClassList("hidden"));
    }

    // ══════════════════════════════════════════════════════════════
    //  SIMPLE PAGES
    // ══════════════════════════════════════════════════════════════
    void ShowDashboard()
    {
        _contentArea.Clear();
        _contentArea.Add(new Label("Dashboard") { style = { color = Color.white, fontSize = 20 } });
    }
    void ShowPersonal()
    {
        _contentArea.Clear();
        _contentArea.Add(new Label("Personal") { style = { color = Color.white, fontSize = 20 } });
    }
    void ShowFinanzen()
    {
        _contentArea.Clear();
        var c = financeTemplate.Instantiate();
        _contentArea.Add(c);
        c.Q<Button>("Tile_Konto")?.RegisterCallback<ClickEvent>(_ =>
            _popup.RemoveFromClassList("hidden"));
    }
    void ShowResearch()
    {
        _contentArea.Clear();
        var c = researchTemplate.Instantiate();
        c.style.flexGrow = 1;
        _contentArea.Add(c);
        researchController.SetupResearchTree(c);
    }

    // ══════════════════════════════════════════════════════════════
    //  ENGINE TAB  (new)
    // ══════════════════════════════════════════════════════════════
    void ShowEngineTab()
    {
        _contentArea.Clear();
        _contentArea.style.flexGrow = 1;
        _contentArea.style.height   = Length.Percent(100);

        if (engineTabController != null)
        {
            engineTabController.SetupEngineTab(_contentArea);
        }
        else
        {
            // Fallback if controller not yet assigned
            var c = engineTabTemplate?.Instantiate();
            if (c != null) { c.style.flexGrow = 1; _contentArea.Add(c); }
            else _contentArea.Add(new Label("Engine Tab — assign EngineTabController in Inspector")
                 { style = { color = Color.white, fontSize = 14 } });
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  NEW GAME: 4-STEP WIZARD
    //
    //  Step 1 — Konzept  : Name, Genre, Engine, Topic
    //  Step 2 — Markt    : Audience, Focus-Sliders, Platform, Marketing
    //  Step 3 — Technik  : Licenses, Consoles
    //  Step 4 — Übersicht: Summary + score preview → opens NodeGraph
    // ══════════════════════════════════════════════════════════════
    void ShowNewGame()
    {
        _contentArea.Clear();
        _pendingGameData = new GameData();
        _currentStep     = 1;

        _contentArea.style.flexGrow = 1;
        _contentArea.style.height   = Length.Percent(100);
        var sc = _contentArea.Q("unity-content-container");
        if (sc != null) { sc.style.flexGrow = 1; sc.style.height = Length.Percent(100); }

        var c = gameCreatorTemplate.Instantiate();
        c.style.flexGrow = 1;
        c.style.width    = Length.Percent(100);
        c.style.height   = Length.Percent(100);
        _contentArea.Add(c);

        // Populate dropdowns from databases
        PopulateDropdowns(c);

        // Wire engine card preview
        WireEngineDropdown(c);

        // Wire genre loop preview
        WireGenreLoopPreview(c);

        // Wire focus sliders
        WireFocusSliders(c);

        // Show step 1
        ShowStep(c, 1);

        // Back / Next / StartDev
        c.Q<Button>("BtnBack")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_currentStep > 1) ShowStep(c, --_currentStep);
        });
        c.Q<Button>("BtnNext")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_currentStep < TOTAL_STEPS) ShowStep(c, ++_currentStep);
        });
        c.Q<Button>("BtnStartDev")?.RegisterCallback<ClickEvent>(_ =>
        {
            CollectAllData(c);
            ShowNodeGraph(_pendingGameData);
        });
    }

    // ── Step navigation ───────────────────────────────────────────
    private void ShowStep(VisualElement root, int step)
    {
        for (int i = 1; i <= TOTAL_STEPS; i++)
        {
            var panel = root.Q<VisualElement>($"Panel{i}");
            if (panel == null) continue;
            if (i == step) panel.RemoveFromClassList("hidden");
            else           panel.AddToClassList("hidden");

            var ind = root.Q<VisualElement>($"StepIndicator{i}");
            if (ind == null) continue;
            ind.RemoveFromClassList("step-active");
            ind.RemoveFromClassList("step-done");
            if (i == step)     ind.AddToClassList("step-active");
            else if (i < step) ind.AddToClassList("step-done");
        }
        ToggleHidden(root.Q<Button>("BtnBack"),     step <= 1);
        ToggleHidden(root.Q<Button>("BtnNext"),     step >= TOTAL_STEPS);
        ToggleHidden(root.Q<Button>("BtnStartDev"), step < TOTAL_STEPS);

        if (step == TOTAL_STEPS) RefreshSummary(root);
    }

    private void ToggleHidden(VisualElement el, bool hide)
    {
        if (el == null) return;
        if (hide) el.AddToClassList("hidden");
        else      el.RemoveFromClassList("hidden");
    }

    // ── Genre loop labels (Step 1) ────────────────────────────────
    private void WireGenreLoopPreview(VisualElement root)
    {
        var dd = root.Q<DropdownField>("DropdownGenre");
        if (dd == null) return;

        void Refresh(string genreName)
        {
            (string trigger, string resolution) = genreName?.ToLower() switch
            {
                var g when g.Contains("rpg")        => ("Quest erhalten",           "Quest abgeschlossen"),
                var g when g.Contains("action")     => ("Bedrohung / Feind",        "Sieg / Überleben"),
                var g when g.Contains("puzzle")     => ("Problem-Zustand",          "Lösung gefunden"),
                var g when g.Contains("strategy")   => ("Ressourcen-Druck",         "Entscheidung + Konsequenz"),
                var g when g.Contains("simulation") => ("System-Ungleichgewicht",   "Gleichgewicht"),
                var g when g.Contains("adventure")  => ("Entdeckung",               "Fortschritt"),
                var g when g.Contains("sport")      => ("Wettkampf beginnt",        "Ergebnis / Sieg"),
                _                                   => ("Spieler-Aktion",           "Spieler-Belohnung"),
            };
            SetLabelText(root, "LoopTrigger",    $"▶  Trigger:     {trigger}");
            SetLabelText(root, "LoopResolution", $"■  Resolution:  {resolution}");
        }

        dd.RegisterValueChangedCallback(e => Refresh(e.newValue));
        Refresh(dd.value);
    }

    // ── Populate all dropdowns ────────────────────────────────────
    private void PopulateDropdowns(VisualElement root)
    {
        var none = new List<string> { "— Keines —" };

        SetChoices(root, "DropdownGenre",
            creatorController.genreDB.genres.Select(g => g.genreName).ToList());
        SetChoices(root, "DropdownSecondGenre",
            none.Concat(creatorController.genreDB.genres.Select(g => g.genreName)).ToList());
        SetChoices(root, "DropdownTopic",
            creatorController.topicDB.topics.Select(t => t.topicName).ToList());
        SetChoices(root, "DropdownSecondTopic",
            none.Concat(creatorController.topicDB.topics.Select(t => t.topicName)).ToList());
        SetChoices(root, "DropdownAudience",
            creatorController.targetDB.targetGroups.Select(tg => tg.displayName).ToList());
        SetChoices(root, "DropdownRating",
            new List<string> { "USK 0", "USK 6", "USK 12", "USK 16", "USK 18" });
        SetChoices(root, "DropdownMarketing",
            new List<string> { "Kein Budget", "Klein (5.000 €)", "Mittel (20.000 €)", "Groß (100.000 €)" });
        SetChoices(root, "DropdownPlatform",
            new List<string> { "PC", "Konsole", "Mobil", "PC + Konsole", "Alle Plattformen" });
        // Engine choices — real EngineSOs if available, otherwise placeholders
        var engineNames = engineTabController?.availableEngines?
            .Where(e => e.status == EngineSO.EngineStatus.Finished)
            .Select(e => e.engineName)
            .ToList();
        if (engineNames == null || engineNames.Count == 0)
            engineNames = new List<string> { "Basic Engine", "Advanced Engine", "Pro Engine" };
        SetChoices(root, "DropdownEngine", engineNames);
    }

    private void SetChoices(VisualElement root, string name, List<string> choices)
    {
        var dd = root.Q<DropdownField>(name);
        if (dd == null) return;
        dd.choices = choices;
        if (choices.Count > 0 && string.IsNullOrEmpty(dd.value)) dd.value = choices[0];
    }

    // ── Engine stats preview (Step 1) ─────────────────────────────
    private void WireEngineDropdown(VisualElement root)
    {
        var dd = root.Q<DropdownField>("DropdownEngine");
        if (dd == null) return;
        // Choices already set by PopulateDropdowns — just wire the refresh callback.

        void Refresh(string name)
        {
            // Try to find a real EngineSO
            var eng = engineTabController?.availableEngines?
                .Find(e => e.engineName == name);

            var cardName    = root.Q<Label>("EngineCardName");
            if (cardName != null) cardName.text = name;

            SetLabelText(root, "EngineStatCpu",   eng != null ? $"{eng.cpuBudget}" : "—");
            SetLabelText(root, "EngineStatRam",   eng != null ? $"{eng.ramBudget}" : "—");
            SetLabelText(root, "EngineStatGfx",   eng != null ? $"+{eng.graphicBonus}" : "—");
            SetLabelText(root, "EngineStatAudio", eng != null ? $"+{eng.audioBonus}"   : "—");
            SetLabelText(root, "EngineStatCost",  eng != null
                ? (eng.licenseCostPerWeek > 0 ? $"${eng.licenseCostPerWeek}" : "Frei")
                : "—");

            // Component tags
            var tagContainer = root.Q<VisualElement>("EngineComponentTags");
            if (tagContainer != null)
            {
                tagContainer.Clear();
                if (eng != null)
                {
                    foreach (var comp in eng.components)
                    {
                        var tag = new Label(comp.componentName);
                        tag.AddToClassList("gc-component-tag");
                        tag.AddToClassList($"gc-component-tag--{comp.componentType.ToLower()}");
                        tagContainer.Add(tag);
                    }
                }
            }
        }

        dd.RegisterValueChangedCallback(e => Refresh(e.newValue));
        Refresh(dd.value);
    }

    // ── Focus sliders ─────────────────────────────────────────────
    private void WireFocusSliders(VisualElement root)
    {
        var slGP  = root.Q<Slider>("SliderGameplay");
        var slGFX = root.Q<Slider>("SliderGraphics");
        var slSND = root.Q<Slider>("SliderSound");
        if (slGP == null) return;

        void Refresh()
        {
            int gp  = Mathf.RoundToInt(slGP.value);
            int gfx = Mathf.RoundToInt(slGFX?.value ?? 33f);
            int snd = Mathf.RoundToInt(slSND?.value ?? 34f);
            int tot = gp + gfx + snd;
            SetLabelText(root, "LabelGameplayVal", gp.ToString());
            SetLabelText(root, "LabelGraphicsVal", gfx.ToString());
            SetLabelText(root, "LabelSoundVal",    snd.ToString());
            var totLabel = root.Q<Label>("LabelFocusTotal");
            if (totLabel != null)
            {
                totLabel.text = $"{tot} / 100";
                totLabel.RemoveFromClassList("gc-focus-total-val--bad");
                totLabel.RemoveFromClassList("gc-focus-total-val--ok");
                totLabel.AddToClassList(tot == 100 ? "gc-focus-total-val--ok" : "gc-focus-total-val--bad");
            }
        }
        slGP.RegisterValueChangedCallback(_  => Refresh());
        slGFX?.RegisterValueChangedCallback(_ => Refresh());
        slSND?.RegisterValueChangedCallback(_ => Refresh());
        Refresh();
    }

    // ── Collect all form data ─────────────────────────────────────
    private void CollectAllData(VisualElement root)
    {
        _pendingGameData.gameName = root.Q<TextField>("InputGameName")?.value ?? "Unbekannt";

        string gn  = root.Q<DropdownField>("DropdownGenre")?.value;
        _pendingGameData.genre       = creatorController.genreDB.genres.Find(g => g.genreName == gn);

        string g2n = root.Q<DropdownField>("DropdownSecondGenre")?.value;
        _pendingGameData.secondGenre = creatorController.genreDB.genres.Find(g => g.genreName == g2n);

        string tn  = root.Q<DropdownField>("DropdownTopic")?.value;
        _pendingGameData.topic       = creatorController.topicDB.topics.Find(t => t.topicName == tn);

        string t2n = root.Q<DropdownField>("DropdownSecondTopic")?.value;
        _pendingGameData.secondTopic = creatorController.topicDB.topics.Find(t => t.topicName == t2n);

        string an  = root.Q<DropdownField>("DropdownAudience")?.value;
        _pendingGameData.targetGroup = creatorController.targetDB.targetGroups
                                       .Find(tg => tg.displayName == an);

        _pendingGameData.gameplayFocus = (root.Q<Slider>("SliderGameplay")?.value ?? 33f) / 100f;
        _pendingGameData.graphicsFocus = (root.Q<Slider>("SliderGraphics")?.value ?? 33f) / 100f;
        _pendingGameData.storyFocus    = (root.Q<Slider>("SliderSound")?.value    ?? 34f) / 100f;
    }

    // ── Summary (step 4) ──────────────────────────────────────────
    private void RefreshSummary(VisualElement root)
    {
        CollectAllData(root);
        var d = _pendingGameData;

        SetLabelText(root, "SummaryName",    d.gameName ?? "—");
        SetLabelText(root, "SummaryGenre",
            (d.genre?.genreName ?? "—") +
            (d.secondGenre != null ? " / " + d.secondGenre.genreName : ""));
        SetLabelText(root, "SummaryTopic",
            (d.topic?.topicName ?? "—") +
            (d.secondTopic != null ? " / " + d.secondTopic.topicName : ""));
        SetLabelText(root, "SummaryAudience",  d.targetGroup?.displayName ?? "—");
        SetLabelText(root, "SummaryPlatform",  root.Q<DropdownField>("DropdownPlatform")?.value ?? "—");
        SetLabelText(root, "SummaryMarketing", root.Q<DropdownField>("DropdownMarketing")?.value ?? "—");
        SetLabelText(root, "SummaryEngine",    root.Q<DropdownField>("DropdownEngine")?.value    ?? "—");
        SetLabelText(root, "SummaryFocusGameplay", $"⚡ Gameplay:  {d.gameplayFocus * 100:0}%");
        SetLabelText(root, "SummaryFocusGraphics", $"🎨 Grafik:    {d.graphicsFocus * 100:0}%");
        SetLabelText(root, "SummaryFocusSound",    $"🔊 Sound:     {d.storyFocus * 100:0}%");

        // Score preview (Coherence pillar only — full score comes from the node graph)
        float baseQ    = d.genre?.CalculateQuality(d) ?? 0f;
        float topicSyn = (d.topic != null && d.genre != null) ? d.topic.GetSynergy(d.genre) : 0.5f;
        float preview  = Mathf.Clamp(baseQ * topicSyn * 10f, 0f, 10f);

        SetLabelText(root, "ScorePreviewValue",  $"{preview:0.0}");
        SetLabelText(root, "ScorePreviewBig",    $"{preview:0.0}");
        SetLabelText(root, "ScoreCoherence",     $"Kohärenz:  {baseQ * 100:0}%");
        SetLabelText(root, "ScoreTechFit",       "Tech-Fit:  (nach Feature-Editor)");
        SetLabelText(root, "ScoreDepth",         "Tiefe:     (nach Feature-Editor)");
        SetLabelText(root, "ScoreSynergy",       topicSyn > 0.7f ? "✓ Gute Genre-Thema Synergie" : "⚠ Schwache Synergie");

        // Synergy box on step 1
        SetLabelText(root, "SynergyValue", $"{topicSyn * 100:0}% Passung");
    }

    // ══════════════════════════════════════════════════════════════
    //  NODE GRAPH SCREEN
    // ══════════════════════════════════════════════════════════════
    void ShowNodeGraph(GameData gameData)
    {
        _contentArea.Clear();
        _contentArea.style.flexGrow = 1;
        _contentArea.style.height   = Length.Percent(100);
        var sc = _contentArea.Q("unity-content-container");
        if (sc != null) { sc.style.flexGrow = 1; sc.style.height = Length.Percent(100); }

        var templateContainer = nodeGraphTemplate.Instantiate();
        templateContainer.style.flexGrow = 1;
        templateContainer.style.width    = Length.Percent(100);
        templateContainer.style.height   = Length.Percent(100);
        _contentArea.Add(templateContainer);
        VisualElement c = templateContainer;

        SetLabelText(c, "LabelGameName", gameData?.gameName ?? "Neues Spiel");

        nodeGraphController.ActiveGameData = gameData;
        nodeGraphController.ActiveGenre    = gameData?.genre;
        nodeGraphController.maxCpu         = GetCpuBudgetForGameData(gameData);
        nodeGraphController.SetupGraph(c);

        nodeGraphController.OnGameFinalized += (result) =>
        {
            if (result == null || !result.IsValid) return;
            _pendingGameData.selectedFeatures = result.SelectedFeatures;
            Debug.Log($"[UIController] '{_pendingGameData.gameName}' — {result.Summary}");
            GameManager.Instance?.CreateNewGame(_pendingGameData);

            _contentArea.Clear();
            _contentArea.Add(new Label($"'{_pendingGameData.gameName}' wird entwickelt…")
            {
                style = { color = Color.white, fontSize = 18,
                           unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }
            });
        };

        WireSidebarTabs(c);
        WirePillarTabs(c);
    }

    private float GetCpuBudgetForGameData(GameData data)
    {
        if (data == null) return 120f;
        int year = 1972 + (GameTimeManager.Instance?.currentWeek ?? 0) / 52;
        return year switch
        {
            < 1977 => 80f,
            < 1982 => 100f,
            < 1985 => 120f,
            < 1990 => 140f,
            _      => 180f,
        };
    }

    // ── Sidebar tabs (NodeGraph) ───────────────────────────────────
    private void WireSidebarTabs(VisualElement root)
    {
        var tabF   = root.Q<Button>("SidebarTabFeatures");
        var tabG   = root.Q<Button>("SidebarTabGenre");
        var panelF = root.Q<ScrollView>("FeatureList");
        var panelG = root.Q<ScrollView>("GenrePanel");
        if (tabF == null || tabG == null) return;

        tabF.RegisterCallback<ClickEvent>(_ =>
        {
            panelF?.RemoveFromClassList("hidden");
            panelG?.AddToClassList("hidden");
            tabF.AddToClassList("sidebar-tab-active");
            tabG.RemoveFromClassList("sidebar-tab-active");
        });
        tabG.RegisterCallback<ClickEvent>(_ =>
        {
            panelG?.RemoveFromClassList("hidden");
            panelF?.AddToClassList("hidden");
            tabG.AddToClassList("sidebar-tab-active");
            tabF.RemoveFromClassList("sidebar-tab-active");
        });
    }

    private void WirePillarTabs(VisualElement root)
    {
        var tabs = new Dictionary<string, Button>
        {
            { "TabAll",       root.Q<Button>("TabAll") },
            { "TabGameplay",  root.Q<Button>("TabGameplay") },
            { "TabGraphic",   root.Q<Button>("TabGraphic") },
            { "TabSound",     root.Q<Button>("TabSound") },
            { "TabTech",      root.Q<Button>("TabTech") },
            { "TabNarrative", root.Q<Button>("TabNarrative") },
            { "TabUX",        root.Q<Button>("TabUX") },
        };
        foreach (var kvp in tabs)
        {
            if (kvp.Value == null) continue;
            string key = kvp.Key;
            kvp.Value.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (var t in tabs.Values) t?.RemoveFromClassList("tab-selected");
                kvp.Value.AddToClassList("tab-selected");
                FeatureSO.FeatureCategory? filter = key switch
                {
                    "TabGameplay"  => FeatureSO.FeatureCategory.Gameplay,
                    "TabGraphic"   => FeatureSO.FeatureCategory.Graphic,
                    "TabSound"     => FeatureSO.FeatureCategory.Sound,
                    "TabTech"      => FeatureSO.FeatureCategory.Tech,
                    "TabNarrative" => FeatureSO.FeatureCategory.Narrative,
                    "TabUX"        => FeatureSO.FeatureCategory.UX,
                    _              => (FeatureSO.FeatureCategory?)null,
                };
                nodeGraphController.FilterSidebarByPillar(filter);
            });
        }
    }

    // ── Helper ────────────────────────────────────────────────────
    private void SetLabelText(VisualElement root, string name, string text)
    {
        var lbl = root?.Q<Label>(name);
        if (lbl != null) lbl.text = text;
    }
}