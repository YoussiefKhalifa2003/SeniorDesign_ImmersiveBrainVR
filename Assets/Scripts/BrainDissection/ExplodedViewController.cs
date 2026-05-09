using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the exploded diagram view for a single cluster at a time.
/// Animates regions outward from their anatomical positions along
/// pre-authored local-space vectors, dims non-cluster renderers,
/// and provides a clean reversal path.
/// </summary>
public class ExplodedViewController : MonoBehaviour
{
    public static ExplodedViewController Instance { get; private set; }

    [Tooltip("Assign the cluster catalog asset (or leave null for auto-populated defaults).")]
    public ExplodedClusterCatalog catalog;

    [Header("Animation")]
    [Range(0.2f, 1.0f)]
    public float animDuration = 0.45f;
    [Range(0.05f, 0.5f)]
    public float dimAlpha = 0.15f;

    bool _exploded;
    int _activeClusterIndex = -1;
    Coroutine _anim;

    // Cached state for reversal
    struct CachedRegion
    {
        public BrainRegion region;
        public Vector3 originalLocalPos;
        public Vector3 targetLocalPos;
    }

    readonly List<CachedRegion> _cachedRegions = new List<CachedRegion>();
    readonly Dictionary<Renderer, float> _dimmedRenderers = new Dictionary<Renderer, float>();

    // UI
    GameObject _panelRoot;
    Button[] _clusterButtons;

