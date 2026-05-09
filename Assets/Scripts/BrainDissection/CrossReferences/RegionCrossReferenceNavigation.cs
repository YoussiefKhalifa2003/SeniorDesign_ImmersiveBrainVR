using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrator for "click a linked region pill on the info panel" navigation.
///
/// Flow when <see cref="RequestNavigate"/> is called:
///   1. <see cref="CanNavigate"/> gates: never run inside Tutorial / Live
///      Dissection / MCQ assessment so authored flows never get hijacked.
///   2. Resolve the target <see cref="RegionData"/> to a live
///      <see cref="BrainRegion"/> in the scene (reference-equality first,
///      regionId fallback).
///   3. If a region is currently extracted, put it back through the
///      existing <see cref="BrainManager.PutBackRegion"/> path so all
///      dependent state (info panel, voice narration, compare overlay,
///      OnInspectionEnded subscribers) reset cleanly.
///   4. Switch hemisphere if the target lives in the hidden one.
///   5. Blink-highlight the target region until it is selected or times out.
///
/// The orchestrator does NOT auto-open the target's details panel — the
/// agreed UX is "panel closes, brain returns, target lights up so the
/// student can find it." Tap the region with tweezers afterwards to read.
/// </summary>
public class RegionCrossReferenceNavigation : MonoBehaviour
{
    public static RegionCrossReferenceNavigation Instance { get; private set; }

    BrainManager _brain;
    RegionUIController _ui;
    bool _busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject(nameof(RegionCrossReferenceNavigation));
        Instance = go.AddComponent<RegionCrossReferenceNavigation>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// True when navigation is currently allowed. Used by the info-panel
    /// pill UI to disable its buttons in non-Play contexts so a stray VR
    /// raycast can't bypass the gate.
    /// </summary>
    public bool CanNavigate()
    {
        if (SessionData.IsAssessmentMode) return false;

        var live = LiveDissectionManager.Instance;
        if (live != null && live.IsLiveDissectionActive) return false;

        var tut = TutorialManager.Instance;
        if (tut != null && tut.IsTutorialActive) return false;

        if (FindBrain() == null) return false;
        return true;
    }

    /// <summary>
    /// Begin navigation to the given target region. Safe to call multiple
    /// times — subsequent calls while a navigation is already running are
    /// ignored to keep the experience predictable.
    /// </summary>
    public void RequestNavigate(RegionData target)
    {
        if (target == null) return;
        if (_busy) return;
        StartCoroutine(NavigateCoroutine(target));
    }

    IEnumerator NavigateCoroutine(RegionData target)
    {
        _busy = true;
        try
        {
            if (!CanNavigate())
            {
                ShowStatus("Cross-reference navigation isn't available right now.");
                yield break;
            }

            var brain = FindBrain();
            if (brain == null) yield break;

            var br = FindBrainRegion(target);
            if (br == null)
            {
                Debug.LogWarning($"[CrossReference] No BrainRegion found for '{target.displayName}' (id='{target.regionId}').");
                ShowStatus($"Couldn't find {target.displayName} on the current brain.");
                yield break;
            }

            if (brain.IsInspectingRegion)
            {
                brain.PutBackRegion();
                yield return null;
            }

            if (brain.BeginHemisphereSwitchForCrossReference(br))
                yield return new WaitForSeconds(brain.hemiMoveAnimDuration + 0.05f);

            ShowStatus($"Showing: {target.displayName}");
            brain.StartCrossReferenceTargetBlink(br, 5f);
        }
        finally
        {
            _busy = false;
        }
    }

    BrainManager FindBrain()
    {
        if (_brain == null) _brain = FindFirstObjectByType<BrainManager>();
        return _brain;
    }

    void ShowStatus(string message)
    {
        if (_ui == null) _ui = FindFirstObjectByType<RegionUIController>();
        if (_ui != null) _ui.SetStatusMessage(message);
    }

    /// <summary>
    /// Resolve a <see cref="RegionData"/> to its live <see cref="BrainRegion"/>
    /// instance in the scene. Reference equality first; falls back to
    /// <see cref="RegionData.regionId"/> match so cross-refs can survive
    /// asset duplication / re-import edge cases.
    /// </summary>
    public static BrainRegion FindBrainRegion(RegionData data)
    {
        if (data == null) return null;
        var all = FindObjectsByType<BrainRegion>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return null;

        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r != null && r.regionData == data) return r;
        }

        if (!string.IsNullOrEmpty(data.regionId))
        {
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r != null && r.regionData != null && r.regionData.regionId == data.regionId)
                    return r;
            }
        }
        return null;
    }
}
