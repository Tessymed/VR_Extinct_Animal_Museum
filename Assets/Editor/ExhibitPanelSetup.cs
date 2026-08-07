#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// One-click setup: builds all TMP labels, navigation buttons, and a Canvas
/// on every selected VRExhibitPanelUI GameObject, then auto-wires every
/// serialized field so you never have to drag anything manually.
///
/// Usage:  Select the panel GameObjects  →  Tools > Setup Exhibit Panel(s)
/// </summary>
public static class ExhibitPanelSetup
{
    // ── Layout constants (matching original panel 650x520 sizeDelta) ──────────────
    const float CANVAS_W      = 650f;
    const float CANVAS_H      = 520f;

    const float TITLE_FONT    = 34f;
    const float SCI_FONT      = 22f;
    const float STATUS_FONT   = 18f;
    const float BODY_FONT     = 22f;
    const float PAGE_FONT     = 18f;
    const float BTN_FONT      = 22f;

    static readonly Color BG_COLOUR     = new Color(0.04f, 0.05f, 0.08f, 0.98f);  // dark museum navy card
    static readonly Color TEXT_COLOUR   = Color.white;
    static readonly Color BTN_NORMAL    = new Color(0.18f, 0.18f, 0.30f, 1f);
    static readonly Color BTN_HOVER     = new Color(0.28f, 0.28f, 0.50f, 1f);
    static readonly Color CLOSE_COLOUR  = new Color(0.65f, 0.12f, 0.12f, 1f);

    // ──────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Setup Exhibit Panel(s)")]
    public static void SetupSelected()
    {
        int count = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            var panel = go.GetComponent<VRExhibitPanelUI>();
            if (panel == null)
            {
                Debug.LogWarning($"[ExhibitPanelSetup] '{go.name}' has no VRExhibitPanelUI — skipped.");
                continue;
            }
            BuildPanel(panel);
            count++;
        }

