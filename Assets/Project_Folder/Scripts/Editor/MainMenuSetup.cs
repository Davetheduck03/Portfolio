using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;

/// <summary>
/// Editor utility that builds a Main Menu Canvas in the active scene
/// and wires every button and slider to the MainMenu component.
/// Run via Tools → UI → Create Main Menu.
///
/// Creates:
///   MainMenuRoot  (has MainMenu behaviour)
///     └── MainMenuCanvas  (Canvas, always active)
///           ├── Background       (full-screen image)
///           ├── Title            ("GAME TITLE")
///           ├── ButtonGroup      (centred vertical stack)
///           │     ├── StartButton     → MainMenu.OnStartPressed
///           │     ├── ContinueButton  → MainMenu.OnContinuePressed
///           │     ├── LevelSelectButton → MainMenu.OnLevelSelectPressed
///           │     ├── SettingsButton  → MainMenu.OnSettingsPressed
///           │     ├── CreditsButton   → MainMenu.OnCreditsPressed
///           │     └── QuitButton      → MainMenu.OnQuitPressed
///           ├── SettingsPanel    (hidden by default)
///           │     ├── Title      "SETTINGS"
///           │     ├── MasterRow  [slider] → MainMenu.OnMasterSliderChanged
///           │     ├── MusicRow   [slider] → MainMenu.OnMusicSliderChanged
///           │     └── CloseButton → MainMenu.OnSettingsClose
///           └── CreditsPanel     (hidden by default)
///                 ├── Title      "CREDITS"
///                 ├── CreditsText
///                 └── CloseButton → MainMenu.OnCreditsClose
///
/// After creation assign:
///   • AudioMixer on MainMenuRoot → MainMenu → Audio Mixer
///   • All Level Indices array on MainMenuRoot → MainMenu
/// </summary>
public static class MainMenuSetup
{
    // ----------------------------------------------------------------
    //  Palette
    // ----------------------------------------------------------------

    private static readonly Color BackgroundColour  = new Color(0.06f, 0.07f, 0.10f, 1f);
    private static readonly Color TitleColour       = new Color(0.95f, 0.95f, 1.00f, 1f);
    private static readonly Color PanelColour       = new Color(0.08f, 0.10f, 0.14f, 1f);
    private static readonly Color OverlayColour     = new Color(0f,    0f,    0f,    0.72f);
    private static readonly Color PanelTitleColour  = new Color(0.85f, 0.85f, 1.00f, 1f);
    private static readonly Color LabelColour       = new Color(0.75f, 0.75f, 0.85f, 1f);
    private static readonly Color SliderBg          = new Color(0.20f, 0.20f, 0.28f, 1f);
    private static readonly Color SliderFill        = new Color(0.40f, 0.55f, 1.00f, 1f);
    private static readonly Color SliderHandle      = new Color(0.70f, 0.80f, 1.00f, 1f);
    private static readonly Color ButtonNormal      = new Color(0.14f, 0.16f, 0.22f, 1f);
    private static readonly Color ButtonHighlight   = new Color(0.24f, 0.28f, 0.40f, 1f);
    private static readonly Color ButtonPressed     = new Color(0.08f, 0.10f, 0.16f, 1f);
    private static readonly Color ButtonText        = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color CloseButtonNormal = new Color(0.22f, 0.14f, 0.14f, 1f);
    private static readonly Color CloseButtonHl     = new Color(0.38f, 0.20f, 0.20f, 1f);
    private static readonly Color CloseButtonPr     = new Color(0.12f, 0.08f, 0.08f, 1f);

    // ----------------------------------------------------------------
    //  Entry point
    // ----------------------------------------------------------------

    [MenuItem("Tools/UI/Create Main Menu")]
    public static void CreateMainMenu()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ── Root ─────────────────────────────────────────────────────
        GameObject root = new GameObject("MainMenuRoot");
        Undo.RegisterCreatedObjectUndo(root, "Create Main Menu");
        MainMenu mainMenu = root.AddComponent<MainMenu>();

        // ── Canvas ───────────────────────────────────────────────────
        GameObject canvasGO = CreateUIObject("MainMenuCanvas", root.transform);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Background ───────────────────────────────────────────────
        GameObject bgGO = CreateUIObject("Background", canvasGO.transform);
        bgGO.AddComponent<Image>().color = BackgroundColour;
        StretchFull(bgGO.GetComponent<RectTransform>());

