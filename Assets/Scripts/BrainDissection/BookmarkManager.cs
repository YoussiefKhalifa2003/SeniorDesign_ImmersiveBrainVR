using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Saves and restores camera/brain bookmark positions in Play mode.
/// Up to 5 bookmarks per user, persisted in PlayerPrefs.
/// </summary>
public class BookmarkManager : MonoBehaviour
{
    public static BookmarkManager Instance { get; private set; }

    const int MaxBookmarks = 5;
    const string PrefsPrefix = "Bookmark_";

    [System.Serializable]
    struct Bookmark
    {
        public string label;
        public Vector3 brainRootEuler;
        public float opacity;
    }

    List<Bookmark> _bookmarks = new List<Bookmark>();
    GameObject _bookmarkPanel;
    List<GameObject> _slotButtons = new List<GameObject>();
    GameObject _saveBtn;
    Text _statusText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        LoadBookmarks();
        BuildBookmarkUI();
    }

    string PrefsKey => PrefsPrefix + (SessionData.UserName ?? "default");

    void LoadBookmarks()
    {
        _bookmarks.Clear();
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;

        var wrapper = JsonUtility.FromJson<BookmarkListWrapper>(json);
        if (wrapper != null && wrapper.items != null)
            _bookmarks.AddRange(wrapper.items);
    }

    void SaveBookmarks()
    {
        var wrapper = new BookmarkListWrapper { items = _bookmarks };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    [System.Serializable]
    class BookmarkListWrapper { public List<Bookmark> items = new List<Bookmark>(); }

    void BuildBookmarkUI()
    {
        if (_bookmarkPanel != null) Destroy(_bookmarkPanel);

        var uiCtrl = FindFirstObjectByType<RegionUIController>();
        Transform parent = uiCtrl != null && uiCtrl.mainButtonPanel != null
            ? uiCtrl.mainButtonPanel.transform : null;

        if (parent == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) parent = canvas.transform;
        }

        if (parent == null) return;

        _bookmarkPanel = new GameObject("BookmarkPanel");
        _bookmarkPanel.transform.SetParent(parent, false);

        var rt = _bookmarkPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, -60);
        rt.sizeDelta = new Vector2(0, 50);

        var layout = _bookmarkPanel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _saveBtn = CreateButton("SaveBookmark", _bookmarkPanel.transform, "\u2605 Save", font,
            new Color(0.2f, 0.55f, 0.3f), new Vector2(100, 36));
        _saveBtn.GetComponent<Button>().onClick.AddListener(SaveCurrentView);

        _slotButtons.Clear();
        for (int i = 0; i < MaxBookmarks; i++)
        {
            int idx = i;
            string label = i < _bookmarks.Count ? _bookmarks[i].label : $"Slot {i + 1}";
            bool active = i < _bookmarks.Count;
            var btn = CreateButton($"Bookmark_{i}", _bookmarkPanel.transform, label, font,
                active ? new Color(0.25f, 0.4f, 0.65f) : new Color(0.25f, 0.25f, 0.3f),
                new Vector2(90, 36));
            if (active)
                btn.GetComponent<Button>().onClick.AddListener(() => LoadBookmark(idx));
            else
                btn.GetComponent<Button>().interactable = false;
            _slotButtons.Add(btn);
        }

        var statusGO = new GameObject("BookmarkStatus");
        statusGO.transform.SetParent(_bookmarkPanel.transform, false);
        var statusRT = statusGO.AddComponent<RectTransform>();
        statusRT.sizeDelta = new Vector2(150, 30);
        _statusText = statusGO.AddComponent<Text>();
        _statusText.font = font;
        _statusText.fontSize = 11;
        _statusText.color = new Color(0.7f, 0.7f, 0.7f);
        _statusText.alignment = TextAnchor.MiddleCenter;
        _statusText.text = "";
    }

    void SaveCurrentView()
    {
        var bm = FindFirstObjectByType<BrainManager>();
        if (bm == null || bm.brainRoot == null) return;

        if (_bookmarks.Count >= MaxBookmarks)
        {
            _bookmarks.RemoveAt(0);
        }

        var bookmark = new Bookmark
        {
            label = $"View {_bookmarks.Count + 1}",
            brainRootEuler = bm.brainRoot.transform.eulerAngles,
            opacity = 1f
        };

        var slider = bm.regionUIController != null ? bm.regionUIController.opacitySlider : null;
        if (slider != null) bookmark.opacity = slider.value;

        _bookmarks.Add(bookmark);
        SaveBookmarks();
        RefreshSlotButtons();

        if (_statusText != null) _statusText.text = "Bookmark saved!";
        StartCoroutine(ClearStatusAfter(2f));
    }

    void LoadBookmark(int index)
    {
        if (index < 0 || index >= _bookmarks.Count) return;

        var bm = FindFirstObjectByType<BrainManager>();
        if (bm == null || bm.brainRoot == null) return;

        var bookmark = _bookmarks[index];
        bm.brainRoot.transform.eulerAngles = bookmark.brainRootEuler;

        var slider = bm.regionUIController != null ? bm.regionUIController.opacitySlider : null;
        if (slider != null) slider.value = bookmark.opacity;

        if (_statusText != null) _statusText.text = $"Loaded: {bookmark.label}";
        StartCoroutine(ClearStatusAfter(2f));
    }

    void RefreshSlotButtons()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < _slotButtons.Count && i < MaxBookmarks; i++)
        {
            bool active = i < _bookmarks.Count;
            var btn = _slotButtons[i].GetComponent<Button>();
            var txt = _slotButtons[i].GetComponentInChildren<Text>();
            if (txt != null) txt.text = active ? _bookmarks[i].label : $"Slot {i + 1}";
            btn.interactable = active;
            _slotButtons[i].GetComponent<Image>().color = active
                ? new Color(0.25f, 0.4f, 0.65f) : new Color(0.25f, 0.25f, 0.3f);
            btn.onClick.RemoveAllListeners();
            if (active)
            {
                int idx = i;
                btn.onClick.AddListener(() => LoadBookmark(idx));
            }
        }
    }

    System.Collections.IEnumerator ClearStatusAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null) _statusText.text = "";
    }

    static GameObject CreateButton(string name, Transform parent, string label, Font font, Color bg, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        var colors = btn.colors;
        colors.normalColor = bg; colors.highlightedColor = bg * 1.2f; colors.pressedColor = bg * 0.8f;
        btn.colors = colors;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = label; txt.font = font;
        txt.fontSize = 11; txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(0.95f, 0.95f, 0.97f);
        txt.alignment = TextAnchor.MiddleCenter;
        return go;
    }
}
