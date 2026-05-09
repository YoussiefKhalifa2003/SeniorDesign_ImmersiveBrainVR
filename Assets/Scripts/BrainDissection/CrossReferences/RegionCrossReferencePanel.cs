using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds and refreshes the "Adjacent to" / "Related to" pill rows on the
/// dissection details panel at runtime. The panel is constructed lazily so
/// no editor-time wiring is required — the script attaches itself to the
/// same GameObject as <see cref="RegionUIController"/> via
/// <see cref="RuntimeInitializeOnLoadMethod"/>.
///
/// Layout assumptions (set in BrainDissectionSceneSetup):
///   - detailsPanel: 820 x 480, pivot (0.5, 0.5)
///   - DetailedDesc body: anchoredPos (0, -10), size (720, 240)
///   - Bottom buttons row at y = -195
///
/// To make room without touching scene setup, we shrink the body's
/// rect when cross-references are visible (height 200, anchored y +10) and
/// restore the original rect when they are cleared. The pill strip then
/// sits at y = -150 with height 60, which fits cleanly above the buttons.
/// </summary>
public class RegionCrossReferencePanel : MonoBehaviour
{
    static readonly Color LabelColor = new Color(0.66f, 0.70f, 0.78f, 1f);
    static readonly Color PillBg = new Color(0.20f, 0.40f, 0.65f, 0.90f);
    static readonly Color PillBgDisabled = new Color(0.20f, 0.30f, 0.40f, 0.55f);
    static readonly Color PillText = new Color(0.95f, 0.95f, 0.97f, 1f);

    const float ClickCooldown = 0.35f;
    const int MaxRefsPerCategory = 3;
    const int MaxPillLabelCharacters = 18;
    const float PillWidth = 150f;

    RegionUIController _ui;

    GameObject _container;
    GameObject _adjacentRow;
    GameObject _relatedRow;
    Transform _adjacentPills;
    Transform _relatedPills;

    Vector2 _bodyOriginalAnchoredPos;
    Vector2 _bodyOriginalSizeDelta;
    bool _bodyRectCaptured;
    bool _bodyShrunk;

    float _lastClickTime = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureAttached();

    static void EnsureAttached()
    {
        var ui = Object.FindFirstObjectByType<RegionUIController>();
        if (ui == null) return;
        if (ui.GetComponent<RegionCrossReferencePanel>() != null) return;
        ui.gameObject.AddComponent<RegionCrossReferencePanel>();
    }

    void Awake()
    {
        _ui = GetComponent<RegionUIController>();
    }

    void OnEnable()
    {
        BrainManager.OnInspectionEnded += OnInspectionEnded;
    }

    void OnDisable()
    {
        BrainManager.OnInspectionEnded -= OnInspectionEnded;
    }

    void OnInspectionEnded() => ClearImmediate();

    void LateUpdate()
    {
        if (_ui == null || _ui.detailsPanel == null) return;

        // Refresh whenever the details panel is visible and the inspected
        // region has cross-refs to display. Polling here is cheap (rebuild
        // is no-op if region hasn't changed) and avoids us needing to hook
        // into every place ShowRegionDetails is called.
        if (!_ui.detailsPanel.activeInHierarchy)
        {
            ClearImmediate();
            return;
        }

        var bm = FindFirstObjectByType<BrainManager>();
        if (bm == null) return;
        var region = bm.InspectedRegion;
        var data = region != null ? region.regionData : null;

        if (data == null)
        {
            ClearImmediate();
            return;
        }

        Refresh(data);
    }

    RegionData _builtFor;

