using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Pure TextMeshPro story card controller.
/// Receives an ExhibitStory ScriptableObject and drives TMP text fields directly.
/// UIDocument is intentionally left disabled — TMP is the sole rendering path.
/// </summary>
public class VRExhibitPanelUI : MonoBehaviour
{
    [Header("TMP Text References — assign in Inspector")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text scientificNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text pageIndicatorText;

    [Header("Button GameObjects — assign in Inspector")]
    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject nextButton;
    [SerializeField] private GameObject closeButton;

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    // Runtime state
    private ExhibitStory currentStory;
    private int currentPage;

    public event System.Action OnPanelClosed;

    // ── Backwards-compat struct so VRExhibitTrigger old field still compiles ──
    [System.Serializable]
    public struct AnimalPageData { [TextArea(3, 8)] public string content; }
    [System.Serializable]
    public struct AnimalExhibitData
    {
        public string animalName;
        public string scientificName;
        public string status;
        public AnimalPageData[] pages;
    }

    private void Awake()
    {
        // Disable legacy PanelRenderer and UIDocument so UI Toolkit cards never render
        var pr = GetComponent("PanelRenderer") as MonoBehaviour;
        if (pr != null) pr.enabled = false;

        var uiDoc = GetComponent<UnityEngine.UIElements.UIDocument>();
        if (uiDoc != null) uiDoc.enabled = false;

        // If panelRoot not explicitly assigned, use this GameObject
        if (panelRoot == null) panelRoot = gameObject;

        // Always hide the card at scene start
        panelRoot.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API called by VRExhibitTrigger
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Primary entry point: show story from a ScriptableObject.
    /// </summary>
    public void ShowStory(ExhibitStory story)
    {
        if (story == null)
        {
            Debug.LogError("[VRExhibitPanelUI] ShowStory called with null ExhibitStory!", this);
            return;
        }

        currentStory = story;
        currentPage  = 0;

        panelRoot.SetActive(true);

        // Ensure all child canvases and CanvasGroups are active and visible
        var childCanvases = panelRoot.GetComponentsInChildren<Canvas>(true);
        foreach (var c in childCanvases)
        {
            c.gameObject.SetActive(true);
            c.enabled = true;
        }

        var canvasGroups = panelRoot.GetComponentsInChildren<CanvasGroup>(true);
        foreach (var cg in canvasGroups)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        LogStoryState();
        RefreshPage();
    }

    /// <summary>
    /// Legacy shim: keeps existing scene wiring working while transition is underway.
    /// </summary>
    public void ShowExhibit(AnimalExhibitData data)
    {
        // Build a temporary runtime ExhibitStory from the inline data
        var temp = ScriptableObject.CreateInstance<ExhibitStory>();
        temp.animalName      = data.animalName;
        temp.scientificName  = data.scientificName;
        temp.status          = string.IsNullOrEmpty(data.status) ? "EXTINCT" : data.status;
        temp.pages           = new System.Collections.Generic.List<ExhibitStoryPage>();
        if (data.pages != null)
        {
            foreach (var p in data.pages)
                temp.pages.Add(new ExhibitStoryPage { body = p.content });
        }
        ShowStory(temp);
    }

    private bool isHiding = false;

    public void HidePanel()
    {
        if (isHiding) return;
        isHiding = true;

        try
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            currentStory = null;
            currentPage  = 0;
            OnPanelClosed?.Invoke();
        }
        finally
        {
            isHiding = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Legacy / Backward-Compatibility Shims
    // ─────────────────────────────────────────────────────────────────────────

    public void SetData(AnimalExhibitData data) => ShowExhibit(data);
    public void UpdateUI() { if (currentStory != null) RefreshPage(); }
    public void ActivatePanel() { if (currentStory != null) ShowStory(currentStory); }
    public void ResetPanel() => HidePanel();
    public void TogglePanel() { if (panelRoot != null && panelRoot.activeSelf) HidePanel(); else if (currentStory != null) ShowStory(currentStory); }

    public void OnNextClicked() => NextPage();
    public void OnBackClicked() => PreviousPage();
    public void CloseCard()     => ClosePanel();

    public void NextPage()
    {
        if (currentStory == null) return;
        if (currentPage < currentStory.pages.Count - 1)
        {
            currentPage++;
            RefreshPage();
        }
    }

    public void PreviousPage()
    {
        if (currentStory == null) return;
        if (currentPage > 0)
        {
            currentPage--;
            RefreshPage();
        }
    }

    public void ClosePanel() => HidePanel();

    // ─────────────────────────────────────────────────────────────────────────
    // Internal
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshPage()
    {
        if (currentStory == null || currentStory.pages == null || currentStory.pages.Count == 0)
        {
            Debug.LogError("[VRExhibitPanelUI] RefreshPage — currentStory has no pages!", this);
            return;
        }

        ExhibitStoryPage page = currentStory.pages[currentPage];

        // Set TMP text directly — the only rendering path
        SetText(titleText,          currentStory.animalName);
        SetText(scientificNameText, currentStory.scientificName);
        SetText(statusText,         string.IsNullOrEmpty(currentStory.status) ? "EXTINCT" : currentStory.status);
        SetText(bodyText,           page.body);
        SetText(pageIndicatorText,  $"{currentPage + 1} / {currentStory.pages.Count}");

        // Show / hide navigation buttons
        if (backButton  != null) backButton.SetActive(currentPage > 0);
        if (nextButton  != null) nextButton.SetActive(currentPage < currentStory.pages.Count - 1);
        if (closeButton != null) closeButton.SetActive(currentPage == currentStory.pages.Count - 1);

        Debug.Log($"[VRExhibitPanelUI] RefreshPage → '{currentStory.animalName}' page {currentPage + 1}/{currentStory.pages.Count}");
    }

    private static void SetText(TMP_Text field, string value)
    {
        if (field == null) return;
        field.text = value;

        // Force alpha 1 in case CanvasGroup or parent faded it
        Color c = field.color;
        c.a = 1f;
        field.color = c;

        field.gameObject.SetActive(true);
    }

    private void LogStoryState()
    {
        if (currentStory == null)
        {
            Debug.LogError("[VRExhibitPanelUI] LogStoryState — currentStory is NULL!", this);
            return;
        }
        Debug.Log($"[VRExhibitPanelUI] Story: {currentStory.animalName} | Page: {currentPage} | Pages: {currentStory.pages?.Count}");
    }
}