        // ── Title ────────────────────────────────────────────────────
        GameObject titleGO = CreateUIObject("Title", canvasGO.transform);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text      = "GAME TITLE";
        titleText.font      = font;
        titleText.fontSize  = 80;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color     = TitleColour;

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.offsetMin        = new Vector2(0f,   -300f);
        titleRT.offsetMax        = new Vector2(0f,   -80f);

        // ── Button Group ─────────────────────────────────────────────
        // Place centred horizontally, below the title
        GameObject groupGO = CreateUIObject("ButtonGroup", canvasGO.transform);
        RectTransform groupRT = groupGO.GetComponent<RectTransform>();
        groupRT.anchorMin        = new Vector2(0.5f, 0.5f);
        groupRT.anchorMax        = new Vector2(0.5f, 0.5f);
        groupRT.pivot            = new Vector2(0.5f, 0.5f);
        groupRT.sizeDelta        = new Vector2(420f, 560f);
        groupRT.anchoredPosition = new Vector2(0f, -40f);

        VerticalLayoutGroup vlg = groupGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment        = TextAnchor.MiddleCenter;
        vlg.spacing               = 16f;
        vlg.padding               = new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        Button startBtn       = CreateMenuButton(groupGO.transform, "StartButton",     "START",        font);
        Button continueBtn    = CreateMenuButton(groupGO.transform, "ContinueButton",  "CONTINUE",     font);
        Button levelSelectBtn = CreateMenuButton(groupGO.transform, "LevelSelectButton","LEVEL SELECT", font);
        Button settingsBtn    = CreateMenuButton(groupGO.transform, "SettingsButton",  "SETTINGS",     font);
        Button creditsBtn     = CreateMenuButton(groupGO.transform, "CreditsButton",   "CREDITS",      font);
        Button quitBtn        = CreateMenuButton(groupGO.transform, "QuitButton",      "QUIT",         font);

        // ── Settings Panel ───────────────────────────────────────────
        GameObject settingsPanel = CreateOverlayPanel(canvasGO.transform, "SettingsPanel",
                                                      "SETTINGS", 480f, 420f, font);

        Transform settingsPanelInner = settingsPanel.transform.GetChild(0).GetChild(0); // Overlay → Panel

        Slider masterSlider = CreateSliderRow(settingsPanelInner, "MasterRow",
                                              "MASTER", font, LabelColour,
                                              SliderBg, SliderFill, SliderHandle);

        Slider musicSlider  = CreateSliderRow(settingsPanelInner, "MusicRow",
                                              "MUSIC",  font, LabelColour,
                                              SliderBg, SliderFill, SliderHandle);

        Button settingsCloseBtn = CreateCloseButton(settingsPanelInner, font);

        settingsPanel.SetActive(false);

        // ── Credits Panel ────────────────────────────────────────────
        GameObject creditsPanel = CreateOverlayPanel(canvasGO.transform, "CreditsPanel",
                                                     "CREDITS", 560f, 500f, font);

        Transform creditsPanelInner = creditsPanel.transform.GetChild(0).GetChild(0); // Overlay → Panel

