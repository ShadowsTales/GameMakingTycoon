using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public List<GameData> activeProjects = new List<GameData>();

    void Awake()
    {
        Instance = this;
    }


    // Wird vom GameCreatorController aufgerufen
    public void CreateNewGame(GameData data)
    {
        data.currentProgress = 0;
        data.isFinished = false;
        activeProjects.Add(data);
    }

    public List<GameData> releasedGames = new List<GameData>();


    // Diese Methode muss von deinem TimeManager jede Woche aufgerufen werden!
    public void OnWeekPassed()
    {
        for (int i = activeProjects.Count - 1; i >= 0; i--)
        {
            GameData game = activeProjects[i];
            
            // Simpler Fortschritt: Jede Woche 25% (Spiel dauert 4 Wochen)
            game.currentProgress += 25f; 

            if (game.currentProgress >= 100f)
            {
                FinishDevelopment(game);
                activeProjects.RemoveAt(i);
            }
        }
    }

    void FinishDevelopment(GameData game)
    {
        game.isFinished = true;
        // Hier setzen wir die Release-Woche für deine Sales-Statistiken
        // game.releaseWeek = TimeManager.Instance.currentWeek; 
        GameTimeManager.Instance.currentWeek = game.releaseWeek;

        game.isFinished = true;
        releasedGames.Add(game);

        Debug.Log($"Release: {game.gameName} geht in den Verkauf!");
        
        // ÜBERGABE AN DEN SALES MANAGER
        // SalesManager.Instance.AddGame(game);
        GameSalesManager.Instance.AddGame(game);
    }
}
