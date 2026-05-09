using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Custom VR virtual keyboard for input fields.
/// Creates a world-space keyboard panel with letter/number keys, backspace, space, and done.
/// Automatically shows when an InputField is selected (detected via EventSystem polling)
/// and hides when done or when focus leaves an input field.
///
/// This is a self-contained keyboard that does NOT depend on the XRI Spatial Keyboard sample.
/// It works with legacy UI InputField and VR ray interactors.
/// </summary>
public class VRKeyboardManager : MonoBehaviour
{
    [Header("Keyboard Panel (built by editor setup)")]
    public GameObject keyboardPanel;

    [Header("Layout Settings")]
    public float keySize = 40f;
    public float keySpacing = 4f;

    private InputField _activeInputField;
    private bool _isShifted;
    private bool _keyboardBuilt;

    private static readonly string[] Row1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
    private static readonly string[] Row2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
    private static readonly string[] Row3 = { "Z", "X", "C", "V", "B", "N", "M" };
    private static readonly string[] NumberRow = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };

    private void Start()
    {
        if (keyboardPanel != null)
        {
            if (!_keyboardBuilt)
                BuildKeyboard();
            keyboardPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Auto-detect when an InputField gains focus via EventSystem
        var currentSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (currentSelected != null)
        {
            var field = currentSelected.GetComponent<InputField>();
            if (field != null && field != _activeInputField)
            {
                ShowKeyboard(field);
            }
        }
    }

    /// <summary>No-op kept for backward compatibility with editor setup.</summary>
    public void RegisterInputField(InputField field) { }

    public void ShowKeyboard(InputField field)
    {
        _activeInputField = field;
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(true);
            _isShifted = false;
        }
    }

    public void HideKeyboard()
    {
        if (keyboardPanel != null)
            keyboardPanel.SetActive(false);
        _activeInputField = null;
    }

    private void TypeCharacter(string ch)
    {
        if (_activeInputField == null) return;
        string c = _isShifted ? ch.ToUpper() : ch.ToLower();
        _activeInputField.text += c;
        // Reset shift after typing one character
        if (_isShifted) _isShifted = false;
    }

    private void Backspace()
    {
        if (_activeInputField == null) return;
        if (_activeInputField.text.Length > 0)
            _activeInputField.text = _activeInputField.text.Substring(0, _activeInputField.text.Length - 1);
    }

    private void Space()
    {
        if (_activeInputField == null) return;
        _activeInputField.text += " ";
    }

    private void ToggleShift()
    {
        _isShifted = !_isShifted;
    }

    private void Done()
    {
        HideKeyboard();
    }

    /// <summary>Build the keyboard UI inside the keyboardPanel.</summary>
    public void BuildKeyboard()
    {
        if (keyboardPanel == null) return;
        _keyboardBuilt = true;

        // Ensure the panel has a VerticalLayoutGroup or we position manually
        float startY = 120f;
        float rowHeight = keySize + keySpacing;

        // Number row
        CreateKeyRow(NumberRow, startY, 0f);
        startY -= rowHeight;

        // Row 1: QWERTY
        CreateKeyRow(Row1, startY, 0f);
        startY -= rowHeight;

        // Row 2: ASDF
        CreateKeyRow(Row2, startY, keySize * 0.5f);
        startY -= rowHeight;

        // Row 3: ZXCV + Backspace
        float row3Start = keySize * 1f;
        CreateKeyRow(Row3, startY, row3Start);
        // Backspace button at end of row 3
        CreateSpecialKey("DEL", startY, row3Start + Row3.Length * (keySize + keySpacing), keySize * 1.8f, Backspace);
        startY -= rowHeight;

        // Bottom row: Shift, Space, Done
        float bottomX = -180f;
        CreateSpecialKey("SHIFT", startY, bottomX, keySize * 1.8f, ToggleShift);
        bottomX += keySize * 2f + keySpacing;
        CreateSpecialKey("SPACE", startY, bottomX, keySize * 5f, Space);
        bottomX += keySize * 5.2f + keySpacing;
        CreateSpecialKey("DONE", startY, bottomX, keySize * 1.8f, Done);
    }

    private void CreateKeyRow(string[] keys, float yPos, float xOffset)
    {
        float totalWidth = keys.Length * (keySize + keySpacing) - keySpacing;
        float startX = -totalWidth / 2f + xOffset;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            float x = startX + i * (keySize + keySpacing);
            CreateLetterKey(key, yPos, x);
        }
    }

    private void CreateLetterKey(string letter, float y, float x)
    {
        var keyGO = CreateKeyBase(letter, y, x, keySize);
        string captured = letter;
        keyGO.GetComponent<Button>().onClick.AddListener(() => TypeCharacter(captured));
    }

    private void CreateSpecialKey(string label, float y, float x, float width, UnityEngine.Events.UnityAction action)
    {
        var keyGO = CreateKeyBase(label, y, x, width);
        keyGO.GetComponent<Button>().onClick.AddListener(action);
    }

    private GameObject CreateKeyBase(string label, float y, float x, float width)
    {
        var keyGO = new GameObject($"Key_{label}");
        keyGO.transform.SetParent(keyboardPanel.transform, false);

        var rt = keyGO.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, keySize);

        var img = keyGO.AddComponent<Image>();
        img.color = new Color(0.20f, 0.22f, 0.28f, 1f);

        var btn = keyGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.20f, 0.22f, 0.28f, 1f);
        colors.highlightedColor = new Color(0.30f, 0.35f, 0.45f, 1f);
        colors.pressedColor = new Color(0.15f, 0.40f, 0.70f, 1f);
        colors.selectedColor = new Color(0.25f, 0.30f, 0.38f, 1f);
        btn.colors = colors;

        // Label text
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(keyGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var text = textGO.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 18;
        text.color = new Color(0.92f, 0.92f, 0.95f, 1f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return keyGO;
    }
}
