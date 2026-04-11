using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;

/// <summary>
/// Patches an existing Level Select hierarchy to add a  ← MENU  button
/// to the Chapter View title bar, wired to LevelSelectMenu.OnBackToMenuPressed.
///
/// Run via  Tools → UI → Add Menu Button to Level Select
///
/// Safe to run on a scene you've already colour-customised — it only adds
/// new objects and components; nothing existing is removed or recoloured.
/// The new button's visual style is copied from the existing ← BACK button
/// so it matches automatically.
/// </summary>
public static class AddMenuButtonToLevelSelect
{
    [MenuItem("Tools/UI/Add Menu Button to Level Select")]
    public static void Run()
    {
        // ── 1.  Find the LevelSelectMenu ────────────────────────────────────
        LevelSelectMenu menu = Object.FindFirstObjectByType<LevelSelectMenu>(
                                   FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Add Menu Button",
                "No LevelSelectMenu found in the active scene.\n" +
                "Run  Tools → UI → Create Level Select  first.",
                "OK");
            return;
        }

        // ── 2.  Find ChapterView → TitleBar ─────────────────────────────────
        Transform chapterView = DeepFind(menu.transform, "ChapterView");
        if (chapterView == null)
        {
            EditorUtility.DisplayDialog("Add Menu Button",
                "Could not find 'ChapterView' under LevelSelectMenu.\n" +
                "Make sure the hierarchy was created by LevelSelectSetup.",
                "OK");
            return;
        }

        Transform titleBar = DeepFind(chapterView, "TitleBar");
        if (titleBar == null)
        {
            EditorUtility.DisplayDialog("Add Menu Button",
                "Could not find 'TitleBar' under ChapterView.", "OK");
            return;
        }

        // ── 3.  Already patched? ─────────────────────────────────────────────
        if (titleBar.Find("MainMenuButton") != null)
        {
            EditorUtility.DisplayDialog("Add Menu Button",
                "A 'MainMenuButton' already exists inside TitleBar.\n" +
                "Nothing was changed.", "OK");
            return;
        }

        // ── 4.  Read style from the existing ← BACK button ──────────────────
        //       (it lives in LevelView → Header → BackButton)
        StyleInfo style = ReadStyleFromBackButton(menu.transform);

