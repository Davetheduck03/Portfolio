using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// One-click fix for slider handles that are too wide or mispositioned.
///
/// Run via  Tools → UI → Fix Slider Handles in Scene
///
/// What it does for every Slider found in the active scene:
///   • Finds the "Handle Slide Area" child and sets proper insets (8 px each side)
///   • Finds the "Handle" child inside that and sets:
///       anchorMin / anchorMax = (0, 0.5)  — point anchor, left-centre
///       pivot                 = (0.5, 0.5)
///       sizeDelta             = (12, 30)   — thin pill knob
///       anchoredPosition      = (0, 0)
/// </summary>
public static class FixSliderHandles
{
    [MenuItem("Tools/UI/Fix Slider Handles in Scene")]
    public static void Fix()
    {
        int fixed_count = 0;

        Slider[] sliders = Object.FindObjectsByType<Slider>(FindObjectsSortMode.None);

        foreach (Slider slider in sliders)
        {
            // ── Handle Slide Area ────────────────────────────────────
            Transform slideAreaT = slider.transform.Find("Handle Slide Area");
            if (slideAreaT != null)
            {
                Undo.RecordObject(slideAreaT.GetComponent<RectTransform>(),
                                  "Fix Handle Slide Area");

                RectTransform rt = slideAreaT.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(8f,  0f);
                rt.offsetMax = new Vector2(-8f, 0f);

                // ── Handle ───────────────────────────────────────────
                Transform handleT = slideAreaT.Find("Handle");
                if (handleT != null)
                {
                    Undo.RecordObject(handleT.GetComponent<RectTransform>(),
                                      "Fix Slider Handle RectTransform");

                    RectTransform hrt = handleT.GetComponent<RectTransform>();
                    hrt.anchorMin        = new Vector2(0f,   0.5f);
                    hrt.anchorMax        = new Vector2(0f,   0.5f);
                    hrt.pivot            = new Vector2(0.5f, 0.5f);
                    hrt.sizeDelta        = new Vector2(12f,  16f);
                    hrt.anchoredPosition = Vector2.zero;

                    fixed_count++;
                }
            }
        }

        if (fixed_count == 0)
            Debug.LogWarning("[FixSliderHandles] No sliders with a 'Handle Slide Area → Handle' " +
                             "hierarchy found in the active scene.");
        else
            Debug.Log($"[FixSliderHandles] Fixed {fixed_count} slider handle(s). " +
                      "Use Edit → Undo if you need to revert.");
    }
}
