using System.Text;

/// <summary>
/// Builds the spoken script for a given <see cref="RegionData"/>.
/// Mirrors the body-selection rules used by
/// <see cref="RegionUIController.ShowRegionDetails"/> so the voice narration
/// always reads what would be displayed in the info panel.
///
/// Output structure (in order):
///   1. Optional intro: "Here is the description for {displayName}."
///   2. Subtitle: "Brain region: {displayName}." (only if no intro is included,
///      otherwise omitted to avoid the listener hearing the name three times).
///   3. Body: detailedDescription if non-empty, else shortDescription.
///
/// All work is pure / side-effect-free so this can be unit tested without Unity.
/// </summary>
public static class RegionNarrationScriptBuilder
{
    /// <summary>
    /// Maximum number of characters of body text to read aloud. SAPI/Google TTS
    /// happily speak much longer strings, but a hard cap keeps voice playback
    /// from blocking the user for over ~20 seconds. The cut is performed at
    /// the last sentence boundary that fits.
    /// </summary>
    public const int MaxBodyCharacters = 1200;

    public static string Build(RegionData data, bool includeIntro = true)
    {
        if (data == null) return string.Empty;

        var sb = new StringBuilder();
        string name = string.IsNullOrWhiteSpace(data.displayName) ? "this region" : data.displayName.Trim();

        if (includeIntro)
        {
            sb.Append("Here is the description for ");
            sb.Append(name);
            sb.Append(". ");
        }
        else
        {
            sb.Append("Brain region: ");
            sb.Append(name);
            sb.Append(". ");
        }

        string body = !string.IsNullOrWhiteSpace(data.detailedDescription)
            ? data.detailedDescription
            : data.shortDescription;

        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.Append(NormaliseForSpeech(TruncateAtSentence(body.Trim(), MaxBodyCharacters)));
        }
        else
        {
            sb.Append("No description has been authored for this region yet.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Short fallback message spoken when the user triggers the button while
    /// no region is currently extracted/inspected.
    /// </summary>
    public static string BuildNoRegionMessage()
    {
        return "Please extract a region with the tweezers before asking for a description.";
    }

    /// <summary>
    /// Spoken when speech recognition returns nothing intelligible (no
    /// audio detected, or only background noise).
    /// </summary>
    public static string BuildDidNotCatchMessage()
    {
        return "Sorry, I didn't catch that. Please try again.";
    }

    /// <summary>
    /// Spoken when the system clearly heard the user speak but the words
    /// did not match any of the registered question phrases. Tells the
    /// learner to rephrase rather than just falling silent.
    /// </summary>
    public static string BuildUnrecognisedQuestionMessage()
    {
        return "Sorry, I didn't quite get that. Try asking, what is this region, or what does this region do.";
    }

    /// <summary>
    /// Truncate at the latest sentence boundary that is at or below maxChars.
    /// If no sentence boundary fits, returns the first maxChars characters
    /// and an ellipsis is appended so listeners hear the cutoff.
    /// </summary>
    static string TruncateAtSentence(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;

        int cut = -1;
        int searchEnd = System.Math.Min(text.Length - 1, maxChars);
        for (int i = searchEnd; i >= 0; i--)
        {
            char c = text[i];
            if (c == '.' || c == '!' || c == '?')
            {
                cut = i + 1;
                break;
            }
        }

        if (cut <= 0) return text.Substring(0, maxChars) + "…";
        return text.Substring(0, cut);
    }

    /// <summary>
    /// Lightly normalise text for spoken delivery: collapse double newlines
    /// (which our panel uses for paragraph breaks) into a sentence pause, and
    /// strip leftover rich-text tags so they aren't read literally by SAPI.
    /// </summary>
    static string NormaliseForSpeech(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string s = text.Replace("\r", "");
        s = s.Replace("\n\n", ". ");
        s = s.Replace("\n", " ");

        var sb = new StringBuilder(s.Length);
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }

        return sb.ToString();
    }
}