        // ── 5.  Ensure TitleBar has a HorizontalLayoutGroup ──────────────────
        HorizontalLayoutGroup hlg = titleBar.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = Undo.AddComponent<HorizontalLayoutGroup>(titleBar.gameObject);
        }
        // Configure to match the LevelView header style
        Undo.RecordObject(hlg, "Configure TitleBar HLG");
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.spacing                = 0f;
        hlg.padding                = new RectOffset(24, 24, 0, 0);
        hlg.childControlHeight     = true;
        hlg.childControlWidth      = true;     // let LayoutElement drive widths
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth  = false;

        // ── 6.  Fix the existing TitleText so it fills the middle ────────────
        Transform titleTextT = titleBar.Find("TitleText");
        if (titleTextT != null)
        {
            LayoutElement le = titleTextT.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(titleTextT.gameObject);
            Undo.RecordObject(le, "Configure TitleText LayoutElement");
            le.flexibleWidth = 1f;
        }

        // ── 7.  Create ← MENU button (insert before TitleText) ──────────────
        GameObject btnGO = new GameObject("MainMenuButton");
        Undo.RegisterCreatedObjectUndo(btnGO, "Add Menu Button");
        btnGO.transform.SetParent(titleBar, false);
        btnGO.transform.SetAsFirstSibling();    // left of title text

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.minWidth       = style.buttonWidth;
        btnLE.preferredWidth = style.buttonWidth;
        btnLE.flexibleWidth  = 0f;

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = style.bgColor;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        ColorBlock cb     = btn.colors;
        cb.normalColor      = style.cbNormal;
        cb.highlightedColor = style.cbHover;
        cb.pressedColor     = style.cbPressed;
        cb.disabledColor    = style.cbDisabled;
        cb.colorMultiplier  = style.cbMultiplier;
        cb.fadeDuration     = style.cbFadeDuration;
        btn.colors          = cb;

        // Label
        GameObject labelGO = new GameObject("Label");
        Undo.RegisterCreatedObjectUndo(labelGO, "Add Menu Button Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        Text label = labelGO.AddComponent<Text>();
        label.text      = "← MENU";
        label.font      = style.font;
        label.fontSize  = style.fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color     = style.textColor;

        // ── 8.  Add a right spacer so the title stays centred ────────────────
        GameObject spacer = new GameObject("RightSpacer");
        Undo.RegisterCreatedObjectUndo(spacer, "Add Menu Button Spacer");
        spacer.transform.SetParent(titleBar, false);
        spacer.transform.SetAsLastSibling();

        LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.minWidth       = style.buttonWidth;
        spacerLE.preferredWidth = style.buttonWidth;
        spacerLE.flexibleWidth  = 0f;

        // ── 9.  Wire onClick → LevelSelectMenu.OnBackToMenuPressed ───────────
        UnityEventTools.AddPersistentListener(
            btn.onClick,
            (UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityAction), menu,
                nameof(LevelSelectMenu.OnBackToMenuPressed)));

        EditorUtility.SetDirty(titleBar.gameObject);
        EditorUtility.SetDirty(menu.gameObject);

        Selection.activeGameObject = btnGO;
        EditorGUIUtility.PingObject(btnGO);

        Debug.Log("[AddMenuButtonToLevelSelect] Done — 'MainMenuButton' added to TitleBar " +
                  "and wired to LevelSelectMenu.OnBackToMenuPressed.");
    }

    // ====================================================================
    //  Style harvesting
    // ====================================================================

    private struct StyleInfo
    {
        public Color bgColor;
        public Color cbNormal, cbHover, cbPressed, cbDisabled;
        public float cbMultiplier, cbFadeDuration;
        public Color textColor;
        public Font  font;
        public int   fontSize;
        public float buttonWidth;
    }

    /// <summary>
    /// Reads visual properties from the existing "← BACK" button so the
    /// new button matches whatever colours the user has already customised.
    /// Falls back to the LevelSelectSetup defaults if nothing is found.
    /// </summary>
    private static StyleInfo ReadStyleFromBackButton(Transform root)
    {
        // Defaults (match LevelSelectSetup.cs)
        StyleInfo s = new StyleInfo
        {
            bgColor        = new Color(0.18f, 0.20f, 0.28f, 1f),
            cbNormal        = new Color(0.18f, 0.20f, 0.28f, 1f),
            cbHover         = new Color(0.28f, 0.30f, 0.42f, 1f),
            cbPressed       = new Color(0.11f, 0.12f, 0.17f, 1f),
            cbDisabled      = new Color(0.5f, 0.5f, 0.5f, 1f),
            cbMultiplier    = 1f,
            cbFadeDuration  = 0.1f,
            textColor       = new Color(0.85f, 0.85f, 0.90f, 1f),
            font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
            fontSize        = 24,
            buttonWidth     = 150f,
        };

        // Try to find the BackButton under LevelView → Header
        Transform backBtnT = DeepFind(root, "BackButton");
        if (backBtnT == null) return s;

        Button backBtn = backBtnT.GetComponent<Button>();
        Image  backImg = backBtnT.GetComponent<Image>();

        if (backImg != null) s.bgColor = backImg.color;

        if (backBtn != null)
        {
            ColorBlock cb    = backBtn.colors;
            s.cbNormal       = cb.normalColor;
            s.cbHover        = cb.highlightedColor;
            s.cbPressed      = cb.pressedColor;
            s.cbDisabled     = cb.disabledColor;
            s.cbMultiplier   = cb.colorMultiplier;
            s.cbFadeDuration = cb.fadeDuration;
        }

        // Grab text style from the button's Label child
        Text backLabel = backBtnT.GetComponentInChildren<Text>(includeInactive: true);
        if (backLabel != null)
        {
            s.textColor = backLabel.color;
            if (backLabel.font != null) s.font = backLabel.font;
            s.fontSize = backLabel.fontSize;
        }

        // Match the button width to the existing back button
        RectTransform backRT = backBtnT.GetComponent<RectTransform>();
        if (backRT != null && backRT.sizeDelta.x > 0f)
            s.buttonWidth = backRT.sizeDelta.x;

        return s;
    }

    // ====================================================================
    //  Hierarchy utilities
    // ====================================================================

    /// <summary>
    /// Depth-first search for the first Transform named <paramref name="name"/>
    /// anywhere under <paramref name="root"/> (including inactive objects).
    /// </summary>
    private static Transform DeepFind(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = DeepFind(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
