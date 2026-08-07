#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Robust, pixel-perfect World Space Exhibit Panel Builder.
/// Generates full-size 1.4m x 1.0m TextMeshPro story cards with explicit
/// RectTransform sizes (immune to parent anchor collapse) and 3D BoxColliders for VR buttons.
/// </summary>
public static class ExhibitPanelSetup
{
    // ── Physical Card Dimensions ──────────────────────────────────────────
    const float CARD_W        = 700f;
    const float CARD_H        = 520f;
    const float WORLD_SCALE   = 0.0065f; // Scales 700x520 to ~1.4m x 1.0m relative to 0.307 parent

    const float TITLE_FONT    = 34f;
    const float SCI_FONT      = 22f;
    const float STATUS_FONT   = 20f;
    const float BODY_FONT     = 24f;
    const float PAGE_FONT     = 20f;
    const float BTN_FONT      = 22f;

    static readonly Color BG_COLOUR     = new Color(0.04f, 0.05f, 0.08f, 0.98f);  // dark museum navy
    static readonly Color TEXT_COLOUR   = Color.white;
    static readonly Color STATUS_COLOUR = new Color(0.95f, 0.25f, 0.25f, 1f);     // vibrant extinction red
    static readonly Color BTN_NORMAL    = new Color(0.18f, 0.20f, 0.32f, 1f);
    static readonly Color BTN_HOVER     = new Color(0.28f, 0.32f, 0.55f, 1f);
    static readonly Color CLOSE_COLOUR  = new Color(0.70f, 0.15f, 0.15f, 1f);

    // ──────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Setup All Exhibit Panels in Scene")]
    public static void SetupAllPanels()
    {
        var panels = Object.FindObjectsByType<VRExhibitPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var panel in panels)
        {
            if (panel != null && panel.gameObject.scene.isLoaded)
            {
                BuildPanel(panel);
                count++;
            }
        }

