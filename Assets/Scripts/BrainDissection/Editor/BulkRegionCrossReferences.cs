using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu that bakes the curated cross-reference table from
/// <see cref="RegionCrossReferenceData"/> onto every <see cref="RegionData"/>
/// asset's <c>adjacentRegions</c> / <c>relatedRegions</c> arrays. This is
/// optional — the cross-reference panel resolves the same data at runtime
/// if the asset arrays are empty — but baking is useful when you want the
/// references to show up in the Inspector or to be diffable in version
/// control.
///
/// Re-runnable: existing arrays are overwritten so the dataset stays
/// consistent across the whole project. Unresolved cross-references are
/// logged as warnings so authoring mistakes are easy to spot.
///
/// Run via: Tools > Brain Dissection > Bulk Fill Region Cross-References.
/// </summary>
public static class BulkRegionCrossReferences
{
    [MenuItem("Tools/Brain Dissection/Bulk Fill Region Cross-References")]
    public static void Run()
    {
        var allAssets = AssetDatabase.FindAssets("t:RegionData")
            .Select(g => AssetDatabase.LoadAssetAtPath<RegionData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .ToArray();

        var byId = new Dictionary<string, RegionData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var a in allAssets)
            if (!string.IsNullOrEmpty(a.regionId) && !byId.ContainsKey(a.regionId))
                byId[a.regionId] = a;

        int updated = 0;
        int missingEntry = 0;
        var noEntry = new SortedSet<string>();
        int unresolvedTargets = 0;

        foreach (var data in allAssets)
        {
            string baseKey = RegionCrossReferenceData.StripPrefixAndHemisphere(data.regionId, out string hemiSuffix);
            if (string.IsNullOrEmpty(baseKey) || string.IsNullOrEmpty(hemiSuffix)) continue;

            var entryNullable = RegionCrossReferenceData.GetEntry(baseKey);
            if (!entryNullable.HasValue)
            {
                missingEntry++;
                noEntry.Add(baseKey);
                continue;
            }

            var entry = entryNullable.Value;
            data.adjacentRegions = ResolveKeys(entry.adjacent, hemiSuffix, byId, data.regionId, ref unresolvedTargets);
            data.relatedRegions = ResolveKeys(entry.related, hemiSuffix, byId, data.regionId, ref unresolvedTargets);
            EditorUtility.SetDirty(data);
            updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BulkRegionCrossReferences] Updated {updated} regions. {missingEntry} regions had no curated entry. {unresolvedTargets} individual cross-refs could not be resolved (warnings above).");
        if (noEntry.Count > 0)
            Debug.Log($"[BulkRegionCrossReferences] Regions with no curated entry:\n  {string.Join("\n  ", noEntry)}");
    }

    static RegionData[] ResolveKeys(string[] keys, string hemiSuffix,
        Dictionary<string, RegionData> byId, string sourceId, ref int unresolvedCount)
    {
        if (keys == null || keys.Length == 0) return new RegionData[0];

        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var result = new List<RegionData>();
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            string targetId = "Allen_" + key + "_" + hemiSuffix;
            if (string.Equals(targetId, sourceId, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(targetId)) continue;

            if (byId.TryGetValue(targetId, out var target))
            {
                result.Add(target);
            }
            else
            {
                unresolvedCount++;
                Debug.LogWarning($"[BulkRegionCrossReferences] Unresolved cross-ref '{targetId}' from '{sourceId}'.");
            }
        }
        return result.ToArray();
    }
}