    void Refresh(RegionData data)
    {
        if (_builtFor == data && _container != null && _container.activeSelf) return;

        EnsureContainer();

        // First honour any Inspector-set arrays (manual overrides win).
        // If both are empty, fall back to the runtime cross-reference
        // table so the panel works without anyone having to run an editor
        // menu or hand-author every region's pill list.
        RegionData[] adjacent = data.adjacentRegions;
        RegionData[] related = data.relatedRegions;
        int adjCount = CountNonNull(adjacent);
        int relCount = CountNonNull(related);

        bool usedRuntimeFallback = false;
        if (adjCount == 0 && relCount == 0)
        {
            var fallback = RegionCrossReferenceData.Resolve(data);
            adjacent = fallback.adjacent ?? System.Array.Empty<RegionData>();
            related = fallback.related ?? System.Array.Empty<RegionData>();
            adjCount = adjacent.Length;
            relCount = related.Length;
            usedRuntimeFallback = adjCount > 0 || relCount > 0;
        }

        int sourceAdjCount = adjCount;
        int sourceRelCount = relCount;
        adjacent = TakeFirstNonNull(adjacent, MaxRefsPerCategory);
        related = TakeFirstNonNull(related, MaxRefsPerCategory);
        adjCount = adjacent.Length;
        relCount = related.Length;

        bool hasAdjacent = adjCount > 0;
        bool hasRelated = relCount > 0;
        bool capped = sourceAdjCount > adjCount || sourceRelCount > relCount;

        if (_builtFor != data)
            Debug.Log($"[CrossRef] Inspecting '{data.displayName}': {adjCount} adjacent, {relCount} related{(usedRuntimeFallback ? " (runtime lookup)" : "")}{(capped ? " (capped)" : "")}.");

        if (!hasAdjacent && !hasRelated)
        {
            ClearImmediate();
            _builtFor = data; // suppress repeated logging while held
            return;
        }

        ShrinkBody();
        _container.SetActive(true);
        _builtFor = data;

        BuildRow(_adjacentRow, _adjacentPills, "Adjacent to:", adjacent, hasAdjacent);
        BuildRow(_relatedRow, _relatedPills, "Related to:", related, hasRelated);
    }

    static int CountNonNull(RegionData[] arr)
    {
        if (arr == null) return 0;
        int n = 0;
        for (int i = 0; i < arr.Length; i++) if (arr[i] != null) n++;
        return n;
    }

    static RegionData[] TakeFirstNonNull(RegionData[] arr, int max)
    {
        if (arr == null || max <= 0) return System.Array.Empty<RegionData>();

        var result = new RegionData[Mathf.Min(max, CountNonNull(arr))];
        int n = 0;
        for (int i = 0; i < arr.Length && n < result.Length; i++)
        {
            if (arr[i] == null) continue;
            result[n++] = arr[i];
        }
        return result;
    }

    void ClearImmediate()
    {
        if (_container == null) return;
        _container.SetActive(false);
        ClearChildren(_adjacentPills);
        ClearChildren(_relatedPills);
        _builtFor = null;
        RestoreBody();
    }

    void EnsureContainer()
    {
        if (_container != null || _ui == null || _ui.detailsPanel == null) return;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _container = new GameObject("CrossRefContainer");
        _container.transform.SetParent(_ui.detailsPanel.transform, false);
        var rt = _container.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 60f);
        rt.anchoredPosition = new Vector2(0f, -116f);

        var vlg = _container.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 2f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;

        _adjacentRow = BuildEmptyRow("AdjacentRow", _container.transform);
        _adjacentPills = _adjacentRow.transform.Find("Pills");
        _relatedRow = BuildEmptyRow("RelatedRow", _container.transform);
        _relatedPills = _relatedRow.transform.Find("Pills");

