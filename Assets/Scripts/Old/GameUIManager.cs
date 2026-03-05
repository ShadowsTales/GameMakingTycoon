using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class GameUIManager : MonoBehaviour
{
    [Header("Databases")]
    public GenreDatabase genreDatabase;
    public targetGroupDatabase targetGroup;
    public topicDatabase topicDatabase;
    

    [Header("UI Inputs")]
    public TMP_InputField nameInput;
    public TMP_Dropdown genreDropdown;
    public TMP_Dropdown secondgenreDropdown;
    public TMP_Dropdown targetDropdown;
    public TMP_Dropdown topicDropdown;
    public TMP_Dropdown secondTopicDropdown;

    public Slider gameplaySlider;
    public Slider graphicsSlider;
    public Slider storySlider;

    [Header("Paging System")]
    public List<GameObject> pages;
    public GameObject backButton;
    public Button navigationButton;
    public TextMeshProUGUI navigationButtonText;

    private int currentPage = 0;

    private void OnEnable()
    {
        // Dropdowns jedes Mal korrekt initialisieren
        PopulateDropdown(genreDropdown, genreDatabase.genres, g => g.genreName);
        PopulateDropdown(secondgenreDropdown, genreDatabase.genres, g => g.genreName);

        PopulateDropdown(targetDropdown, targetGroup.targetGroups, t => t.group.ToString());


        PopulateDropdown(topicDropdown, topicDatabase.topics, t => t.topicName);
        PopulateDropdown(secondTopicDropdown, topicDatabase.topics, t => t.topicName);

        // Paging zurücksetzen
        currentPage = 0;
        UpdateUI();

        // Layout sofort korrekt berechnen
        ForceLayoutRebuild();
    }

    private void Start()
    {
        // Button-Event nur einmal registrieren
        navigationButton.onClick.AddListener(OnNavigationClicked);
    }

    public void PopulateDropdown<T>(TMP_Dropdown dropdown, List<T> items, Func<T, string> getLabel)
    {
        dropdown.ClearOptions();

        List<string> option = new List<string>();
        foreach (var item in items)
        {
            option.Add(getLabel(item));
        }

        dropdown.AddOptions(option);
    }

    private void OnNavigationClicked()
    {
        if (currentPage == pages.Count - 1)
        {
            CreateGame();
        }
        else
        {
            NextPage();
        }
    }

    public void NextPage()
    {
        if (currentPage < pages.Count - 1)
        {
            currentPage++;
            UpdateUI();
            ForceLayoutRebuild();
        }
    }

    public void BackPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdateUI();
            ForceLayoutRebuild();
        }
    }

    private void UpdateUI()
    {
        // Nur die aktuelle Page aktivieren
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].SetActive(i == currentPage);
        }

        // Back-Button nur ab Page 1 anzeigen
        backButton.SetActive(currentPage > 0);

        // Next/Create Button Text
        if (currentPage == pages.Count - 1)
        {
            navigationButtonText.text = "Create Game";
        }
        else
        {
            navigationButtonText.text = "Next";
        }
    }

    private void ForceLayoutRebuild()
    {
        // Wichtig: verhindert UI-Sprünge beim ersten Öffnen
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(pages[currentPage].GetComponent<RectTransform>());
    }

   public void CreateGame()
{
    GameData newGame = new GameData();

    newGame.gameName = nameInput.text;

    newGame.genre = genreDatabase.genres[genreDropdown.value];
    newGame.secondGenre = genreDatabase.genres[secondgenreDropdown.value];

    newGame.topic = topicDatabase.topics[topicDropdown.value];
    newGame.secondTopic = topicDatabase.topics[secondTopicDropdown.value];

    newGame.targetGroup = targetGroup.targetGroups[targetDropdown.value];

    newGame.gameplayFocus = gameplaySlider.value;
    newGame.graphicsFocus = graphicsSlider.value;
    newGame.storyFocus = storySlider.value;

    newGame.releaseWeek = GameTimeManager.Instance.currentWeek;

    GameManager.Instance.CreateNewGame(newGame);
    
    GameSalesManager.Instance.AddGame(newGame);

   
}


}
