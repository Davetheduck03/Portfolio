using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;

/// <summary>
/// Editor utility that builds a Death Screen Canvas in the active scene.
/// Run via Tools → UI → Create Death Screen.
///
/// Creates:
///   DeathScreen (Canvas, inactive by default)
///     └── Overlay        (full-screen dark panel)
///           └── Panel    (centred card)
///                 ├── Title      ("YOU DIED")
///                 ├── Restart    button → LevelManager.RestartLevel
///                 ├── MainMenu   button → LevelManager.QuitToMenu
///                 └── ChooseLevel button → LevelManager.GoToLevelSelect
///
/// If a LevelManager exists in the scene the buttons are wired automatically.
/// </summary>
public static class DeathScreenSetup
{
    // ----------------------------------------------------------------
    //  Palette
    // ----------------------------------------------------------------

    private static readonly Color OverlayColour  = new Color(0f,    0f,    0f,    0.75f);
    private static readonly Color PanelColour    = new Color(0.10f, 0.10f, 0.13f, 1f);
    private static readonly Color TitleColour    = new Color(0.90f, 0.20f, 0.20f, 1f);
    private static readonly Color ButtonNormal   = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color ButtonHighlight= new Color(0.28f, 0.28f, 0.35f, 1f);
    private static readonly Color ButtonPressed  = new Color(0.10f, 0.10f, 0.13f, 1f);
    private static readonly Color ButtonText     = new Color(0.92f, 0.92f, 0.92f, 1f);

    // ----------------------------------------------------------------
    //  Entry point
    // ----------------------------------------------------------------

    [MenuItem("Tools/UI/Create Death Screen")]
    public static void CreateDeathScreen()
    {
        // ── Canvas ──────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("DeathScreen");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Death Screen");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // render on top of everything

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Full-screen overlay ──────────────────────────────────────
        GameObject overlayGO = CreateUIObject("Overlay", canvasGO.transform);
        Image overlay = overlayGO.AddComponent<Image>();
        overlay.color = OverlayColour;
        StretchFull(overlayGO.GetComponent<RectTransform>());

        // ── Centred card ─────────────────────────────────────────────
        GameObject panelGO = CreateUIObject("Panel", overlayGO.transform);
        Image panel = panelGO.AddComponent<Image>();
        panel.color = PanelColour;

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin  = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax  = new Vector2(0.5f, 0.5f);
        panelRT.pivot      = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta  = new Vector2(520f, 420f);
        panelRT.anchoredPosition = Vector2.zero;

        // Vertical layout on the panel
        VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment           = TextAnchor.MiddleCenter;
        layout.spacing                  = 24f;
        layout.padding                  = new RectOffset(48, 48, 48, 48);
        layout.childControlWidth        = true;
        layout.childControlHeight       = false;
        layout.childForceExpandWidth    = true;
        layout.childForceExpandHeight   = false;

        // ── Title ────────────────────────────────────────────────────
        GameObject titleGO = CreateUIObject("Title", panelGO.transform);
        Text title = titleGO.AddComponent<Text>();
        title.text      = "YOU DIED";
        title.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize  = 64;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color     = TitleColour;

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.sizeDelta = new Vector2(0f, 90f);

        // ── Buttons ──────────────────────────────────────────────────
        LevelManager lm = Object.FindFirstObjectByType<LevelManager>();

        CreateButton(panelGO.transform, "Restart",     "RESTART",      lm,
                     lm != null ? nameof(LevelManager.RestartLevel)   : null);

        CreateButton(panelGO.transform, "MainMenu",    "MAIN MENU",    lm,
                     lm != null ? nameof(LevelManager.QuitToMenu)     : null);

        CreateButton(panelGO.transform, "ChooseLevel", "CHOOSE LEVEL", lm,
                     lm != null ? nameof(LevelManager.GoToLevelSelect) : null);

        // ── Finish ───────────────────────────────────────────────────
        // Start inactive so the death screen is hidden until triggered
        canvasGO.SetActive(false);

        // Assign to LevelManager if one exists
        if (lm != null)
        {
            SerializedObject so = new SerializedObject(lm);
            SerializedProperty prop = so.FindProperty("deathScreenCanvas");
            if (prop != null)
            {
                prop.objectReferenceValue = canvasGO;
                so.ApplyModifiedProperties();
                Debug.Log("[DeathScreenSetup] Assigned DeathScreen to LevelManager.deathScreenCanvas.");
            }
        }

        Selection.activeGameObject = canvasGO;
        EditorGUIUtility.PingObject(canvasGO);

        Debug.Log("[DeathScreenSetup] Death Screen created." +
                  (lm == null ? " No LevelManager found — wire buttons manually." : ""));
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
    }

    private static void CreateButton(Transform parent, string goName,
                                     string label, LevelManager lm,
                                     string methodName)
    {
        // Container
        GameObject go = CreateUIObject(goName, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 72f);

        // Background image
        Image bg = go.AddComponent<Image>();
        bg.color = ButtonNormal;

        // Button component + colour transitions
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = ButtonNormal;
        cb.highlightedColor = ButtonHighlight;
        cb.pressedColor     = ButtonPressed;
        cb.selectedColor    = ButtonHighlight;
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;
        btn.targetGraphic   = bg;

        // Label
        GameObject textGO = CreateUIObject("Label", go.transform);
        Text text = textGO.AddComponent<Text>();
        text.text      = label;
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = ButtonText;
        StretchFull(textGO.GetComponent<RectTransform>());

        // Wire onClick if LevelManager and method name provided
        if (lm != null && !string.IsNullOrEmpty(methodName))
        {
            UnityAction call = System.Delegate.CreateDelegate(
                typeof(UnityAction), lm, methodName) as UnityAction;

            if (call != null)
                UnityEventTools.AddPersistentListener(btn.onClick, call);
        }
    }
}
