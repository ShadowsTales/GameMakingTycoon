using UnityEngine;
using System.Collections.Generic;

public class GameSalesManager : MonoBehaviour
{
    public static GameSalesManager Instance;

    private List<GameData> activeGames = new List<GameData>();


    private void Start()
    {
        Instance = this;
        GameTimeManager.Instance.OnWeekPassed += ProcessWeeklySales;
    }

    private void OnDestroy()
    {
        GameTimeManager.Instance.OnWeekPassed -= ProcessWeeklySales;
    }


    public void AddGame(GameData game)
    {
        activeGames.Add(game);
    }

    private void ProcessWeeklySales(int week)
    {
        foreach (var game in activeGames)
        {
            int sales = CalculateWeeklySales(game, week);
            game.totalSales += sales;

            Debug.Log($"{game.gameName} sold {sales} units in week {week}. Total: {game.totalSales}");
        }
    }

    private int CalculateWeeklySales(GameData game, int currentWeek)
    {
        int age = currentWeek - game.releaseWeek;

        float quality = 
            game.gameplayFocus * 0.4f +
            game.graphicsFocus * 0.3f +
            game.storyFocus * 0.3f;

        float hype = 1f + (game.hype / 100f);

        float targetFit = game.genre.GetAppealFor(game.targetGroup);

        //float synergy = game.topic.GetSynergy(game.genre);

        float decay = Mathf.Exp(-age * 0.25f);

        float sales = quality * hype * targetFit  * 1000f * decay;

        int finalSales = Mathf.RoundToInt(sales);

        game.weeklySales.Add(finalSales);
        game.totalSales += finalSales;

        return finalSales;
    }


}
