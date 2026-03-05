using UnityEngine;
using System;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance;

    public int currentWeek = 1;
    public float weekDuration = 1.0f; // 1 Sekunde = 1 Woche (zum Testen)
    private float timer;

    public event Action<int> OnWeekPassed;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= weekDuration)
        {
            timer = 0f;
            currentWeek++;
            GameManager.Instance.OnWeekPassed();
            OnWeekPassed?.Invoke(currentWeek);
        }
    }
}
