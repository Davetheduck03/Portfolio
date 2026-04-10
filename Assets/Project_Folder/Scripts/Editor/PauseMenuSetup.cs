using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;

/// <summary>
/// Editor utility that builds a Pause Menu Canvas in the active scene
/// and wires every button and slider to the PauseMenu component.
/// Run via Tools → UI → Create Pause Menu.
///
/// Creates:
///   PauseMenuRoot  (has PauseMenu behaviour)
///     └── PauseCanvas  (Canvas, inactive by default)
///           └── Overlay
///                 └── Panel
///                       ├── Title          "PAUSED"
///                       ├── AudioRow       "AUDIO"  [slider]
///                       ├── MusicRow       "MUSIC"  [slider]
///                       ├── RestartButton  → PauseMenu.OnRestartPressed
///                       └── MainMenuButton → PauseMenu.OnMainMenuPressed
/// </summary>
public static class PauseMenuSetup
{
    // ----------------------------------------------------------------
    //  Palette — slightly different from the death screen so the two
    //  menus read as distinct at a glance.
    // ----------------------------------------------------------------

    private static readonly Color OverlayColour   = new Color(0f,    0f,    0f,    0.65f);
    private static readonly Color PanelColour     = new Color(0.08f, 0.10f, 0.14f, 1f);
    private static readonly Color TitleColour     = new Color(0.85f, 0.85f, 1.00f, 1f);
    private static readonly Color LabelColour     = new Color(0.75f, 0.75f, 0.85f, 1f);
    private static readonly Color SliderBg        = new Color(0.20f, 0.20f, 0.28f, 1f);
    private static readonly Color SliderFill      = new Color(0.40f, 0.55f, 1.00f, 1f);
    private static readonly Color SliderHandle    = new Color(0.70f, 0.80f, 1.00f, 1f);
    private static readonly Color ButtonNormal    = new Color(0.18f, 0.20f, 0.28f, 1f);
    private static readonly Color ButtonHighlight = new Color(0.28f, 0.30f, 0.42f, 1f);
    private static readonly Color ButtonPressed   = new Color(0.10f, 0.12f, 0.18f, 1f);
    private static readonly Color ButtonText      = new Color(0.92f, 0.92f, 0.95f, 1f);

    // ----------------------------------------------------------------
    //  Entry point
    // ----------------------------------------------------------------

    [MenuItem("Tools/UI/Create Pause Menu")]
    public static void CreatePauseMenu()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ── Root GameObject (holds the PauseMenu behaviour) ──────────
        GameObject root = new GameObject("PauseMenuRoot");
        Undo.RegisterCreatedObjectUndo(root, "Create Pause Menu");
        PauseMenu pauseMenu = root.AddComponent<PauseMenu>();

        // ── Canvas ───────────────────────────────────────────────────
        GameObject canvasGO = CreateUIObject("PauseCanvas", root.transform);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;   // just below the death screen (100)

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.SetActive(false);

        // ── Overlay ──────────────────────────────────────────────────
        GameObject overlayGO = CreateUIObject("Overlay", canvasGO.transform);
        overlayGO.AddComponent<Image>().color = OverlayColour;
        StretchFull(overlayGO.GetComponent<RectTransform>());

