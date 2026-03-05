using UnityEngine;

public class SimpleViewSwitcher : MonoBehaviour
{
    public static SimpleViewSwitcher Instance;

    [Header("References")]
    public RectTransform contentField; // Drag your 'contentField' here
    public GameObject homeScreenPrefab; // Drag your Dashboard prefab here

    private GameObject currentView;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Load the Home Screen by default so the box isn't black
        SwitchView(homeScreenPrefab);
    }

    public void SwitchView(GameObject prefab)
    {
        if (prefab == null) return;

        // Clean up current view
        if (currentView != null)
            Destroy(currentView);

        // Spawn new view
        currentView = Instantiate(prefab, contentField);

        // Ensure it stretches to fill the black box
        RectTransform rt = currentView.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}