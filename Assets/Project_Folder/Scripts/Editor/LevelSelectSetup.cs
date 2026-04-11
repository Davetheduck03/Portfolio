using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Builds the Chapter-based Level Select scene in one click.
/// Run via Tools → UI → Create Level Select.
///
/// Static structure created here — LevelSelectMenu populates the
/// chapter cards and level grid at runtime.
///
///  LevelSelectRoot  (LevelSelectMenu + LevelProgressManager)
///  LevelSelectCanvas
///    Background
///      ── ChapterView (active on start)
///            Title
///            ChapterScrollView  ← horizontal scroll
///              Viewport
///                Content        ← HorizontalLayoutGroup, cards spawned here
///      ── LevelView (inactive on start)
///            Header
///              BackButton
///              ChapterTitle
///            LevelScrollView
///              Viewport
///                Content        ← GridLayoutGroup, level buttons spawned here
/// </summary>
public static class LevelSelectSetup
{
    // ----------------------------------------------------------------
    //  Colours
    // ----------------------------------------------------------------

    static readonly Color BgColor        = new Color(0.06f, 0.06f, 0.09f, 1f);
    static readonly Color PanelColor     = new Color(0.09f, 0.09f, 0.12f, 1f);
    static readonly Color TitleColor     = new Color(0.82f, 0.86f, 1.00f, 1f);
    static readonly Color HeaderColor    = new Color(0.07f, 0.07f, 0.10f, 1f);
    static readonly Color BackBtnColor   = new Color(0.18f, 0.20f, 0.28f, 1f);
    static readonly Color BackBtnHover   = new Color(0.28f, 0.30f, 0.42f, 1f);
    static readonly Color BackTextColor  = new Color(0.85f, 0.85f, 0.90f, 1f);
    static readonly Color ScrollbarBg    = new Color(0.12f, 0.12f, 0.16f, 1f);
    static readonly Color ScrollbarThumb = new Color(0.28f, 0.30f, 0.42f, 1f);

    // ----------------------------------------------------------------
    //  Entry point
    // ----------------------------------------------------------------

    [MenuItem("Tools/UI/Create Level Select")]
    public static void CreateLevelSelect()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ── Root ─────────────────────────────────────────────────────
        GameObject root = new GameObject("LevelSelectRoot");
        Undo.RegisterCreatedObjectUndo(root, "Create Level Select");

        LevelSelectMenu menu = root.AddComponent<LevelSelectMenu>();

        if (Object.FindFirstObjectByType<LevelProgressManager>() == null)
        {
            root.AddComponent<LevelProgressManager>();
            Debug.Log("[LevelSelectSetup] Added LevelProgressManager.");
        }

        // ── Canvas ────────────────────────────────────────────────────
        GameObject canvasGO = MakeUIObject("LevelSelectCanvas", root.transform);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Full-screen background ─────────────────────────────────────
        GameObject bg = MakeUIObject("Background", canvasGO.transform);
        bg.AddComponent<Image>().color = BgColor;
        Stretch(bg);

        // ================================================================
        //  CHAPTER VIEW
        // ================================================================

        GameObject chapterView = MakeUIObject("ChapterView", bg.transform);
        chapterView.AddComponent<Image>().color = new Color(0, 0, 0, 0);  // transparent, just a container
        Stretch(chapterView);

        VerticalLayoutGroup cvLayout = chapterView.AddComponent<VerticalLayoutGroup>();
        cvLayout.childAlignment        = TextAnchor.UpperCenter;
        cvLayout.spacing               = 0f;
        cvLayout.padding               = new RectOffset(0, 0, 0, 0);
        cvLayout.childControlWidth     = true;
        cvLayout.childControlHeight    = false;
        cvLayout.childForceExpandWidth = true;
        cvLayout.childForceExpandHeight = false;

