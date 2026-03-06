using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

/// <summary>
/// Master UI controller. Manages page routing and wires
/// the 4-step New Game flow into the NodeGraphController.
/// </summary>
public class UIController : MonoBehaviour
{
    // ── Templates ─────────────────────────────────────────────────────
    [Header("UXML Templates")]
    public VisualTreeAsset financeTemplate;     // FinanzenPage.uxml
    [FormerlySerializedAs("newGameTemplate")]
    public VisualTreeAsset gameCreatorTemplate; // GameCreator.uxml  (4-step form)
    public VisualTreeAsset researchTemplate;    // ResearchTree.uxml
    public VisualTreeAsset nodeGraphTemplate;   // NodeGraph.uxml

    // ── Controllers ───────────────────────────────────────────────────
    [Header("Controller Referenzen")]
    public ResearchTreeController researchController;
    public GameCreatorController  creatorController;
    public NodeGraphController    nodeGraphController;

    // ── Runtime ───────────────────────────────────────────────────────
    private VisualElement _contentArea;
    private VisualElement _popup;
    private GameData      _pendingGameData = new GameData();

    // ── Wizard state ──────────────────────────────────────────────────
    private int           _currentStep = 1;
    private const int     TOTAL_STEPS  = 4;

    // ═════════════════════════════════════════════════════════════════
    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _contentArea = root.Q<VisualElement>("ActivePage");
        _popup       = root.Q<VisualElement>("PopupOverlay");

