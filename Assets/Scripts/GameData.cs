using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime data for a single created game.
/// This is NOT a ScriptableObject on purpose:
/// - It represents dynamic, changing data (sales, hype, reviews, etc.)
/// - It is created at runtime when the player makes a game
/// </summary>
[System.Serializable]
public class GameData
{
    [Header("Basic Info")]
    public string gameName;

    // Primary and secondary genre (e.g. "Action-RPG")
    public GenreSO genre;
    public GenreSO secondGenre;

    // Primary and secondary topic (e.g. "Fantasy + Magic")
    public TopicSO topic;
    public TopicSO secondTopic;

    // Target audience (Kids, Teens, Adults)
    public TargetGroupSO targetGroup;

    [Header("Development Focus (0–1)")]
    // Sliders from UI, usually 0–1
    public float gameplayFocus;
    public float storyFocus;
    public float graphicsFocus;

    [Header("Marketing & Hype")]
    // 0–100, affects sales multiplier
    public int hype;

    [Header("Sales Data")]
    // Total units sold across all weeks
    public int totalSales = 0;

    // Week number when the game was released
    public int releaseWeek = 0;

    // History of weekly sales (index 0 = release week, etc.)
    public List<int> weeklySales = new List<int>();

    [Header("Review Data")]
    // Average review score (0–10)
    public float reviewScore;

    [Header("Financials")]
    // Price per unit (e.g. 40 = 40 currency units)
    public int price = 40;

    // Total revenue = sum of (weeklySales * price)
    public int totalRevenue = 0;

    [Header("Development State")]
    public float currentProgress = 0f;
    public bool isFinished = false;

    [Header("Baukasten")]
    public List<FeatureSO> selectedFeatures = new List<FeatureSO>(); // Speichert die gewählten Features
}
