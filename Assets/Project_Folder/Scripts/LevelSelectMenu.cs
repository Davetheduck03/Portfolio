using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter-based level select.
///
/// Screen 1 — Chapter View: horizontal scroll of chapter cards.
///   Each card shows the chapter name, colour, and completion progress.
///   Locked chapters are dimmed (their first level has not been unlocked yet).
///
/// Screen 2 — Level Grid: shown after clicking a chapter card.
///   Displays a scrollable grid of level buttons for that chapter.
///   A back button returns to the chapter view.
///
/// Setup:
///   1. Run Tools → UI → Create Level Select to build the canvas.
///   2. Fill in the Chapters array.  Each chapter has a name, accent colour,
///      and its own Levels sub-array (display name + scene build index).
///   3. Ensure a LevelProgressManager exists in a persistent scene or on
///      this object (the editor script adds one automatically).
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Data types
    // ----------------------------------------------------------------

    [System.Serializable]
    public class LevelEntry
    {
        [Tooltip("Name shown on the level button (e.g. \"1-3\" or \"The Cavern\").")]
        public string displayName = "Level";

        [Tooltip("Build index of this level's scene.")]
        public int sceneBuildIndex;

        [Tooltip("Optional thumbnail shown inside the level button. Leave empty to use a plain colour.")]
        public Sprite thumbnail;
    }

    [System.Serializable]
    public class ChapterData
    {
        [Tooltip("Name shown on the chapter card.")]
        public string chapterName = "Chapter";

        [Tooltip("Accent colour for this chapter's card and level buttons.")]
        public Color accentColor = new Color(0.30f, 0.45f, 0.80f, 1f);

        [Tooltip("Optional banner image shown on the chapter card. Leave empty to use a plain colour.")]
        public Sprite thumbnail;

        [Tooltip("Levels in this chapter, in play order.")]
        public LevelEntry[] levels;
    }

    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Chapter Data")]
    [SerializeField] private ChapterData[] chapters;

    [Header("UI — Chapter View")]
    [SerializeField] private GameObject    chapterView;
    [SerializeField] private RectTransform chapterContent;   // HorizontalLayoutGroup content

    [Header("UI — Level View")]
    [SerializeField] private GameObject    levelView;
    [SerializeField] private Text          levelViewTitle;
    [SerializeField] private RectTransform levelContent;     // GridLayoutGroup content

    // ----------------------------------------------------------------
    //  Private state
    // ----------------------------------------------------------------

    private readonly List<GameObject> _spawnedChapterCards = new List<GameObject>();
    private readonly List<GameObject> _spawnedLevelButtons = new List<GameObject>();

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Start()
    {
        EnsureFirstLevelUnlocked();
        ShowChapterView();
    }

    void OnEnable()
    {
        // Refresh whenever this screen becomes active (e.g. returning from a level)
        EnsureFirstLevelUnlocked();
        ShowChapterView();
    }

    // ----------------------------------------------------------------
    //  Navigation
    // ----------------------------------------------------------------

    private void ShowChapterView()
    {
        if (chapterView != null) chapterView.SetActive(true);
        if (levelView   != null) levelView.SetActive(false);
        PopulateChapters();
    }

    /// <summary>Called by the Back button in the level view.</summary>
    public void OnBackPressed()
    {
        ShowChapterView();
    }

    private void OpenChapter(int chapterIndex)
    {
        if (chapterIndex < 0 || chapterIndex >= chapters.Length) return;

        ChapterData chapter = chapters[chapterIndex];

        if (chapterView != null) chapterView.SetActive(false);
        if (levelView   != null) levelView.SetActive(true);

        if (levelViewTitle != null)
            levelViewTitle.text = chapter.chapterName;

        PopulateLevels(chapter);
    }

    // ----------------------------------------------------------------
    //  Chapter card population
    // ----------------------------------------------------------------

    private void PopulateChapters()
    {
        if (chapterContent == null || chapters == null) return;

        // Clear old cards
        foreach (GameObject go in _spawnedChapterCards) Destroy(go);
        _spawnedChapterCards.Clear();

        for (int i = 0; i < chapters.Length; i++)
            _spawnedChapterCards.Add(CreateChapterCard(chapters[i], i));
    }

    private GameObject CreateChapterCard(ChapterData chapter, int index)
    {
        bool chapterUnlocked = IsChapterUnlocked(chapter);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── Card root ────────────────────────────────────────────────
        GameObject card = new GameObject(chapter.chapterName);
        card.transform.SetParent(chapterContent, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(280f, 360f);

        // Card background colour driven directly — no Button ColorBlock multiplication issue.
        Image bg = card.AddComponent<Image>();
        bg.color = chapterUnlocked
            ? new Color(chapter.accentColor.r * 0.25f,
                        chapter.accentColor.g * 0.25f,
                        chapter.accentColor.b * 0.25f, 1f)
            : new Color(0.10f, 0.10f, 0.13f, 1f);

        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.interactable  = chapterUnlocked;

        // Keep highlight/press states but start from the already-set bg.color
        ColorBlock cb  = btn.colors;
        cb.normalColor      = Color.white;   // multiplied against bg.color → no change
        cb.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
        cb.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.disabledColor    = Color.white;
        cb.colorMultiplier  = 1f;
        cb.fadeDuration     = 0.12f;
        btn.colors          = cb;

        if (chapterUnlocked)
        {
            int captured = index;
            btn.onClick.AddListener(() => OpenChapter(captured));
        }

        // ── All children use explicit anchor+offset positioning ───────
        // Card is 280 × 360.  Children anchored to top-stretch (anchorMin.x=0,
        // anchorMax.x=1, anchorMin.y=1, anchorMax.y=1) with padding x=12.
        // offsetMax.y = -yTop,  offsetMin.y = -(yTop + height)

        bool hasThumbnail = chapter.thumbnail != null;
        float pad = 12f;

        // ── Accent bar (full width, 8 px) ─────────────────────────────
        Color barColor = chapterUnlocked ? chapter.accentColor : new Color(0.25f, 0.25f, 0.30f, 1f);
        GameObject bar = CardChild("AccentBar", card.transform);
        SetTopRect(bar, 0f, 0f, 0f, 8f);
        bar.AddComponent<Image>().color = barColor;

        // ── Thumbnail OR colour band ───────────────────────────────────
        float thumbHeight = 148f;
        float thumbTop    = 8f;
        if (hasThumbnail)
        {
            GameObject thumbGO = CardChild("Thumbnail", card.transform);
            SetTopRect(thumbGO, 0f, 0f, thumbTop, thumbHeight);
            Image thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite         = chapter.thumbnail;
            thumbImg.type           = Image.Type.Simple;
            thumbImg.preserveAspect = false;
            if (!chapterUnlocked) thumbImg.color = new Color(1f, 1f, 1f, 0.25f);
        }
        else
        {
            // Tinted accent band so the card isn't a plain dark rectangle
            GameObject band = CardChild("AccentBand", card.transform);
            SetTopRect(band, 0f, 0f, thumbTop, thumbHeight);
            Color bandColor = chapterUnlocked
                ? new Color(chapter.accentColor.r * 0.18f,
                            chapter.accentColor.g * 0.18f,
                            chapter.accentColor.b * 0.18f, 1f)
                : new Color(0.08f, 0.08f, 0.10f, 1f);
            band.AddComponent<Image>().color = bandColor;
        }

        float contentTop = thumbTop + thumbHeight + 10f; // 166

        // ── Chapter number ────────────────────────────────────────────
        Color numColor = chapterUnlocked
            ? new Color(Mathf.Clamp01(chapter.accentColor.r + 0.45f),
                        Mathf.Clamp01(chapter.accentColor.g + 0.45f),
                        Mathf.Clamp01(chapter.accentColor.b + 0.45f), 1f)
            : new Color(0.35f, 0.35f, 0.40f, 1f);

        CardTextChild("Number", card.transform, $"{index + 1:00}",
                      font, 64, FontStyle.Bold, numColor,
                      pad, pad, contentTop, 80f);

        // ── Chapter name ──────────────────────────────────────────────
        Color nameColor = chapterUnlocked
            ? new Color(0.90f, 0.90f, 0.95f, 1f)
            : new Color(0.40f, 0.40f, 0.45f, 1f);

        CardTextChild("Name", card.transform, chapter.chapterName,
                      font, 20, FontStyle.Bold, nameColor,
                      pad, pad, contentTop + 80f, 44f);

        // ── Progress / locked label ───────────────────────────────────
        string progressText  = chapterUnlocked ? BuildProgressText(chapter) : "LOCKED";
        Color  progressColor = chapterUnlocked
            ? new Color(0.60f, 0.65f, 0.75f, 1f)
            : new Color(0.40f, 0.40f, 0.45f, 1f);

        CardTextChild("Progress", card.transform, progressText,
                      font, 17, FontStyle.Normal, progressColor,
                      pad, pad, contentTop + 128f, 28f);

        return card;
    }

    // Creates a bare child RectTransform parented to `parent`.
    private static GameObject CardChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    // Anchors child to top-stretch with explicit y offset from top and height.
    // xLeft/xRight are insets from the left and right edges respectively.
    private static void SetTopRect(GameObject go,
                                   float xLeft, float xRight,
                                   float yFromTop, float height)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = new Vector2(0f, 1f);
        rt.anchorMax  = new Vector2(1f, 1f);
        rt.pivot      = new Vector2(0.5f, 1f);
        rt.offsetMin  = new Vector2( xLeft,  -(yFromTop + height));
        rt.offsetMax  = new Vector2(-xRight, -yFromTop);
    }

    // Creates a Text child with explicit top-anchored rect.
    private static void CardTextChild(string name, Transform parent, string content,
                                      Font font, int fontSize, FontStyle style, Color color,
                                      float xLeft, float xRight, float yFromTop, float height)
    {
        GameObject go = CardChild(name, parent);
        SetTopRect(go, xLeft, xRight, yFromTop, height);
        Text t = go.AddComponent<Text>();
        t.text            = content;
        if (font != null) t.font = font;
        t.fontSize        = fontSize;
        t.fontStyle       = style;
        t.alignment       = TextAnchor.MiddleCenter;
        t.color           = color;
        t.resizeTextForBestFit = false;
        t.supportRichText = false;
    }

    // ----------------------------------------------------------------
    //  Level grid population
    // ----------------------------------------------------------------

    private void PopulateLevels(ChapterData chapter)
    {
        if (levelContent == null) return;

        foreach (GameObject go in _spawnedLevelButtons) Destroy(go);
        _spawnedLevelButtons.Clear();

        if (chapter.levels == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        foreach (LevelEntry entry in chapter.levels)
        {
            bool unlocked = LevelProgressManager.Instance != null
                ? LevelProgressManager.Instance.IsUnlocked(entry.sceneBuildIndex)
                : false;
            bool completed = LevelProgressManager.Instance != null
                ? LevelProgressManager.Instance.IsCompleted(entry.sceneBuildIndex)
                : false;

            _spawnedLevelButtons.Add(
                CreateLevelButton(entry, chapter.accentColor, unlocked, completed, font));
        }
    }

    private GameObject CreateLevelButton(LevelEntry entry, Color accent,
                                         bool unlocked, bool completed, Font font)
    {
        GameObject go = new GameObject(entry.displayName);
        go.transform.SetParent(levelContent, false);

        // Size is controlled by the GridLayoutGroup on levelContent
        Image bg = go.AddComponent<Image>();
        bg.color = unlocked
            ? new Color(accent.r * 0.30f, accent.g * 0.30f, accent.b * 0.30f, 1f)
            : new Color(0.10f, 0.10f, 0.13f, 1f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.interactable  = unlocked;

        // normalColor = white so it multiplies cleanly against bg.color.
        // Highlight brightens, press darkens.
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
        cb.pressedColor     = new Color(0.65f, 0.65f, 0.65f, 1f);
        cb.disabledColor    = Color.white;
        cb.colorMultiplier  = 1f;
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;

        if (unlocked)
        {
            int idx = entry.sceneBuildIndex;
            btn.onClick.AddListener(() => LoadLevel(idx));
        }

        // ── Thumbnail (optional) ─────────────────────────────────────
        if (entry.thumbnail != null)
        {
            GameObject thumbGO = new GameObject("Thumbnail");
            thumbGO.transform.SetParent(go.transform, false);
            RectTransform thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = Vector2.zero;
            thumbRT.anchorMax = Vector2.one;
            thumbRT.offsetMin = Vector2.zero;
            thumbRT.offsetMax = Vector2.zero;
            Image thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite         = entry.thumbnail;
            thumbImg.type           = Image.Type.Simple;
            thumbImg.preserveAspect = false;
            thumbImg.raycastTarget  = false; // let clicks pass through to the button
            if (!unlocked) thumbImg.color = new Color(1f, 1f, 1f, 0.20f); // dim when locked
        }

        // Level name
        Color textCol = unlocked
            ? new Color(0.90f, 0.90f, 0.95f, 1f)
            : new Color(0.35f, 0.35f, 0.40f, 1f);

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin  = new Vector2(0f, 0.3f);
        labelRT.anchorMax  = new Vector2(1f, 1f);
        labelRT.offsetMin  = new Vector2(6f, 0f);
        labelRT.offsetMax  = new Vector2(-6f, 0f);
        Text label = labelGO.AddComponent<Text>();
        label.text      = unlocked ? entry.displayName : "🔒";
        label.font      = font;
        label.fontSize  = 20;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color     = textCol;

        // Completion tick
        if (completed)
        {
            GameObject tickGO = new GameObject("Tick");
            tickGO.transform.SetParent(go.transform, false);
            RectTransform tickRT = tickGO.AddComponent<RectTransform>();
            tickRT.anchorMin  = new Vector2(0f, 0f);
            tickRT.anchorMax  = new Vector2(1f, 0.35f);
            tickRT.offsetMin  = Vector2.zero;
            tickRT.offsetMax  = Vector2.zero;
            Text tick = tickGO.AddComponent<Text>();
            tick.text      = "✓";
            tick.font      = font;
            tick.fontSize  = 16;
            tick.alignment = TextAnchor.MiddleCenter;
            tick.color     = new Color(
                Mathf.Clamp01(accent.r + 0.4f),
                Mathf.Clamp01(accent.g + 0.4f),
                Mathf.Clamp01(accent.b + 0.4f), 1f);
        }

        return go;
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private void EnsureFirstLevelUnlocked()
    {
        if (chapters == null || chapters.Length == 0) return;
        ChapterData first = chapters[0];
        if (first.levels == null || first.levels.Length == 0) return;
        LevelProgressManager.Instance?.UnlockLevel(first.levels[0].sceneBuildIndex);
    }

    private bool IsChapterUnlocked(ChapterData chapter)
    {
        if (chapter.levels == null || chapter.levels.Length == 0) return false;
        if (LevelProgressManager.Instance == null) return true;
        return LevelProgressManager.Instance.IsUnlocked(chapter.levels[0].sceneBuildIndex);
    }

    private string BuildProgressText(ChapterData chapter)
    {
        if (chapter.levels == null || chapter.levels.Length == 0) return "";
        if (LevelProgressManager.Instance == null) return $"0 / {chapter.levels.Length}";

        int[] indices = new int[chapter.levels.Length];
        for (int i = 0; i < chapter.levels.Length; i++)
            indices[i] = chapter.levels[i].sceneBuildIndex;

        int done = LevelProgressManager.Instance.CountCompleted(indices);
        return $"{done} / {chapter.levels.Length}";
    }

    private void LoadLevel(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex);
    }

    // ----------------------------------------------------------------
    //  Public helpers (DebugLevelUnlock)
    // ----------------------------------------------------------------

    /// <summary>Returns every build index across all chapters.</summary>
    public int[] GetAllBuildIndices()
    {
        var indices = new List<int>();
        if (chapters == null) return indices.ToArray();
        foreach (ChapterData ch in chapters)
            if (ch.levels != null)
                foreach (LevelEntry e in ch.levels)
                    indices.Add(e.sceneBuildIndex);
        return indices.ToArray();
    }

    /// <summary>Refreshes whichever view is currently visible.</summary>
    public void Refresh()
    {
        if (levelView != null && levelView.activeSelf)
        {
            // Find which chapter is open by matching the title
            if (chapters != null && levelViewTitle != null)
                foreach (ChapterData ch in chapters)
                    if (ch.chapterName == levelViewTitle.text)
                    { PopulateLevels(ch); return; }
        }
        else
        {
            PopulateChapters();
        }
    }
}
