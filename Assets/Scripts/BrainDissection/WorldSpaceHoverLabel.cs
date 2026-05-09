using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tooltip-style label fixed above the brain. Shows the name of whatever
/// region the user is hovering — always in the same stable spot, never
/// obstructing the view. Think desktop cursor tooltip.
/// </summary>
public class WorldSpaceHoverLabel : MonoBehaviour
{
    public static WorldSpaceHoverLabel Instance { get; private set; }

    [Header("Placement")]
    [Tooltip("Extra height above the brain's top bounding box edge.")]
    public float aboveBrain = 0.04f;

    [Header("Appearance")]
    public float labelScale = 0.00032f;
    public int fontSize = 24;

    private Text _text;
    private GameObject _labelRoot;
    private Transform _brainRoot;
    private bool _showing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CreateLabel();
        _labelRoot.SetActive(false);
    }

    private void Start()
    {
        var bm = Object.FindFirstObjectByType<BrainManager>();
        if (bm != null && bm.brainRoot != null)
            _brainRoot = bm.brainRoot.transform;
    }

    private void CreateLabel()
    {
        _labelRoot = new GameObject("HoverLabel_WorldSpace");
        _labelRoot.transform.SetParent(transform, false);

        var canvas = _labelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var rt = _labelRoot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(650, 45);
        rt.localScale = Vector3.one * labelScale;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(_labelRoot.transform, false);
        _text = textObj.AddComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _text.fontSize = fontSize;
        _text.color = new Color(0.82f, 0.88f, 0.94f, 0.85f);
        _text.alignment = TextAnchor.MiddleCenter;
        _text.horizontalOverflow = HorizontalWrapMode.Overflow;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (_labelRoot == null || !_showing) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 pos = GetAnchorPosition();
        _labelRoot.transform.position = pos;
        _labelRoot.transform.rotation = Quaternion.LookRotation(
            pos - cam.transform.position);
    }

    private Vector3 GetAnchorPosition()
    {
        if (_brainRoot == null)
            return transform.position + Vector3.up * 0.15f;

        Bounds b = new Bounds(_brainRoot.position, Vector3.zero);
        bool any = false;
        foreach (var r in _brainRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        return new Vector3(b.center.x, b.max.y + aboveBrain, b.center.z);
    }

    public void SetBrainRoot(Transform root)
    {
        _brainRoot = root;
    }

    public void Show(string regionName, Transform target)
    {
        if (_labelRoot == null) return;
        _text.text = regionName;
        _showing = true;
        _labelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (_labelRoot == null) return;
        _showing = false;
        _labelRoot.SetActive(false);
    }
}
