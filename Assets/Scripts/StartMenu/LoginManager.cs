using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the login panel UI. Reads Name and Age input fields,
/// stores them in SessionData, logs the info, and transitions to the main menu.
///
/// References are wired by the StartMenuSetup editor script.
/// </summary>
public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    public InputField nameInputField;
    public Button submitButton;

    [Header("Manager References")]
    public MenuManager menuManager;

    private void Start()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmit);
    }

    private void OnSubmit()
    {
        string userName = nameInputField != null ? nameInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(userName))
        {
            Debug.LogWarning("[LoginManager] Name field is empty.");
            return;
        }

        SessionData.UserName = userName;

        Debug.Log($"[LoginManager] User logged in: Name={userName}");

        if (menuManager != null)
            menuManager.ShowMainMenu();
    }
}
