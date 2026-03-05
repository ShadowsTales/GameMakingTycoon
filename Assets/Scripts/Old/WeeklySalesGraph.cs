using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeeklySalesGraph : MonoBehaviour
{
    public RectTransform barContainer;
    public GameObject barPrefab;

    public float animationSpeed = 300f;

    public void ShowWeeklySales(List<int> weeklySales)
    {
        foreach (Transform child in barContainer)
            Destroy(child.gameObject);

        if (weeklySales.Count == 0)
            return;

        float maxValue = Mathf.Max(weeklySales.ToArray());
        float graphHeight = barContainer.sizeDelta.y;

        foreach (int sales in weeklySales)
        {
            float targetHeight = (sales / maxValue) * graphHeight;

            GameObject bar = Instantiate(barPrefab, barContainer);
            RectTransform rt = bar.GetComponent<RectTransform>();

            StartCoroutine(AnimateBar(rt, targetHeight));
        }
    }

    private System.Collections.IEnumerator AnimateBar(RectTransform bar, float targetHeight)
    {
        float current = 0f;

        while (current < targetHeight)
        {
            current += Time.deltaTime * animationSpeed;
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, Mathf.Min(current, targetHeight));
            yield return null;
        }
    }
}
