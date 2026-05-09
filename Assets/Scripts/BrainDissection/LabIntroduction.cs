using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a clear objective/welcome message when the user enters the lab.
/// The panel fades in, stays visible for a duration, then fades out.
/// Attach to a world-space Canvas or let it create one at runtime.
/// </summary>
public class LabIntroduction : MonoBehaviour
{
    [Header("Timing")]
    public float fadeInDuration = 1.0f;
    public float displayDuration = 8.0f;
    public float fadeOutDuration = 1.5f;

    [Header("Position")]
    public float distanceFromPlayer = 2.0f;
    public float heightOffset = 0.2f;

    [Header("Message")]
    [TextArea(5, 15)]
    public string introMessage =
        "<b>Welcome to the Brain Dissection Lab</b>\n\n" +
        "Your goal is to explore and understand the major\n" +
        "regions of the brain by performing a virtual dissection.\n\n" +
        "<b>Procedure:</b>\n" +
        "1. Equip gloves\n" +
        "2. Use the dissection knife\n" +
        "3. Separate the hemispheres\n" +
        "4. Inspect and extract brain regions using tweezers";

    private GameObject _introPanel;
    private CanvasGroup _canvasGroup;

    /// <summary>Called by MenuManager after Play sequence completes.</summary>
    public void ShowIntroduction()
    {
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        CreateIntroPanel();
        _canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }

        Destroy(_introPanel);
    }

    private void CreateIntroPanel()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 pos = cam.transform.position + forward * distanceFromPlayer;
        pos.y = cam.transform.position.y + heightOffset;

        _introPanel = new GameObject("LabIntroPanel");
        _introPanel.transform.position = pos;
        _introPanel.transform.rotation = Quaternion.LookRotation(
            _introPanel.transform.position - cam.transform.position);

        var canvas = _introPanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;

        var scaler = _introPanel.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        _canvasGroup = _introPanel.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        var rt = _introPanel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(700, 450);
        rt.localScale = Vector3.one * 0.0012f;

        var bgObj = new GameObject("BG");
        bgObj.transform.SetParent(_introPanel.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.07f, 0.16f, 0.24f, 0.94f);
        var bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(bgObj.transform, false);
        var text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.color = new Color(0.91f, 0.95f, 0.95f, 1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        text.text = introMessage;
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(30, 20);
        textRt.offsetMax = new Vector2(-30, -20);
    }
}