        if (count == 0)
            Debug.LogWarning("[ExhibitPanelSetup] No GameObjects with VRExhibitPanelUI found in scene.");
        else
            Debug.Log($"[ExhibitPanelSetup] ✅ Successfully built and wired {count} exhibit panel(s)!");
    }

    [MenuItem("Tools/Setup Exhibit Panel(s)")]
    public static void SetupSelected()
    {
        int count = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            var panel = go.GetComponent<VRExhibitPanelUI>();
            if (panel == null) continue;
            BuildPanel(panel);
            count++;
        }

        if (count == 0)
            Debug.LogWarning("[ExhibitPanelSetup] No GameObjects with VRExhibitPanelUI selected.");
        else
            Debug.Log($"[ExhibitPanelSetup] ✅ Set up {count} selected panel(s).");
    }

    // ──────────────────────────────────────────────────────────────────────
    public static void BuildPanel(VRExhibitPanelUI panel)
    {
        GameObject root = panel.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Exhibit Panel");

        // ── 0. Disable legacy UI Toolkit components ──────────────────────
        var panelRenderer = root.GetComponent("PanelRenderer") as MonoBehaviour;
        if (panelRenderer != null) panelRenderer.enabled = false;

        var uiDoc = root.GetComponent<UnityEngine.UIElements.UIDocument>();
        if (uiDoc != null) uiDoc.enabled = false;

        var panelRaycaster = root.GetComponent("PanelRaycaster") as MonoBehaviour;
        if (panelRaycaster != null) panelRaycaster.enabled = false;

        // ── 1. Clean up existing ExhibitCanvas ────────────────────────────
        var old = root.transform.Find("ExhibitCanvas");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ── 2. Create Root ExhibitCanvas ─────────────────────────────────
        var canvasGO = new GameObject("ExhibitCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create ExhibitCanvas");
        canvasGO.transform.SetParent(root.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var raycaster = canvasGO.AddComponent<GraphicRaycaster>();
        var cr        = canvasGO.AddComponent<CanvasRenderer>();
        var cg        = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.anchorMin          = new Vector2(0.5f, 0.5f);
        canvasRT.anchorMax          = new Vector2(0.5f, 0.5f);
        canvasRT.pivot              = new Vector2(0.5f, 0.5f);
        canvasRT.sizeDelta          = new Vector2(CARD_W, CARD_H);
        canvasRT.anchoredPosition   = Vector2.zero;
        canvasRT.localScale         = Vector3.one * WORLD_SCALE;
        canvasRT.localRotation      = Quaternion.identity;

        // Background panel image
        var bgImg = canvasGO.AddComponent<Image>();
        bgImg.color = BG_COLOUR;

        // Background BoxCollider for raycast reception
        var bgCol = canvasGO.AddComponent<BoxCollider>();
        bgCol.size = new Vector3(CARD_W, CARD_H, 2f);
        bgCol.isTrigger = false;

        // ── 3. Explicit Pixel Layout (No stretching, no collapsing) ───────
        // Title (top)
        var titleGO = MakeLabel(canvasGO, "TitleText",
                                new Vector2(0f, 195f), new Vector2(620f, 60f),
                                TITLE_FONT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, TEXT_COLOUR);

        // Scientific Name (sub-title left)
        var sciGO = MakeLabel(canvasGO, "ScientificNameText",
                              new Vector2(-110f, 145f), new Vector2(400f, 40f),
                              SCI_FONT, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, new Color(0.8f, 0.85f, 0.95f, 1f));

        // Status Badge (top right)
        var statusGO = MakeLabel(canvasGO, "StatusText",
                                 new Vector2(210f, 145f), new Vector2(200f, 40f),
                                 STATUS_FONT, TextAlignmentOptions.MidlineRight, FontStyles.Bold, STATUS_COLOUR);

        // Divider Line
        MakeDivider(canvasGO, new Vector2(0f, 120f), new Vector2(620f, 3f));

        // Story Body Text (center main area)
        var bodyGO = MakeLabel(canvasGO, "BodyText",
                               new Vector2(0f, -10f), new Vector2(620f, 220f),
                               BODY_FONT, TextAlignmentOptions.TopLeft, FontStyles.Normal, TEXT_COLOUR);
        var bodyTMP = bodyGO.GetComponent<TextMeshProUGUI>();
        bodyTMP.lineSpacing = 18f;

        // Page Indicator (bottom center)
        var pageGO = MakeLabel(canvasGO, "PageIndicatorText",
                               new Vector2(0f, -185f), new Vector2(200f, 45f),
                               PAGE_FONT, TextAlignmentOptions.Center, FontStyles.Normal, new Color(0.7f, 0.7f, 0.8f, 1f));

        // ── 4. Navigation Buttons (with dedicated 3D BoxColliders) ────────
        // Back Button (bottom left)
        var backGO = MakeButton(canvasGO, "BackButton",
                                new Vector2(-225f, -185f), new Vector2(170f, 55f),
                                "< Back", BTN_NORMAL, panel, "OnBackClicked");

        // Next Button (bottom right)
        var nextGO = MakeButton(canvasGO, "NextButton",
                                new Vector2(225f, -185f), new Vector2(170f, 55f),
                                "Next >", BTN_NORMAL, panel, "OnNextClicked");

        // Close Button (bottom right, active on final page)
        var closeGO = MakeButton(canvasGO, "CloseButton",
                                 new Vector2(225f, -185f), new Vector2(170f, 55f),
                                 "X Close", CLOSE_COLOUR, panel, "ClosePanel");

        // ── 5. Auto-wire serialized fields via SerializedObject ───────────
        var so = new SerializedObject(panel);
        SetRef(so, "titleText",          titleGO.GetComponent<TMP_Text>());
        SetRef(so, "scientificNameText", sciGO.GetComponent<TMP_Text>());
        SetRef(so, "statusText",         statusGO.GetComponent<TMP_Text>());
        SetRef(so, "bodyText",           bodyGO.GetComponent<TMP_Text>());
        SetRef(so, "pageIndicatorText",  pageGO.GetComponent<TMP_Text>());
        SetRef(so, "backButton",         backGO);
        SetRef(so, "nextButton",         nextGO);
        SetRef(so, "closeButton",        closeGO);
        SetRef(so, "panelRoot",          root);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(panel);
        Debug.Log($"[ExhibitPanelSetup] ✅ '{root.name}' — Card built with explicit dimensions & wired.");
    }

    // ── Layout Helpers ─────────────────────────────────────────────────────

    static GameObject MakeLabel(GameObject parent, string name,
                                Vector2 anchoredPos, Vector2 sizeDelta,
                                float fontSize, TextAlignmentOptions align,
                                FontStyles style, Color colour)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        rt.localScale       = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text            = name;
        tmp.fontSize        = fontSize;
        tmp.alignment       = align;
        tmp.fontStyle       = style;
        tmp.color           = colour;
        tmp.enableWordWrapping = true;
        tmp.overflowMode    = TextOverflowModes.Ellipsis;

        return go;
    }

    static void MakeDivider(GameObject parent, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject("Divider");
        Undo.RegisterCreatedObjectUndo(go, "Create Divider");
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        rt.localScale       = Vector3.one;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.25f);
    }

    static GameObject MakeButton(GameObject parent, string name,
                                 Vector2 anchoredPos, Vector2 sizeDelta,
                                 string label, Color bgColour,
                                 VRExhibitPanelUI panel, string methodName)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        rt.localScale       = Vector3.one;
        rt.localPosition    = new Vector3(anchoredPos.x, anchoredPos.y, -2f); // projected slightly forward

        var img = go.AddComponent<Image>();
        img.color = bgColour;

        // Dedicated 3D BoxCollider for VR raycasting
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(sizeDelta.x, sizeDelta.y, 10f);
        col.isTrigger = false;

        var btn = go.AddComponent<Button>();
        var colours              = btn.colors;
        colours.normalColor      = bgColour;
        colours.highlightedColor = BTN_HOVER;
        colours.pressedColor     = new Color(bgColour.r * 1.3f, bgColour.g * 1.3f, bgColour.b * 1.3f, 1f);
        colours.selectedColor    = bgColour;
        btn.colors               = colours;

        // Wire UnityAction
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            btn.onClick,
            System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction),
                panel,
                typeof(VRExhibitPanelUI).GetMethod(methodName)) as UnityEngine.Events.UnityAction
        );

        // Child label
        var labelGO = new GameObject("Label");
        Undo.RegisterCreatedObjectUndo(labelGO, $"Create {name} Label");
        labelGO.transform.SetParent(go.transform, false);

        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin        = Vector2.zero;
        lrt.anchorMax        = Vector2.one;
        lrt.offsetMin        = Vector2.zero;
        lrt.offsetMax        = Vector2.zero;
        lrt.localScale       = Vector3.one;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = BTN_FONT;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = TEXT_COLOUR;
        tmp.fontStyle = FontStyles.Bold;

        return go;
    }

    static void SetRef<T>(SerializedObject so, string fieldName, T obj) where T : Object
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null)
            prop.objectReferenceValue = obj;
        else
            Debug.LogWarning($"[ExhibitPanelSetup] Field '{fieldName}' not found on VRExhibitPanelUI.");
    }
}
#endif
