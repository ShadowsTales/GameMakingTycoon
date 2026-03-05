using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TopBarController : MonoBehaviour
{
    private Label dateLabel;
    private Label moneyLabel;
    private Label flowLabel; // Das neue Label für das Dreieck

    private Button btnPause, btnFast, btnNormal;
    private List<Button> timeButtons = new List<Button>();

    private long currentMoney = 50000;
    private long weeklyIncome = 1500; 

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        dateLabel = root.Q<Label>("DateValue");
        moneyLabel = root.Q<Label>("MoneyValue");
        flowLabel = root.Q<Label>("FlowValue"); // Erstelle ein Label namens "FlowValue" in deiner UXML
        
        //Time Buttons
        btnPause = root.Q<Button>("BtnPause");
        btnNormal = root.Q<Button>("BtnNormal");
        btnFast = root.Q<Button>("BtnFast");

        timeButtons.AddRange(new[] { btnPause, btnNormal, btnFast});

        //Events Binden
        btnPause.clicked += () => UpdateTimeScale(0, btnPause);
        btnNormal.clicked += () => UpdateTimeScale(1, btnNormal);
        btnFast.clicked += () => UpdateTimeScale(2, btnFast);

        UpdateUI(GameTimeManager.Instance.currentWeek);
        GameTimeManager.Instance.OnWeekPassed += HandleWeekPassed;
    }

    void OnDisable()
    {
        if(GameTimeManager.Instance != null)
            GameTimeManager.Instance.OnWeekPassed -= HandleWeekPassed;
    }


    private void UpdateTimeScale(int speed, Button activeBtn)
    {
        if(speed <= 0)
        {
            GameTimeManager.Instance.enabled = false;
        }
        else
        {
            GameTimeManager.Instance.enabled = true;
            GameTimeManager.Instance.weekDuration = GameTimeManager.Instance.weekDuration / speed;
        }
    }
    private void HandleWeekPassed(int newWeek)
    {
        // Hier passiert die Erhöhung!
        currentMoney += weeklyIncome; 
        UpdateUI(newWeek);
    }

    private void UpdateUI(int week)
    {
        int year = (week - 1) / 52 + 1;
        int displayWeek = (week - 1) % 52 + 1;

        if(dateLabel != null) dateLabel.text = $"J{year}, W{displayWeek}";

        if(moneyLabel != null) moneyLabel.text = currentMoney.ToString("N0") + " €";

        // --- GELD-FLOW ANZEIGE ---
        if(flowLabel != null)
        {
            if(weeklyIncome >= 0)
            {
                flowLabel.text = "▲"; // Alt + 30
                flowLabel.style.color = new Color(0.29f, 0.87f, 0.50f); // Ein schönes Grün
            }
            else
            {
                flowLabel.text = "▼"; // Alt + 31
                flowLabel.style.color = Color.red;
            }
        }
    }
}