        // Title bar — HorizontalLayoutGroup so we can put a menu button on the left
        GameObject titleBarGO = MakeUIObject("TitleBar", chapterView.transform);
        titleBarGO.AddComponent<Image>().color = PanelColor;
        titleBarGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 120f);

        HorizontalLayoutGroup tbLayout = titleBarGO.AddComponent<HorizontalLayoutGroup>();
        tbLayout.childAlignment         = TextAnchor.MiddleLeft;
        tbLayout.spacing                = 0f;
        tbLayout.padding                = new RectOffset(24, 24, 0, 0);
        tbLayout.childControlHeight     = true;
        tbLayout.childControlWidth      = false;
        tbLayout.childForceExpandHeight = true;
        tbLayout.childForceExpandWidth  = false;

        // ← MENU button (left side)
        Button mainMenuBtn = CreateTextButton(titleBarGO.transform, "MainMenuButton",
                                              "← MENU", font, 24,
                                              BackBtnColor, BackBtnHover, BackTextColor,
                                              new Vector2(150f, 0f));

        // Title text (expands to fill the middle)
        GameObject titleTextGO = MakeUIObject("TitleText", titleBarGO.transform);
        LayoutElement titleLE = titleTextGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1f;
        Text titleText = titleTextGO.AddComponent<Text>();
        titleText.text      = "SELECT CHAPTER";
        titleText.font      = font;
        titleText.fontSize  = 48;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color     = TitleColor;

        // Right spacer — mirrors the button width so the title stays centred
        GameObject rightSpacer = MakeUIObject("RightSpacer", titleBarGO.transform);
        rightSpacer.AddComponent<RectTransform>().sizeDelta = new Vector2(150f, 0f);

        // Horizontal scroll view for chapter cards
        GameObject chScrollGO = MakeUIObject("ChapterScrollView", chapterView.transform);
        chScrollGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 800f);

        ScrollRect chScroll = chScrollGO.AddComponent<ScrollRect>();
        chScroll.horizontal     = true;
        chScroll.vertical       = false;
        chScroll.movementType   = ScrollRect.MovementType.Elastic;
        chScroll.elasticity     = 0.1f;
        chScroll.inertia        = true;
        chScroll.decelerationRate = 0.135f;

        // Viewport — RectMask2D clips by rect without needing a stencil write,
        // which avoids the transparent-Image stencil-cull issue in Unity 6.
        GameObject chViewport = MakeUIObject("Viewport", chScrollGO.transform);
        Stretch(chViewport);
        chViewport.AddComponent<RectMask2D>();

        // Content (chapter cards spawned here at runtime)
        GameObject chContent = MakeUIObject("Content", chViewport.transform);
        RectTransform chContentRT = chContent.GetComponent<RectTransform>();
        chContentRT.anchorMin        = new Vector2(0f, 0.5f);
        chContentRT.anchorMax        = new Vector2(0f, 0.5f);
        chContentRT.pivot            = new Vector2(0f, 0.5f);
        chContentRT.anchoredPosition = new Vector2(60f, 0f);
        chContentRT.sizeDelta        = Vector2.zero;

        HorizontalLayoutGroup chLayout = chContent.AddComponent<HorizontalLayoutGroup>();
        chLayout.childAlignment        = TextAnchor.MiddleLeft;
        chLayout.spacing               = 40f;
        chLayout.padding               = new RectOffset(60, 60, 0, 0);
        chLayout.childControlWidth     = false;
        chLayout.childControlHeight    = false;
        chLayout.childForceExpandWidth  = false;
        chLayout.childForceExpandHeight = false;

        ContentSizeFitter chCSF = chContent.AddComponent<ContentSizeFitter>();
        chCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Horizontal scrollbar
        GameObject chScrollbarGO = MakeUIObject("Scrollbar", chScrollGO.transform);
        RectTransform chSbRT = chScrollbarGO.GetComponent<RectTransform>();
        chSbRT.anchorMin        = new Vector2(0f, 0f);
        chSbRT.anchorMax        = new Vector2(1f, 0f);
        chSbRT.pivot            = new Vector2(0.5f, 0f);
        chSbRT.sizeDelta        = new Vector2(0f, 10f);
        chSbRT.anchoredPosition = Vector2.zero;
        chScrollbarGO.AddComponent<Image>().color = ScrollbarBg;

        Scrollbar chSb = chScrollbarGO.AddComponent<Scrollbar>();
        chSb.direction = Scrollbar.Direction.LeftToRight;
        RectTransform chSbHandle = BuildScrollbarHandle(chScrollbarGO.transform, ScrollbarThumb);
        chSb.handleRect    = chSbHandle;
        chSb.targetGraphic = chSbHandle.GetComponent<Image>();

        chScroll.viewport             = chViewport.GetComponent<RectTransform>();
        chScroll.content              = chContentRT;
        chScroll.horizontalScrollbar  = chSb;
        chScroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // ================================================================
        //  LEVEL VIEW
        // ================================================================

        GameObject levelView = MakeUIObject("LevelView", bg.transform);
        levelView.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        Stretch(levelView);
        levelView.SetActive(false);   // hidden until a chapter is clicked

        VerticalLayoutGroup lvLayout = levelView.AddComponent<VerticalLayoutGroup>();
        lvLayout.childAlignment        = TextAnchor.UpperCenter;
        lvLayout.spacing               = 0f;
        lvLayout.padding               = new RectOffset(0, 0, 0, 0);
        lvLayout.childControlWidth     = true;
        lvLayout.childControlHeight    = false;
        lvLayout.childForceExpandWidth = true;
        lvLayout.childForceExpandHeight = false;

        // Header bar (back button + chapter title)
        GameObject headerGO = MakeUIObject("Header", levelView.transform);
        headerGO.AddComponent<Image>().color = HeaderColor;
        headerGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 100f);

        HorizontalLayoutGroup headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment         = TextAnchor.MiddleLeft;
        headerLayout.spacing                = 24f;
        headerLayout.padding                = new RectOffset(40, 40, 0, 0);
        headerLayout.childControlHeight     = true;
        headerLayout.childControlWidth      = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childForceExpandWidth  = false;

        // Back button
        Button backBtn = CreateTextButton(headerGO.transform, "BackButton",
                                          "← BACK", font, 26,
                                          BackBtnColor, BackBtnHover, BackTextColor,
                                          new Vector2(160f, 0f));

        // Chapter title
        GameObject chTitleGO = MakeUIObject("ChapterTitle", headerGO.transform);
        chTitleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 0f);
        Text chTitleText = chTitleGO.AddComponent<Text>();
        chTitleText.text      = "";
        chTitleText.font      = font;
        chTitleText.fontSize  = 36;
        chTitleText.fontStyle = FontStyle.Bold;
        chTitleText.alignment = TextAnchor.MiddleLeft;
        chTitleText.color     = TitleColor;

        // Level scroll view
        GameObject lvScrollGO = MakeUIObject("LevelScrollView", levelView.transform);
        lvScrollGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 880f);

        ScrollRect lvScroll = lvScrollGO.AddComponent<ScrollRect>();
        lvScroll.horizontal   = false;
        lvScroll.vertical     = true;
        lvScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject lvViewport = MakeUIObject("Viewport", lvScrollGO.transform);
        Stretch(lvViewport);
        lvViewport.AddComponent<RectMask2D>();

        // Level grid content
        GameObject lvContent = MakeUIObject("Content", lvViewport.transform);
        RectTransform lvContentRT = lvContent.GetComponent<RectTransform>();
        lvContentRT.anchorMin        = new Vector2(0f, 1f);
        lvContentRT.anchorMax        = new Vector2(1f, 1f);
        lvContentRT.pivot            = new Vector2(0.5f, 1f);
        lvContentRT.anchoredPosition = Vector2.zero;
        lvContentRT.sizeDelta        = Vector2.zero;

        GridLayoutGroup grid = lvContent.AddComponent<GridLayoutGroup>();
        grid.cellSize         = new Vector2(160f, 160f);
        grid.spacing          = new Vector2(20f, 20f);
        grid.padding          = new RectOffset(60, 60, 40, 40);
        grid.startCorner      = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis        = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment   = TextAnchor.UpperLeft;
        grid.constraint       = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount  = 6;

        ContentSizeFitter lvCSF = lvContent.AddComponent<ContentSizeFitter>();
        lvCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Vertical scrollbar
        GameObject lvScrollbarGO = MakeUIObject("Scrollbar", lvScrollGO.transform);
        RectTransform lvSbRT = lvScrollbarGO.GetComponent<RectTransform>();
        lvSbRT.anchorMin        = new Vector2(1f, 0f);
        lvSbRT.anchorMax        = new Vector2(1f, 1f);
        lvSbRT.pivot            = new Vector2(1f, 0.5f);
        lvSbRT.sizeDelta        = new Vector2(10f, 0f);
        lvSbRT.anchoredPosition = Vector2.zero;
        lvScrollbarGO.AddComponent<Image>().color = ScrollbarBg;

        Scrollbar lvSb = lvScrollbarGO.AddComponent<Scrollbar>();
        lvSb.direction = Scrollbar.Direction.BottomToTop;
        RectTransform lvSbHandle = BuildScrollbarHandle(lvScrollbarGO.transform, ScrollbarThumb);
        lvSb.handleRect    = lvSbHandle;
        lvSb.targetGraphic = lvSbHandle.GetComponent<Image>();

        lvScroll.viewport                    = lvViewport.GetComponent<RectTransform>();
        lvScroll.content                     = lvContentRT;
        lvScroll.verticalScrollbar           = lvSb;
        lvScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // ================================================================
        //  Wire LevelSelectMenu fields
        // ================================================================

        SerializedObject so = new SerializedObject(menu);
        SetRef(so, "chapterView",    chapterView);
        SetRef(so, "chapterContent", chContentRT);
        SetRef(so, "levelView",      levelView);
        SetRef(so, "levelViewTitle", chTitleText);
        SetRef(so, "levelContent",   lvContentRT);
        so.ApplyModifiedProperties();

        // Wire Back button (level view) → LevelSelectMenu.OnBackPressed
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            backBtn.onClick,
            System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), menu,
                nameof(LevelSelectMenu.OnBackPressed))
            as UnityEngine.Events.UnityAction);

        // Wire ← MENU button (chapter view) → LevelSelectMenu.OnBackToMenuPressed
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            mainMenuBtn.onClick,
            System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), menu,
                nameof(LevelSelectMenu.OnBackToMenuPressed))
            as UnityEngine.Events.UnityAction);

        // ── EventSystem ───────────────────────────────────────────────
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[LevelSelectSetup] Done. Fill in the Chapters array on LevelSelectMenu.");
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    static GameObject MakeUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void Stretch(GameObject go,
                        Vector2? minOff = null, Vector2? maxOff = null)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = minOff ?? Vector2.zero;
        rt.offsetMax = maxOff ?? Vector2.zero;
    }

    static RectTransform BuildScrollbarHandle(Transform parent, Color color)
    {
        GameObject area = MakeUIObject("SlidingArea", parent);
        Stretch(area);

        GameObject handle = MakeUIObject("Handle", area.transform);
        handle.AddComponent<Image>().color = color;
        RectTransform rt = handle.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Button CreateTextButton(Transform parent, string name,
                                   string label, Font font, int fontSize,
                                   Color normal, Color hover, Color textCol,
                                   Vector2 size)
    {
        GameObject go = MakeUIObject(name, parent);
        go.GetComponent<RectTransform>().sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.color = normal;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = btn.colors;
        cb.normalColor      = normal;
        cb.highlightedColor = hover;
        cb.pressedColor     = new Color(normal.r * 0.6f, normal.g * 0.6f, normal.b * 0.6f, 1f);
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;

        GameObject labelGO = MakeUIObject("Label", go.transform);
        Stretch(labelGO);
        Text t = labelGO.AddComponent<Text>();
        t.text      = label;
        t.font      = font;
        t.fontSize  = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color     = textCol;

        return btn;
    }

    static void SetRef(SerializedObject so, string propName, Object value)
    {
        SerializedProperty p = so.FindProperty(propName);
        if (p != null) p.objectReferenceValue = value;
    }

    static void SetRef(SerializedObject so, string propName, Component value)
        => SetRef(so, propName, (Object)value);
}
