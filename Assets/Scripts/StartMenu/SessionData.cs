/// <summary>
/// Simple static data holder for the current user session.
/// Persists across script references without needing a MonoBehaviour.
/// </summary>
public static class SessionData
{
    public static string UserName { get; set; } = "";
    public static bool IsPlayMode { get; set; }
    public static bool IsTutorialMode { get; set; }
    public static bool IsAssessmentMode { get; set; }
}