        if (count == 0)
            Debug.LogWarning("[ExhibitPanelSetup] No GameObjects with VRExhibitPanelUI were selected.");
        else
            Debug.Log($"[ExhibitPanelSetup] ✅ Set up {count} panel(s). Save your scene!");
    }

    // ──────────────────────────────────────────────────────────────────────
    public static void BuildPanel(VRExhibitPanelUI panel)
    {
        GameObject root = panel.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Exhibit Panel");

        // ── 0. Disable legacy PanelRenderer & UIDocument ─────────────────
        var panelRenderer = root.GetComponent("PanelRenderer") as MonoBehaviour;
        if (panelRenderer != null) panelRenderer.enabled = false;

        var uiDoc = root.GetComponent<UnityEngine.UIElements.UIDocument>();
        if (uiDoc != null) uiDoc.enabled = false;

        // ── 1. Remove any stale canvas we previously created ─────────────
        var old = root.transform.Find("ExhibitCanvas");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ── 2. Background Image GameObject ───────────────────────────────
        var canvasGO = new GameObject("ExhibitCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create ExhibitCanvas");
        canvasGO.transform.SetParent(root.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var cr = canvasGO.AddComponent<CanvasRenderer>();
        var cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.anchorMin        = Vector2.zero;
        canvasRT.anchorMax        = Vector2.one;
        canvasRT.offsetMin        = Vector2.zero;
        canvasRT.offsetMax        = Vector2.zero;
        canvasRT.localScale       = Vector3.one * 0.0065f;  // Perfect 1.4m x 1.0m world scale relative to 0.307 parent
        canvasRT.localPosition    = Vector3.zero;
        canvasRT.localRotation    = Quaternion.identity;

        // Background image
        var img = canvasGO.AddComponent<Image>();
        img.color = BG_COLOUR;

        // ── 3. Create layout areas (normalised anchor coords) ─────────────
        // Title row  (top)
        var titleGO      = MakeTMPLabel(canvasGO, "TitleText",
                                        new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.96f),
                                        TITLE_FONT, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        // Scientific name row
        var sciGO        = MakeTMPLabel(canvasGO, "ScientificNameText",
                                        new Vector2(0.06f, 0.72f), new Vector2(0.70f, 0.82f),
                                        SCI_FONT, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);

        // Status badge (top-right)
        var statusGO     = MakeTMPLabel(canvasGO, "StatusText",
                                        new Vector2(0.70f, 0.72f), new Vector2(0.94f, 0.82f),
                                        STATUS_FONT, TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        // Divider line
        MakeDivider(canvasGO, new Vector2(0.06f, 0.705f), new Vector2(0.94f, 0.715f));

        // Body text (main story area)
        var bodyGO       = MakeTMPLabel(canvasGO, "BodyText",
                                        new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.69f),
                                        BODY_FONT, TextAlignmentOptions.TopLeft, FontStyles.Normal);

        // Page indicator (bottom-centre)
        var pageGO       = MakeTMPLabel(canvasGO, "PageIndicatorText",
                                        new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.16f),
                                        PAGE_FONT, TextAlignmentOptions.Center, FontStyles.Normal);

        // ── 4. Navigation Buttons ─────────────────────────────────────────
        // Back (bottom-left)
        var backGO  = MakeButton(canvasGO, "BackButton",
                                 new Vector2(0.06f, 0.05f), new Vector2(0.28f, 0.17f),
                                 "< Back", BTN_NORMAL, panel, "OnBackClicked");

        // Next (bottom-right)
        var nextGO  = MakeButton(canvasGO, "NextButton",
                                 new Vector2(0.72f, 0.05f), new Vector2(0.94f, 0.17f),
                                 "Next >", BTN_NORMAL, panel, "OnNextClicked");

        // Close (bottom-right, shown on last page only at runtime)
        var closeGO = MakeButton(canvasGO, "CloseButton",
                                 new Vector2(0.72f, 0.05f), new Vector2(0.94f, 0.17f),
                                 "X Close", CLOSE_COLOUR, panel, "ClosePanel");

        // ── 5. Wire serialized fields via SerializedObject ────────────────
        var so = new SerializedObject(panel);

        SetRef(so, "titleText",          titleGO .GetComponent<TMP_Text>());
        SetRef(so, "scientificNameText", sciGO   .GetComponent<TMP_Text>());
        SetRef(so, "statusText",         statusGO.GetComponent<TMP_Text>());
        SetRef(so, "bodyText",           bodyGO  .GetComponent<TMP_Text>());
        SetRef(so, "pageIndicatorText",  pageGO  .GetComponent<TMP_Text>());
        SetRef(so, "backButton",         backGO);
        SetRef(so, "nextButton",         nextGO);
        SetRef(so, "closeButton",        closeGO);
        SetRef(so, "panelRoot",          root);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(panel);
        Debug.Log($"[ExhibitPanelSetup] ✅ '{root.name}' — full-size TMP canvas built & all fields wired.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    static GameObject MakeTMPLabel(GameObject parent, string name,
                                   Vector2 anchorMin, Vector2 anchorMax,
                                   float fontSize, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = name;           // placeholder; replaced at runtime
        tmp.fontSize           = fontSize;
        tmp.alignment          = align;
        tmp.fontStyle          = style;
        tmp.color              = TEXT_COLOUR;
        tmp.enableWordWrapping = true;
        tmp.overflowMode       = TextOverflowModes.Ellipsis;

        return go;
    }

    static void MakeDivider(GameObject parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Divider");
        Undo.RegisterCreatedObjectUndo(go, "Create Divider");
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.25f);
    }

    static GameObject MakeButton(GameObject parent, string name,
                                 Vector2 anchorMin, Vector2 anchorMax,
                                 string label, Color bgColour,
                                 VRExhibitPanelUI panel, string methodName)
    {
        // Container
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = bgColour;

        var btn = go.AddComponent<Button>();

        // Hover colour state
        var colours              = btn.colors;
        colours.normalColor      = bgColour;
        colours.highlightedColor = BTN_HOVER;
        colours.pressedColor     = new Color(bgColour.r * 1.3f, bgColour.g * 1.3f, bgColour.b * 1.3f, 1f);
        colours.selectedColor    = bgColour;
        btn.colors               = colours;

        // Wire click → panel method via persistent listener
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            btn.onClick,
            System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction),
                panel,
                typeof(VRExhibitPanelUI).GetMethod(methodName)) as UnityEngine.Events.UnityAction
        );

        // Label text
        var labelGO = new GameObject("Label");
        Undo.RegisterCreatedObjectUndo(labelGO, $"Create {name} label");
        labelGO.transform.SetParent(go.transform, false);

        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

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