        // ── Panel ────────────────────────────────────────────────────
        GameObject panelGO = CreateUIObject("Panel", overlayGO.transform);
        panelGO.AddComponent<Image>().color = PanelColour;

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta        = new Vector2(560f, 500f);
        panelRT.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment        = TextAnchor.MiddleCenter;
        layout.spacing               = 20f;
        layout.padding               = new RectOffset(48, 48, 48, 48);
        layout.childControlWidth     = true;
        layout.childControlHeight    = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ── Title ────────────────────────────────────────────────────
        GameObject titleGO = CreateUIObject("Title", panelGO.transform);
        Text title = titleGO.AddComponent<Text>();
        title.text      = "PAUSED";
        title.font      = font;
        title.fontSize  = 58;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color     = TitleColour;
        titleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 80f);

        // ── Audio slider row ─────────────────────────────────────────
        Slider audioSlider = CreateSliderRow(panelGO.transform, "AudioRow",
                                             "AUDIO", font, LabelColour,
                                             SliderBg, SliderFill, SliderHandle);

        // ── Music slider row ─────────────────────────────────────────
        Slider musicSlider = CreateSliderRow(panelGO.transform, "MusicRow",
                                             "MUSIC", font, LabelColour,
                                             SliderBg, SliderFill, SliderHandle);

        // ── Buttons ──────────────────────────────────────────────────
        Button restartBtn  = CreateButton(panelGO.transform, "RestartButton",
                                          "RESTART",   font, ButtonNormal,
                                          ButtonHighlight, ButtonPressed, ButtonText);

        Button menuBtn     = CreateButton(panelGO.transform, "MainMenuButton",
                                          "MAIN MENU", font, ButtonNormal,
                                          ButtonHighlight, ButtonPressed, ButtonText);

        // ── Wire everything to PauseMenu ─────────────────────────────
        SerializedObject so = new SerializedObject(pauseMenu);

        SetProp(so, "pauseCanvas",  canvasGO);
        SetProp(so, "audioSlider",  audioSlider);
        SetProp(so, "musicSlider",  musicSlider);
        so.ApplyModifiedProperties();

        // Slider OnValueChanged
        WireSlider(audioSlider, pauseMenu, nameof(PauseMenu.OnAudioSliderChanged));
        WireSlider(musicSlider, pauseMenu, nameof(PauseMenu.OnMusicSliderChanged));

        // Button OnClick
        WireButton(restartBtn, pauseMenu, nameof(PauseMenu.OnRestartPressed));
        WireButton(menuBtn,    pauseMenu, nameof(PauseMenu.OnMainMenuPressed));

        // ── Finish ───────────────────────────────────────────────────
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[PauseMenuSetup] Pause Menu created. " +
                  "Assign your AudioMixer to PauseMenuRoot → PauseMenu → Audio Mixer.");
    }

    // ----------------------------------------------------------------
    //  UI factory helpers
    // ----------------------------------------------------------------

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    private static Slider CreateSliderRow(Transform parent, string rowName,
                                          string labelText, Font font,
                                          Color labelCol,
                                          Color bgCol, Color fillCol, Color handleCol)
    {
        // Row container
        GameObject rowGO = CreateUIObject(rowName, parent);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0f, 56f);

        HorizontalLayoutGroup hLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment        = TextAnchor.MiddleCenter;
        hLayout.spacing               = 20f;
        hLayout.childControlHeight    = true;
        hLayout.childControlWidth     = false;
        hLayout.childForceExpandWidth = false;

        // Label
        GameObject labelGO = CreateUIObject("Label", rowGO.transform);
        labelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 0f);
        Text label = labelGO.AddComponent<Text>();
        label.text      = labelText;
        label.font      = font;
        label.fontSize  = 26;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color     = labelCol;

        // Slider
        GameObject sliderGO = CreateUIObject("Slider", rowGO.transform);
        sliderGO.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 0f);
        Slider slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;

        // Background
        GameObject bgGO = CreateUIObject("Background", sliderGO.transform);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = bgCol;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin  = new Vector2(0f, 0.25f);
        bgRT.anchorMax  = new Vector2(1f, 0.75f);
        bgRT.offsetMin  = Vector2.zero;
        bgRT.offsetMax  = Vector2.zero;

        // Fill area
        GameObject fillAreaGO = CreateUIObject("Fill Area", sliderGO.transform);
        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin  = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax  = new Vector2(1f, 0.75f);
        fillAreaRT.offsetMin  = new Vector2(5f, 0f);
        fillAreaRT.offsetMax  = new Vector2(-15f, 0f);

        GameObject fillGO = CreateUIObject("Fill", fillAreaGO.transform);
        Image fill = fillGO.AddComponent<Image>();
        fill.color = fillCol;
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillRT.sizeDelta = new Vector2(10f, 0f);

        // Handle
        GameObject handleAreaGO = CreateUIObject("Handle Slide Area", sliderGO.transform);
        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin  = new Vector2(0f, 0f);
        handleAreaRT.anchorMax  = new Vector2(1f, 1f);
        handleAreaRT.offsetMin  = new Vector2(10f, 0f);
        handleAreaRT.offsetMax  = new Vector2(-10f, 0f);

        GameObject handleGO = CreateUIObject("Handle", handleAreaGO.transform);
        Image handle = handleGO.AddComponent<Image>();
        handle.color = handleCol;
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta        = new Vector2(24f, 32f);
        handleRT.anchorMin        = new Vector2(0f, 0.5f);
        handleRT.anchorMax        = new Vector2(0f, 0.5f);
        handleRT.anchoredPosition = Vector2.zero;

        // Wire slider references
        slider.fillRect   = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handle;

        ColorBlock cb = slider.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.9f, 1f);
        cb.pressedColor     = new Color(0.7f, 0.7f, 0.9f);
        slider.colors = cb;

        return slider;
    }

    private static Button CreateButton(Transform parent, string goName,
                                       string label, Font font,
                                       Color normal, Color highlight,
                                       Color pressed, Color textCol)
    {
        GameObject go = CreateUIObject(goName, parent);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 68f);

        Image bg = go.AddComponent<Image>();
        bg.color = normal;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = normal;
        cb.highlightedColor = highlight;
        cb.pressedColor     = pressed;
        cb.selectedColor    = highlight;
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;
        btn.targetGraphic   = bg;

        GameObject textGO = CreateUIObject("Label", go.transform);
        Text text = textGO.AddComponent<Text>();
        text.text      = label;
        text.font      = font;
        text.fontSize  = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = textCol;
        StretchFull(textGO.GetComponent<RectTransform>());

        return btn;
    }

    // ----------------------------------------------------------------
    //  Wiring helpers
    // ----------------------------------------------------------------

    private static void SetProp(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void WireButton(Button btn, PauseMenu target, string methodName)
    {
        UnityAction call = System.Delegate.CreateDelegate(
            typeof(UnityAction), target, methodName) as UnityAction;
        if (call != null)
            UnityEventTools.AddPersistentListener(btn.onClick, call);
    }

    private static void WireSlider(Slider slider, PauseMenu target, string methodName)
    {
        UnityAction<float> call = System.Delegate.CreateDelegate(
            typeof(UnityAction<float>), target, methodName) as UnityAction<float>;
        if (call != null)
            UnityEventTools.AddPersistentListener(slider.onValueChanged, call);
    }
}