        root.Q<Button>("BtnDashboard") ?.RegisterCallback<ClickEvent>(_ => ShowDashboard());
        root.Q<Button>("BtnNewGame")   ?.RegisterCallback<ClickEvent>(_ => ShowNewGame());
        root.Q<Button>("BtnFinanzen")  ?.RegisterCallback<ClickEvent>(_ => ShowFinanzen());
        root.Q<Button>("BtnResearch")  ?.RegisterCallback<ClickEvent>(_ => ShowResearch());
        root.Q<Button>("BtnPersonal")  ?.RegisterCallback<ClickEvent>(_ => ShowPersonal());
        root.Q<Button>("BtnClosePopup")?.RegisterCallback<ClickEvent>(_ =>
            _popup.AddToClassList("hidden"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  SIMPLE PAGES
    // ═════════════════════════════════════════════════════════════════

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

    // ═════════════════════════════════════════════════════════════════
    //  NEW GAME: 4-STEP WIZARD
    // ═════════════════════════════════════════════════════════════════
    //
    //  Step 1 — Basis-Daten   : Name, Genre, Topic
    //  Step 2 — Markt          : Audience, Focus-Sliders, Marketing, Platform
    //  Step 3 — Technik        : Engine, Licenses, Consoles
    //  Step 4 — Zusammenfassung: Summary + score preview → opens NodeGraph
    //
    // ═════════════════════════════════════════════════════════════════

    void ShowNewGame()
    {
        _contentArea.Clear();
        _pendingGameData = new GameData();
        _currentStep     = 1;

        // Fix ScrollView container to fill height
        _contentArea.style.flexGrow = 1;
        _contentArea.style.height   = Length.Percent(100);
        var sc = _contentArea.Q("unity-content-container");
        if (sc != null) { sc.style.flexGrow = 1; sc.style.height = Length.Percent(100); }

        var c = gameCreatorTemplate.Instantiate();
        c.style.flexGrow = 1;
        c.style.width    = Length.Percent(100);
        c.style.height   = Length.Percent(100);
        _contentArea.Add(c);

        PopulateDropdowns(c);
        WireSliders(c);
        WireEngineDropdown(c);
        WireNavigationButtons(c);
        GoToStep(1, c);
    }

    // ── Populate all dropdowns ─────────────────────────────────────
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

        SetChoices(root, "DropdownEngine",
            new List<string> { "Basic Engine", "Advanced Engine", "Pro Engine" });

        WireSynergyPreview(root);
    }

    private void SetChoices(VisualElement root, string name, List<string> choices)
    {
        var dd = root.Q<DropdownField>(name);
        if (dd == null) return;
        dd.choices = choices;
        if (choices.Count > 0) dd.value = choices[0];
    }

    // ── Focus sliders ──────────────────────────────────────────────
    private void WireSliders(VisualElement root)
    {
        var slGP  = root.Q<Slider>("SliderGameplay");
        var slGFX = root.Q<Slider>("SliderGraphics");
        var slSND = root.Q<Slider>("SliderSound");
        var lGP   = root.Q<Label>("LabelGameplayVal");
        var lGFX  = root.Q<Label>("LabelGraphicsVal");
        var lSND  = root.Q<Label>("LabelSoundVal");
        var lTot  = root.Q<Label>("LabelFocusTotal");
        if (slGP == null) return;

        void Refresh()
        {
            int gp = Mathf.RoundToInt(slGP.value);
            int gfx = Mathf.RoundToInt(slGFX.value);
            int snd = Mathf.RoundToInt(slSND.value);
            int tot = gp + gfx + snd;
            if (lGP  != null) lGP.text  = gp.ToString();
            if (lGFX != null) lGFX.text = gfx.ToString();
            if (lSND != null) lSND.text = snd.ToString();
            if (lTot != null)
            {
                lTot.text = $"{tot} / 100";
                if (tot == 100) lTot.RemoveFromClassList("over-budget");
                else            lTot.AddToClassList("over-budget");
            }
        }
        slGP.RegisterValueChangedCallback(_  => Refresh());
        slGFX.RegisterValueChangedCallback(_ => Refresh());
        slSND.RegisterValueChangedCallback(_ => Refresh());
        Refresh();
    }

    // ── Engine stats preview ───────────────────────────────────────
    private void WireEngineDropdown(VisualElement root)
    {
        var dd = root.Q<DropdownField>("DropdownEngine");
        if (dd == null) return;

        // Placeholder stats — replace with your EngineSO later
        var stats = new Dictionary<string, (int cpu, int ram, int gfx, int cost)>
        {
            { "Basic Engine",    (60,  50,  0,  0)    },
            { "Advanced Engine", (80,  70,  10, 2000) },
            { "Pro Engine",      (100, 100, 25, 8000) },
        };

        void Refresh(string e)
        {
            if (!stats.TryGetValue(e, out var s)) return;
            SetLabelText(root, "EngineStatCpu",  $"{s.cpu}%");
            SetLabelText(root, "EngineStatRam",  $"{s.ram}%");
            SetLabelText(root, "EngineStatGfx",  $"+{s.gfx}%");
            SetLabelText(root, "EngineStatCost", s.cost == 0 ? "Kostenlos" : $"{s.cost:N0} €/Woche");
        }
        dd.RegisterValueChangedCallback(evt => Refresh(evt.newValue));
        Refresh(dd.value);
    }

    // ── Genre + Topic synergy ──────────────────────────────────────
    private void WireSynergyPreview(VisualElement root)
    {
        var ddG = root.Q<DropdownField>("DropdownGenre");
        var ddT = root.Q<DropdownField>("DropdownTopic");
        var lbl = root.Q<Label>("SynergyValue");
        if (ddG == null || ddT == null || lbl == null) return;

        void Refresh()
        {
            var genre = creatorController.genreDB.genres.Find(g => g.genreName == ddG.value);
            var topic = creatorController.topicDB.topics.Find(t => t.topicName == ddT.value);
            if (genre == null || topic == null) { lbl.text = "—"; return; }
            float syn   = topic.GetSynergy(genre);
            int   stars = Mathf.RoundToInt(syn * 5);
            lbl.text = new string('★', stars) + new string('☆', 5 - stars) + $"  ({syn * 100:0}%)";
        }
        ddG.RegisterValueChangedCallback(_ => Refresh());
        ddT.RegisterValueChangedCallback(_ => Refresh());
        Refresh();
    }

    // ── Step navigation ────────────────────────────────────────────
    private void WireNavigationButtons(VisualElement root)
    {
        root.Q<Button>("BtnNext")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_currentStep < TOTAL_STEPS) GoToStep(_currentStep + 1, root);
        });
        root.Q<Button>("BtnBack")?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_currentStep > 1) GoToStep(_currentStep - 1, root);
        });
        root.Q<Button>("BtnStartDev")?.RegisterCallback<ClickEvent>(_ =>
        {
            CollectAllData(root);
            ShowNodeGraph(_pendingGameData);
        });
    }

    private void GoToStep(int step, VisualElement root)
    {
        _currentStep = step;

        for (int i = 1; i <= TOTAL_STEPS; i++)
        {
            var panel = root.Q<VisualElement>($"Panel{i}");
            if (i == step) panel?.RemoveFromClassList("hidden");
            else           panel?.AddToClassList("hidden");

            var ind = root.Q<VisualElement>($"StepIndicator{i}");
            if (ind == null) continue;
            ind.RemoveFromClassList("step-active");
            ind.RemoveFromClassList("step-done");
            if (i == step)    ind.AddToClassList("step-active");
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

    // ── Collect all form data ──────────────────────────────────────
    private void CollectAllData(VisualElement root)
    {
        _pendingGameData.gameName = root.Q<TextField>("InputGameName")?.value ?? "Unbekannt";

        string gn = root.Q<DropdownField>("DropdownGenre")?.value;
        _pendingGameData.genre = creatorController.genreDB.genres.Find(g => g.genreName == gn);

        string g2n = root.Q<DropdownField>("DropdownSecondGenre")?.value;
        _pendingGameData.secondGenre = creatorController.genreDB.genres.Find(g => g.genreName == g2n);

        string tn = root.Q<DropdownField>("DropdownTopic")?.value;
        _pendingGameData.topic = creatorController.topicDB.topics.Find(t => t.topicName == tn);

        string t2n = root.Q<DropdownField>("DropdownSecondTopic")?.value;
        _pendingGameData.secondTopic = creatorController.topicDB.topics.Find(t => t.topicName == t2n);

        string an = root.Q<DropdownField>("DropdownAudience")?.value;
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
        SetLabelText(root, "SummaryEngine",    root.Q<DropdownField>("DropdownEngine")?.value ?? "—");
        SetLabelText(root, "SummaryFocusGameplay", $"Gameplay: {d.gameplayFocus * 100:0}%");
        SetLabelText(root, "SummaryFocusGraphics", $"Grafik:   {d.graphicsFocus * 100:0}%");
        SetLabelText(root, "SummaryFocusSound",    $"Sound:    {d.storyFocus * 100:0}%");

        // Base quality estimate (before features)
        float baseQ     = d.genre?.CalculateQuality(d) ?? 0f;
        float topicSyn  = (d.topic != null && d.genre != null) ? d.topic.GetSynergy(d.genre) : 0.5f;
        float baseScore = Mathf.Clamp(baseQ * topicSyn * 10f, 0f, 10f);
        SetLabelText(root, "ScorePreviewValue", $"{baseScore:0.0}");
    }

    // ═════════════════════════════════════════════════════════════════
    //  NODE GRAPH SCREEN
    // ═════════════════════════════════════════════════════════════════

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

        // Instantiate() returns a TemplateContainer wrapper — the named elements
        // live one level inside it. Q<>() searches descendants so this works,
        // but we pass the wrapper as root so SetupGraph can find "NodeCanvas" etc.
        // The wrapper itself has no name; that's normal and expected.
        VisualElement c = templateContainer;

        SetLabelText(c, "LabelGameName", gameData?.gameName ?? "Neues Spiel");

        // Pre-fill genre dropdown in sidebar
        if (gameData?.genre != null)
        {
            var dd = c.Q<DropdownField>("DropdownGenre");
            if (dd != null)
            {
                dd.choices = creatorController.genreDB.genres.Select(g => g.genreName).ToList();
                dd.value   = gameData.genre.genreName;
            }
        }

        nodeGraphController.SetupGraph(c);

        nodeGraphController.OnGameFinalized += (result) =>
        {
            if (result == null || !result.IsValid) return;
            _pendingGameData.selectedFeatures = result.SelectedFeatures;
            Debug.Log($"[UIController] '{_pendingGameData.gameName}' — {result.Summary}");
            GameManager.Instance?.CreateNewGame(_pendingGameData);

            _contentArea.Clear();
            _contentArea.Add(new Label($"'{_pendingGameData.gameName}' wird entwickelt...")
            {
                style = { color = Color.white, fontSize = 18,
                           unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }
            });
        };

        WireSidebarTabs(c);
        WirePillarTabs(c);
    }

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
            { "TabAll",       root.Q<Button>("TabAll")       },
            { "TabGameplay",  root.Q<Button>("TabGameplay")  },
            { "TabGraphic",   root.Q<Button>("TabGraphic")   },
            { "TabSound",     root.Q<Button>("TabSound")     },
            { "TabTech",      root.Q<Button>("TabTech")      },
            { "TabNarrative", root.Q<Button>("TabNarrative") },
            { "TabUX",        root.Q<Button>("TabUX")        },
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
        var lbl = root.Q<Label>(name);
        if (lbl != null) lbl.text = text;
    }
}