        // Credits text area
        GameObject creditsTextGO = CreateUIObject("CreditsText", creditsPanelInner);
        Text creditsText = creditsTextGO.AddComponent<Text>();
        creditsText.text      = "Made with Unity\n\nYour Name Here\n\nArt, Code & Design";
        creditsText.font      = font;
        creditsText.fontSize  = 24;
        creditsText.fontStyle = FontStyle.Normal;
        creditsText.alignment = TextAnchor.MiddleCenter;
        creditsText.color     = LabelColour;
        creditsTextGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 220f);

        Button creditsCloseBtn = CreateCloseButton(creditsPanelInner, font);

        creditsPanel.SetActive(false);

        // ── Wire everything to MainMenu ───────────────────────────────
        SerializedObject so = new SerializedObject(mainMenu);
        SetProp(so, "startButton",    startBtn.gameObject);
        SetProp(so, "continueButton", continueBtn.gameObject);
        SetProp(so, "settingsPanel",  settingsPanel);
        SetProp(so, "creditsPanel",   creditsPanel);
        SetProp(so, "masterSlider",   masterSlider);
        SetProp(so, "musicSlider",    musicSlider);
        so.ApplyModifiedProperties();

        WireButton(startBtn,       mainMenu, nameof(MainMenu.OnStartPressed));
        WireButton(continueBtn,    mainMenu, nameof(MainMenu.OnContinuePressed));
        WireButton(levelSelectBtn, mainMenu, nameof(MainMenu.OnLevelSelectPressed));
        WireButton(settingsBtn,    mainMenu, nameof(MainMenu.OnSettingsPressed));
        WireButton(creditsBtn,     mainMenu, nameof(MainMenu.OnCreditsPressed));
        WireButton(quitBtn,        mainMenu, nameof(MainMenu.OnQuitPressed));
        WireButton(settingsCloseBtn, mainMenu, nameof(MainMenu.OnSettingsClose));
        WireButton(creditsCloseBtn,  mainMenu, nameof(MainMenu.OnCreditsClose));

        WireSlider(masterSlider, mainMenu, nameof(MainMenu.OnMasterSliderChanged));
        WireSlider(musicSlider,  mainMenu, nameof(MainMenu.OnMusicSliderChanged));

        // ── EventSystem ───────────────────────────────────────────────
        EnsureEventSystem();

        // ── Finish ───────────────────────────────────────────────────
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[MainMenuSetup] Main Menu created. " +
                  "Remember to:\n" +
                  "  1. Assign your AudioMixer to MainMenuRoot → MainMenu → Audio Mixer\n" +
                  "  2. Fill in 'All Level Indices' on MainMenuRoot → MainMenu\n" +
                  "  3. Set the correct build indices for First Level and Level Select Scene\n" +
                  "  4. Replace 'GAME TITLE' text on the Title object");
    }

    // ----------------------------------------------------------------
    //  Panel factory
    // ----------------------------------------------------------------

    /// <summary>
    /// Creates a full-screen overlay containing a centred panel with a title.
    /// Returns the overlay root GameObject (which you SetActive(false) to hide).
    /// The panel's inner transform is: panelGO → Overlay → Panel.
    /// </summary>
    private static GameObject CreateOverlayPanel(Transform parent, string name,
                                                  string titleText,
                                                  float width, float height, Font font)
    {
        GameObject panelRoot = CreateUIObject(name, parent);
        StretchFull(panelRoot.GetComponent<RectTransform>());

        // Dark overlay behind the card
        GameObject overlayGO = CreateUIObject("Overlay", panelRoot.transform);
        overlayGO.AddComponent<Image>().color = OverlayColour;
        StretchFull(overlayGO.GetComponent<RectTransform>());

        // Centred card
        GameObject cardGO = CreateUIObject("Panel", overlayGO.transform);
        cardGO.AddComponent<Image>().color = PanelColour;

        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin        = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax        = new Vector2(0.5f, 0.5f);
        cardRT.pivot            = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta        = new Vector2(width, height);
        cardRT.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = cardGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.MiddleCenter;
        vlg.spacing                = 20f;
        vlg.padding                = new RectOffset(48, 48, 40, 40);
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Panel title
        GameObject titleGO = CreateUIObject("Title", cardGO.transform);
        Text title = titleGO.AddComponent<Text>();
        title.text      = titleText;
        title.font      = font;
        title.fontSize  = 48;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color     = PanelTitleColour;
        titleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 68f);

        return panelRoot;
    }

    // ----------------------------------------------------------------
    //  UI factory helpers
    // ----------------------------------------------------------------

    private static Button CreateMenuButton(Transform parent, string goName,
                                           string label, Font font)
    {
        GameObject go = CreateUIObject(goName, parent);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 72f);

        Image bg = go.AddComponent<Image>();
        bg.color = ButtonNormal;

        // Image.color = ButtonNormal; ColorBlock.normalColor = white
        // so highlight/press tints are expressed as ratios to ButtonNormal
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(
            ButtonHighlight.r / ButtonNormal.r,
            ButtonHighlight.g / ButtonNormal.g,
            ButtonHighlight.b / ButtonNormal.b, 1f);
        cb.pressedColor     = new Color(
            ButtonPressed.r / ButtonNormal.r,
            ButtonPressed.g / ButtonNormal.g,
            ButtonPressed.b / ButtonNormal.b, 1f);
        cb.selectedColor    = Color.white;
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;
        btn.targetGraphic   = bg;

        GameObject textGO = CreateUIObject("Label", go.transform);
        Text text = textGO.AddComponent<Text>();
        text.text      = label;
        text.font      = font;
        text.fontSize  = 30;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = ButtonText;
        StretchFull(textGO.GetComponent<RectTransform>());

        return btn;
    }

    private static Button CreateCloseButton(Transform parent, Font font)
    {
        GameObject go = CreateUIObject("CloseButton", parent);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 60f);

        Image bg = go.AddComponent<Image>();
        bg.color = CloseButtonNormal;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(
            CloseButtonHl.r / CloseButtonNormal.r,
            CloseButtonHl.g / CloseButtonNormal.g,
            CloseButtonHl.b / CloseButtonNormal.b, 1f);
        cb.pressedColor     = new Color(
            CloseButtonPr.r / CloseButtonNormal.r,
            CloseButtonPr.g / CloseButtonNormal.g,
            CloseButtonPr.b / CloseButtonNormal.b, 1f);
        cb.selectedColor    = Color.white;
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;
        btn.targetGraphic   = bg;

        GameObject textGO = CreateUIObject("Label", go.transform);
        Text text = textGO.AddComponent<Text>();
        text.text      = "CLOSE";
        text.font      = font;
        text.fontSize  = 26;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = ButtonText;
        StretchFull(textGO.GetComponent<RectTransform>());

        return btn;
    }

    private static Slider CreateSliderRow(Transform parent, string rowName,
                                          string labelText, Font font,
                                          Color labelCol,
                                          Color bgCol, Color fillCol, Color handleCol)
    {
        GameObject rowGO = CreateUIObject(rowName, parent);
        rowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 56f);

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
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.MiddleLeft;
        label.color     = labelCol;

        // Slider
        GameObject sliderGO = CreateUIObject("Slider", rowGO.transform);
        sliderGO.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 0f);
        Slider slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;

        // Background
        GameObject bgGO = CreateUIObject("Background", sliderGO.transform);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = bgCol;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.25f);
        bgRT.anchorMax = new Vector2(1f, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Fill area
        GameObject fillAreaGO = CreateUIObject("Fill Area", sliderGO.transform);
        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRT.offsetMin = new Vector2(5f, 0f);
        fillAreaRT.offsetMax = new Vector2(-15f, 0f);

        GameObject fillGO = CreateUIObject("Fill", fillAreaGO.transform);
        Image fill = fillGO.AddComponent<Image>();
        fill.color = fillCol;
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillRT.sizeDelta = new Vector2(10f, 0f);

        // Handle Slide Area — inset by half the handle width so the knob
        // stays fully on-screen at both ends (handle width 12 → 8 px inset)
        GameObject handleAreaGO = CreateUIObject("Handle Slide Area", sliderGO.transform);
        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = new Vector2(0f, 0f);
        handleAreaRT.anchorMax = new Vector2(1f, 1f);
        handleAreaRT.offsetMin = new Vector2(8f,  0f);
        handleAreaRT.offsetMax = new Vector2(-8f, 0f);

        // Handle — small pill knob, point-anchored to the left-centre
        GameObject handleGO = CreateUIObject("Handle", handleAreaGO.transform);
        Image handle = handleGO.AddComponent<Image>();
        handle.color = handleCol;
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin        = new Vector2(0f, 0.5f);   // point anchor
        handleRT.anchorMax        = new Vector2(0f, 0.5f);
        handleRT.pivot            = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta        = new Vector2(12f, 16f);    // thin pill, shorter than track
        handleRT.anchoredPosition = Vector2.zero;

        slider.fillRect      = fillRT;
        slider.handleRect    = handleRT;
        slider.targetGraphic = handle;

        ColorBlock cb = slider.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.9f, 1f);
        cb.pressedColor     = new Color(0.7f, 0.7f, 0.9f);
        slider.colors       = cb;

        return slider;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        GameObject esGO = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();

        System.Type newInputModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        if (newInputModule != null)
            esGO.AddComponent(newInputModule);
        else
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        Debug.Log("[MainMenuSetup] Added EventSystem to the scene.");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ----------------------------------------------------------------
    //  Wiring helpers
    // ----------------------------------------------------------------

    private static void SetProp(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void WireButton(Button btn, MainMenu target, string methodName)
    {
        UnityAction call = System.Delegate.CreateDelegate(
            typeof(UnityAction), target, methodName) as UnityAction;
        if (call != null)
            UnityEventTools.AddPersistentListener(btn.onClick, call);
    }

    private static void WireSlider(Slider slider, MainMenu target, string methodName)
    {
        UnityAction<float> call = System.Delegate.CreateDelegate(
            typeof(UnityAction<float>), target, methodName) as UnityAction<float>;
        if (call != null)
            UnityEventTools.AddPersistentListener(slider.onValueChanged, call);
    }
}
