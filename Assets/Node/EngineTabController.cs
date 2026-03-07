// ============================================================
//  EngineTabController.cs  —  NODE-TYCOON
//
//  First-draft Engine Lab tab.
//  Responsibilities:
//    • List all EngineSO assets the player has developed
//    • Show stats and components of the selected engine
//    • Wire "New Engine", "Sell", "Fork", "Finalise" buttons
//
//  Data model: EngineSO  (ScriptableObject, defined below)
//  This controller is intentionally simple — employees,
//  research gating, and the sell market come later.
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class EngineTabController : MonoBehaviour
{
    [Header("Template")]
    public VisualTreeAsset engineTabTemplate;

    [Header("Available Engines (assign in Inspector)")]
    public List<EngineSO> availableEngines = new List<EngineSO>();

    private VisualElement  _root;
    private ScrollView     _engineList;
    private VisualElement  _detailEmpty;
    private VisualElement  _detail;
    private EngineSO       _selected;

    // ── Setup ─────────────────────────────────────────────────
    public void SetupEngineTab(VisualElement container)
    {
        container.Clear();
        var inst = engineTabTemplate.Instantiate();
        inst.style.flexGrow = 1;
        container.Add(inst);
        _root = inst;

        _engineList  = _root.Q<ScrollView>("EngineList");
        _detailEmpty = _root.Q<VisualElement>("EngineDetailEmpty");
        _detail      = _root.Q<VisualElement>("EngineDetail");

        _root.Q<Button>("BtnNewEngine")?.RegisterCallback<ClickEvent>(_ => OnNewEngine());

        RebuildList();
        ShowDetail(null);
    }

    // ── Engine list ───────────────────────────────────────────
    private void RebuildList()
    {
        _engineList.Clear();
        if (availableEngines.Count == 0)
        {
            var hint = new Label("Noch keine Engines entwickelt.")
            {
                style = { color = new Color(0.28f, 0.33f, 0.41f), fontSize = 11,
                          unityTextAlign = TextAnchor.MiddleCenter, marginTop = 20 }
            };
            _engineList.Add(hint);
            return;
        }

        foreach (var eng in availableEngines)
        {
            var card = BuildEngineCard(eng);
            _engineList.Add(card);
        }
    }

    private VisualElement BuildEngineCard(EngineSO eng)
    {
        var card = new VisualElement();
        card.AddToClassList("et-engine-card");
        if (eng == _selected) card.AddToClassList("et-engine-card--selected");

        var nameLabel = new Label(eng.engineName);
        nameLabel.AddToClassList("et-engine-card-name");
        card.Add(nameLabel);

        var meta = new Label($"v{eng.version}  ·  CPU {eng.cpuBudget}  ·  {eng.components.Count} Komp.");
        meta.AddToClassList("et-engine-card-meta");
        card.Add(meta);

        var statusLabel = new Label(StatusText(eng.status));
        statusLabel.AddToClassList("et-engine-card-status");
        statusLabel.AddToClassList(StatusClass(eng.status));
        card.Add(statusLabel);

        card.RegisterCallback<ClickEvent>(_ =>
        {
            _selected = eng;
            RebuildList();
            ShowDetail(eng);
        });

        return card;
    }

    // ── Detail panel ──────────────────────────────────────────
    private void ShowDetail(EngineSO eng)
    {
        if (eng == null)
        {
            _detailEmpty?.RemoveFromClassList("hidden");
            _detail?.AddToClassList("hidden");
            return;
        }
        _detailEmpty?.AddToClassList("hidden");
        _detail?.RemoveFromClassList("hidden");

        SetText("DetailEngineName",    eng.engineName);
        SetText("DetailEngineVersion", $"v{eng.version}");
        SetText("DetailEngineDesc",    eng.description);
        SetText("DetailEngineStatus",  StatusText(eng.status).ToUpper());
        SetText("DetailCpu",           eng.cpuBudget.ToString());
        SetText("DetailRam",           eng.ramBudget.ToString());
        SetText("DetailGfx",           $"+{eng.graphicBonus}");
        SetText("DetailAudio",         $"+{eng.audioBonus}");
        SetText("DetailCost",          eng.licenseCostPerWeek > 0 ? $"${eng.licenseCostPerWeek}" : "Frei");

        // Status badge class
        var badge = _detail.Q<Label>("DetailEngineStatus");
        if (badge != null)
        {
            badge.RemoveFromClassList("et-status-wip");
            badge.RemoveFromClassList("et-status-finished");
            badge.RemoveFromClassList("et-status-for-sale");
            badge.AddToClassList(StatusBadgeClass(eng.status));
        }

        // Components
        var compGrid = _detail.Q<VisualElement>("ComponentGrid");
        if (compGrid != null)
        {
            compGrid.Clear();
            foreach (var comp in eng.components)
                compGrid.Add(BuildComponentCard(comp));
        }

        // Footer buttons
        _detail.Q<Button>("BtnSellEngine")?.RegisterCallback<ClickEvent>(_ => OnSell(eng));
        _detail.Q<Button>("BtnForkEngine")?.RegisterCallback<ClickEvent>(_ => OnFork(eng));
        _detail.Q<Button>("BtnFinalise")  ?.RegisterCallback<ClickEvent>(_ => OnFinalise(eng));
        _detail.Q<Button>("BtnAddComponent")?.RegisterCallback<ClickEvent>(_ => OnAddComponent(eng));
    }

    private VisualElement BuildComponentCard(EngineComponentData comp)
    {
        var card = new VisualElement();
        card.AddToClassList("et-component-card");
        card.AddToClassList($"et-component-card--{comp.componentType.ToLower()}");

        var typeLabel = new Label(comp.componentType.ToUpper());
        typeLabel.AddToClassList("et-component-type");
        card.Add(typeLabel);

        var nameLabel = new Label(comp.componentName);
        nameLabel.AddToClassList("et-component-name");
        card.Add(nameLabel);

        var cpuLabel = new Label($"CPU +{comp.cpuCost}");
        cpuLabel.AddToClassList("et-component-cpu");
        card.Add(cpuLabel);

        return card;
    }

    // ── Actions ───────────────────────────────────────────────
    private void OnNewEngine()
    {
        // TODO: open a "New Engine" dialog or creation screen
        Debug.Log("[EngineTab] Neue Engine erstellen — noch nicht implementiert.");
    }

    private void OnSell(EngineSO eng)
    {
        Debug.Log($"[EngineTab] Verkaufe Engine: {eng.engineName} — Marktplatz kommt später.");
    }

    private void OnFork(EngineSO eng)
    {
        Debug.Log($"[EngineTab] Neue Version von '{eng.engineName}' ableiten — kommt später.");
    }

    private void OnFinalise(EngineSO eng)
    {
        eng.status = EngineSO.EngineStatus.Finished;
        ShowDetail(eng);
        RebuildList();
        Debug.Log($"[EngineTab] Engine '{eng.engineName}' fertiggestellt.");
    }

    private void OnAddComponent(EngineSO eng)
    {
        Debug.Log($"[EngineTab] Komponente zu '{eng.engineName}' hinzufügen — Forschungsbaum kommt später.");
    }

    // ── Helpers ───────────────────────────────────────────────
    private void SetText(string name, string text)
    {
        var lbl = _detail?.Q<Label>(name);
        if (lbl != null) lbl.text = text;
    }

    private static string StatusText(EngineSO.EngineStatus s) => s switch
    {
        EngineSO.EngineStatus.InDevelopment => "In Entwicklung",
        EngineSO.EngineStatus.Finished      => "Fertig",
        EngineSO.EngineStatus.ForSale       => "Zum Verkauf",
        _                                   => "—"
    };
    private static string StatusClass(EngineSO.EngineStatus s) => s switch
    {
        EngineSO.EngineStatus.InDevelopment => "et-engine-card-status--wip",
        EngineSO.EngineStatus.Finished      => "et-engine-card-status--finished",
        EngineSO.EngineStatus.ForSale       => "et-engine-card-status--for-sale",
        _                                   => ""
    };
    private static string StatusBadgeClass(EngineSO.EngineStatus s) => s switch
    {
        EngineSO.EngineStatus.InDevelopment => "et-status-wip",
        EngineSO.EngineStatus.Finished      => "et-status-finished",
        EngineSO.EngineStatus.ForSale       => "et-status-for-sale",
        _                                   => "et-status-wip"
    };
}