    static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    static readonly Color BtnNormal = new Color(0.35f, 0.18f, 0.22f, 1f);
    static readonly Color BtnActive = new Color(0.75f, 0.30f, 0.25f, 1f);
    static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.97f);

    public bool IsExploded => _exploded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ExplodedClusterCatalog>();
            catalog.PopulateDefaults();
            Debug.Log("[ExplodedView] No catalog assigned — using built-in defaults.");
        }
    }

    void Start()
    {
        BuildPanel();
        HidePanel();
    }

    // ========================= PUBLIC API =========================

    /// <summary>Explode the given cluster index. Reverses any current explosion first.</summary>
    public void Explode(int clusterIndex)
    {
        if (catalog == null || clusterIndex < 0 || clusterIndex >= catalog.clusters.Count) return;
        if (_anim != null) StopCoroutine(_anim);

        if (_exploded) CollapseImmediate();

        _activeClusterIndex = clusterIndex;
        var cluster = catalog.clusters[clusterIndex];
        CacheClusterRegions(cluster);
        DimNonCluster();
        _anim = StartCoroutine(AnimateExplode(true));
    }

    /// <summary>Collapse the currently exploded cluster back to anatomical position.</summary>
    public void Collapse()
    {
        if (!_exploded) return;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateExplode(false));
    }

    /// <summary>Toggle: if exploded, collapse; otherwise explode at given index.</summary>
    public void Toggle(int clusterIndex)
    {
        if (_exploded && _activeClusterIndex == clusterIndex) Collapse();
        else Explode(clusterIndex);
    }

    /// <summary>Show the exploded-view cluster selection panel.</summary>
    public void ShowPanel()
    {
        if (_panelRoot == null) return;
        // Hidden in Play mode and in Tutorial mode — exploded diagram is reserved
        // for non-guided lab usage.
        if (SessionData.IsPlayMode || SessionData.IsTutorialMode)
        {
            _panelRoot.SetActive(false);
            return;
        }
        _panelRoot.SetActive(true);
        PositionPanel();
    }

    /// <summary>Hide the exploded-view cluster selection panel.</summary>
    public void HidePanel()
    {
        if (_panelRoot == null) return;
        _panelRoot.SetActive(false);
    }

    // ========================= ANIMATION =========================

    IEnumerator AnimateExplode(bool outward)
    {
        float elapsed = 0f;
        _exploded = outward;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            if (!outward) t = 1f - t;

            foreach (var cr in _cachedRegions)
            {
                if (cr.region == null) continue;
                cr.region.transform.localPosition = Vector3.Lerp(cr.originalLocalPos, cr.targetLocalPos, t);
            }

            yield return null;
        }

        // Snap final
        foreach (var cr in _cachedRegions)
        {
            if (cr.region == null) continue;
            cr.region.transform.localPosition = outward ? cr.targetLocalPos : cr.originalLocalPos;
        }

        if (!outward)
        {
            RestoreDimming();
            _cachedRegions.Clear();
            _activeClusterIndex = -1;
        }

        _anim = null;
        UpdateButtonHighlights();
    }

    void CollapseImmediate()
    {
        foreach (var cr in _cachedRegions)
        {
            if (cr.region == null) continue;
            cr.region.transform.localPosition = cr.originalLocalPos;
        }
        RestoreDimming();
        _cachedRegions.Clear();
        _exploded = false;
        _activeClusterIndex = -1;
    }

    // ========================= REGION CACHING =========================

    void CacheClusterRegions(ExplodedClusterCatalog.Cluster cluster)
    {
        _cachedRegions.Clear();
        var allRegions = FindObjectsByType<BrainRegion>(FindObjectsSortMode.None);
        var regionMap = new Dictionary<string, BrainRegion>(allRegions.Length, System.StringComparer.OrdinalIgnoreCase);

        foreach (var r in allRegions)
        {
            string raw = (r.regionData != null && !string.IsNullOrEmpty(r.regionData.regionId))
                ? r.regionData.regionId : r.gameObject.name;
            if (raw.StartsWith("Allen_", System.StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(6);
            regionMap[raw] = r;
        }

        foreach (var offset in cluster.regions)
        {
            if (!regionMap.TryGetValue(offset.regionKey, out var region)) continue;

            _cachedRegions.Add(new CachedRegion
            {
                region = region,
                originalLocalPos = region.transform.localPosition,
                targetLocalPos = region.transform.localPosition
                    + offset.localExplodeDirection.normalized * offset.maxOffsetMeters
            });
        }
    }

    // ========================= DIMMING =========================

    void DimNonCluster()
    {
        _dimmedRenderers.Clear();

        var clusterSet = new HashSet<BrainRegion>();
        foreach (var cr in _cachedRegions)
        {
            if (cr.region != null) clusterSet.Add(cr.region);
        }

        var allRegions = FindObjectsByType<BrainRegion>(FindObjectsSortMode.None);
        foreach (var r in allRegions)
        {
            if (r == null || clusterSet.Contains(r)) continue;

            foreach (var rend in r.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null) continue;
                foreach (var mat in rend.materials)
                {
                    if (mat == null || !mat.HasProperty("_BaseColor")) continue;
                    Color c = mat.GetColor("_BaseColor");
                    _dimmedRenderers[rend] = c.a;
                    c.a = dimAlpha;
                    mat.SetColor("_BaseColor", c);
                }
            }
        }
    }

    void RestoreDimming()
    {
        foreach (var kv in _dimmedRenderers)
        {
            if (kv.Key == null) continue;
            foreach (var mat in kv.Key.materials)
            {
                if (mat == null || !mat.HasProperty("_BaseColor")) continue;
                Color c = mat.GetColor("_BaseColor");
                c.a = kv.Value;
                mat.SetColor("_BaseColor", c);
            }
        }
        _dimmedRenderers.Clear();
    }

    // ========================= UI PANEL =========================

    void BuildPanel()
    {
        if (catalog == null || catalog.clusters.Count == 0) return;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _panelRoot = new GameObject("ExplodedViewPanel");
        _panelRoot.transform.SetParent(transform, false);

        var canvas = _panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _panelRoot.AddComponent<CanvasScaler>();
        _panelRoot.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var canvasRt = _panelRoot.GetComponent<RectTransform>();
        float height = 80 + catalog.clusters.Count * 55 + 50;
        canvasRt.sizeDelta = new Vector2(400, height);
        canvasRt.localScale = Vector3.one * 0.00065f;

        var bg = new GameObject("BG");
        bg.transform.SetParent(_panelRoot.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        bg.AddComponent<Image>().color = PanelBg;

        float topY = height / 2f - 30f;

        var titleGo = MakeText("Title", _panelRoot.transform, new Vector2(0, topY), new Vector2(380, 28),
            "Exploded Diagram", 17, FontStyle.Bold, TextWhite, font);

        _clusterButtons = new Button[catalog.clusters.Count];
        float btnY = topY - 50f;

        for (int i = 0; i < catalog.clusters.Count; i++)
        {
            var cluster = catalog.clusters[i];
            var btnGo = MakeButton(cluster.clusterName, _panelRoot.transform, new Vector2(0, btnY),
                new Vector2(360, 44), cluster.clusterName, BtnNormal, font);
            _clusterButtons[i] = btnGo.GetComponent<Button>();

            int idx = i;
            _clusterButtons[i].onClick.AddListener(() => Toggle(idx));
            btnY -= 55f;
        }

        // Collapse all button
        var collapseBtn = MakeButton("CollapseAll", _panelRoot.transform, new Vector2(0, btnY - 10f),
            new Vector2(360, 36), "Collapse", new Color(0.4f, 0.4f, 0.4f), font);
        collapseBtn.GetComponent<Button>().onClick.AddListener(Collapse);
    }

    void UpdateButtonHighlights()
    {
        if (_clusterButtons == null) return;
        for (int i = 0; i < _clusterButtons.Length; i++)
        {
            if (_clusterButtons[i] == null) continue;
            bool active = _exploded && _activeClusterIndex == i;
            var colors = _clusterButtons[i].colors;
            colors.normalColor = active ? BtnActive : BtnNormal;
            colors.highlightedColor = active ? BtnActive : new Color(0.45f, 0.25f, 0.30f);
            _clusterButtons[i].colors = colors;

            var img = _clusterButtons[i].GetComponent<Image>();
            if (img != null) img.color = active ? BtnActive : BtnNormal;
        }
    }

    void PositionPanel()
    {
        var cam = Camera.main;
        if (cam == null || _panelRoot == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        _panelRoot.transform.position = cam.transform.position
            + forward * 0.7f
            + cam.transform.right * 0.35f
            + Vector3.up * 0.15f;

        _panelRoot.transform.rotation = Quaternion.LookRotation(
            _panelRoot.transform.position - cam.transform.position);
    }

    // ========================= UI HELPERS =========================

    static GameObject MakeText(string name, Transform parent, Vector2 pos, Vector2 size,
        string content, int fontSize, FontStyle style, Color color, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return go;
    }

    static GameObject MakeButton(string name, Transform parent, Vector2 pos, Vector2 size,
        string label, Color bgColor, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        var t = txtGo.AddComponent<Text>();
        t.text = label;
        t.font = font;
        t.fontSize = 15;
        t.fontStyle = FontStyle.Normal;
        t.color = TextWhite;
        t.alignment = TextAnchor.MiddleCenter;

        return go;
    }
}
