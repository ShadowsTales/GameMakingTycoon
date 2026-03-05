using UnityEngine;
using TMPro;
using System.Linq;

public class DashboardView : MonoBehaviour
{
    [Header("Stats Display")]
    public TextMeshProUGUI latestGameText;
    public TextMeshProUGUI totalCompanySalesText;
    public TextMeshProUGUI activeGamesCountText;
/*
    private void OnEnable()
    {
        UpdateDashboard();
        // Refresh stats every time the week ticks over
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnWeekPassed += HandleWeekPassed;
    }

    private void OnDisable()
    {
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnWeekPassed -= HandleWeekPassed;
    }

    private void HandleWeekPassed(int week) => UpdateDashboard();

    public void UpdateDashboard()
    {
        //var allGames = GameManager.Instance.GetAllGames();
        
        // 1. Show Latest Game
        //var latest = GameManager.Instance.GetLatestGame();
        latestGameText.text = latest != null ? $"Latest: {latest.gameName}" : "No games released";

        // 2. Calculate Total Company Sales
        int totalSales = allGames.Sum(g => g.totalSales);
        totalCompanySalesText.text = $"Total Units Sold: {totalSales:N0}";

        // 3. Count Active Games
        activeGamesCountText.text = $"Portfolio Size: {allGames.Count}";
    }
    */
}