using UnityEngine;
using UnityEngine.UIElements;
using System.Linq; // WICHTIG für .Select()
using System.Collections.Generic;

public class UIController : MonoBehaviour
{
    public VisualTreeAsset financeTemplate; // Hier die FinanzenPage.uxml im Editor reinziehen
    public VisualTreeAsset newGameTemplate; // Hier die newGameTemplate.uxml im Editor reinziehen

    public VisualTreeAsset researchTemplate; // Neues UXML für den Tree

    public VisualTreeAsset baukastenTemplate; // Das neue Feature-UXML im Editor zuweisen

    [Header("Controller Referenzen")]
    // Diese musst du im Unity Inspector zuweisen!
    public ResearchTreeController researchController;
    public GameCreatorController creatorController;


    private VisualElement _contentArea;
    private VisualElement _popup;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _contentArea = root.Q<VisualElement>("ActivePage");
        _popup = root.Q<VisualElement>("PopupOverlay");

        // Buttons binden
        root.Q<Button>("BtnFinanzen").clicked += () => ShowFinanzen();
        root.Q<Button>("BtnClosePopup").clicked += () => _popup.AddToClassList("hidden");

        root.Q<Button>("BtnNewGame").clicked += () => ShowNewGame();
        root.Q<Button>("BtnResearch").clicked += () => ShowResearch(); // NEUER BUTTON
    }

    void ShowFinanzen()
    {
        _contentArea.Clear();
        var content = financeTemplate.Instantiate();
        _contentArea.Add(content);

        // Kachel-Klick für Popup
        content.Q<Button>("Tile_Konto").clicked += () => {
            _popup.RemoveFromClassList("hidden");
        };
    }

    void ShowResearch()
        {
            _contentArea.Clear();
            var content = researchTemplate.Instantiate();
            content.style.flexGrow = 1;
            _contentArea.Add(content);

            researchController.SetupResearchTree(content);
        }

    void ShowNewGame()
    {
        _contentArea.Clear();
        var content = newGameTemplate.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        // Hier nur noch Dropdowns & Name, kein Board mehr!
        creatorController.SetupDropdowns(content);

        // Der Start-Button sammelt jetzt einfach die Daten
        content.Q<Button>("BtnStartGame").clicked += () => {
            // Logik zum Erstellen des Spiels ohne Baukasten
        };
    }

void ShowBaukasten(GameCreatorController controller, VisualElement basicDataRoot)
{
    // 1. DATEN SICHERN
    GameData data = new GameData();
    data.gameName = basicDataRoot.Q<TextField>("InputGameName").value;
    
    string g1Name = basicDataRoot.Q<DropdownField>("DropdownGenre").value;
    data.genre = controller.genreDB.genres.Find(g => g.genreName == g1Name);

    string targetName = basicDataRoot.Q<DropdownField>("DropdownAudience").value;
    data.targetGroup = controller.targetDB.targetGroups.Find(tg => tg.displayName == targetName);

    // 2. Neue UI vorbereiten
    _contentArea.Clear();


    // Zwinge die ContentArea (ActivePage) auf volle Größe
    _contentArea.style.flexGrow = 1;
    _contentArea.style.height = Length.Percent(100);

    // NUR EINMAL instanziieren!
    TemplateContainer baukastenUI = baukastenTemplate.Instantiate();
    
    // WICHTIG: Die Styles müssen auf die Instanz angewendet werden, die in die Area kommt
    baukastenUI.style.flexGrow = 1;
    baukastenUI.style.width = Length.Percent(100);
    baukastenUI.style.height = Length.Percent(100);

    // 3. Spezieller Fix für ScrollViews (ActivePage)
    // Wir müssen den internen Container des ScrollViews finden und aufblasen
    var scrollContent = _contentArea.Q("unity-content-container");
    if (scrollContent != null) 
    {
        scrollContent.style.flexGrow = 1;
        scrollContent.style.height = Length.Percent(100);
    }
    
    _contentArea.Add(baukastenUI);

    // 3. Baukasten initialisieren
    controller.SetupBaukasten(baukastenUI);

    // 4. Finaler Start-Button
    var finalBtn = baukastenUI.Q<Button>("BtnStartFinalDev");
    if (finalBtn != null)
    {
        finalBtn.clicked += () => 
        {
            data.selectedFeatures = controller.GetSelectedFeatures();
            GameManager.Instance.CreateNewGame(data);
            
            Debug.Log($"Spiel {data.gameName} mit {data.selectedFeatures.Count} Features gestartet!");
            
            _contentArea.Clear();
            _contentArea.Add(new Label("Entwicklung läuft..."));
        };
    }
}
}