        _container.SetActive(false);
    }

    GameObject BuildEmptyRow(string name, Transform parent)
    {
        var row = new GameObject(name);
        row.transform.SetParent(parent, false);
        var rt = row.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(720f, 28f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlHeight = false;
        hlg.childControlWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.padding = new RectOffset(16, 16, 0, 0);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(row.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.sizeDelta = new Vector2(120f, 26f);
        var label = labelGO.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 13;
        label.fontStyle = FontStyle.Bold;
        label.color = LabelColor;
        label.alignment = TextAnchor.MiddleLeft;
        label.text = "";

        var pillsGO = new GameObject("Pills");
        pillsGO.transform.SetParent(row.transform, false);
        var prt = pillsGO.AddComponent<RectTransform>();
        prt.sizeDelta = new Vector2(520f, 26f);
        var phlg = pillsGO.AddComponent<HorizontalLayoutGroup>();
        phlg.spacing = 6f;
        phlg.childAlignment = TextAnchor.MiddleLeft;
        phlg.childControlHeight = false;
        phlg.childControlWidth = false;
        phlg.childForceExpandHeight = false;
        phlg.childForceExpandWidth = false;

        return row;
    }

    void BuildRow(GameObject row, Transform pills, string labelText, RegionData[] entries, bool active)
    {
        if (row == null) return;
        row.SetActive(active);
        if (!active) return;

        var label = row.transform.Find("Label")?.GetComponent<Text>();
        if (label != null) label.text = labelText;

        ClearChildren(pills);
        if (entries == null) return;

        bool canNavigate = RegionCrossReferenceNavigation.Instance != null
            && RegionCrossReferenceNavigation.Instance.CanNavigate();

        for (int i = 0; i < entries.Length; i++)
        {
            var data = entries[i];
            if (data == null) continue;
            BuildPill(pills, data, canNavigate);
        }
    }

    void BuildPill(Transform parent, RegionData data, bool interactable)
    {
        var pill = new GameObject($"Pill_{data.regionId}");
        pill.transform.SetParent(parent, false);
        var rt = pill.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(PillWidth, 24f);

        var img = pill.AddComponent<Image>();
        img.color = interactable ? PillBg : PillBgDisabled;

        var btn = pill.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = interactable;
        var captured = data;
        btn.onClick.AddListener(() => OnPillClicked(captured));

        var le = pill.AddComponent<LayoutElement>();
        le.minWidth = PillWidth;
        le.preferredWidth = PillWidth;
        le.flexibleWidth = 0f;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(pill.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(6f, 1f);
        lrt.offsetMax = new Vector2(-6f, -1f);
        var text = labelGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 11;
        text.fontStyle = FontStyle.Bold;
        text.color = PillText;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = TruncateLabel(data.displayName);
    }

    static string TruncateLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return "";
        if (label.Length <= MaxPillLabelCharacters) return label;
        return label.Substring(0, MaxPillLabelCharacters - 1) + "…";
    }

    void OnPillClicked(RegionData data)
    {
        if (data == null) return;
        if (Time.unscaledTime - _lastClickTime < ClickCooldown) return;
        _lastClickTime = Time.unscaledTime;

        if (RegionCrossReferenceNavigation.Instance != null)
            RegionCrossReferenceNavigation.Instance.RequestNavigate(data);
    }

    void ShrinkBody()
    {
        if (_bodyShrunk) return;
        if (_ui == null || _ui.regionDetailedDescriptionTextLegacy == null) return;
        var bodyRT = _ui.regionDetailedDescriptionTextLegacy.rectTransform;
        if (bodyRT == null) return;

        if (!_bodyRectCaptured)
        {
            _bodyOriginalAnchoredPos = bodyRT.anchoredPosition;
            _bodyOriginalSizeDelta = bodyRT.sizeDelta;
            _bodyRectCaptured = true;
        }

        bodyRT.anchoredPosition = new Vector2(_bodyOriginalAnchoredPos.x, _bodyOriginalAnchoredPos.y + 42f);
        bodyRT.sizeDelta = new Vector2(_bodyOriginalSizeDelta.x, Mathf.Max(105f, _bodyOriginalSizeDelta.y - 105f));
        _bodyShrunk = true;
    }

    void RestoreBody()
    {
        if (!_bodyShrunk) return;
        if (_ui == null || _ui.regionDetailedDescriptionTextLegacy == null) return;
        var bodyRT = _ui.regionDetailedDescriptionTextLegacy.rectTransform;
        if (bodyRT == null) return;
        if (!_bodyRectCaptured) return;

        bodyRT.anchoredPosition = _bodyOriginalAnchoredPos;
        bodyRT.sizeDelta = _bodyOriginalSizeDelta;
        _bodyShrunk = false;
    }

    static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }
}
