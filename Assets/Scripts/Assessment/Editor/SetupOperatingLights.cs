using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates inactive Spot + Fill lights as children of sm_lights.
/// These lights are ONLY activated at runtime by LiveDissectionManager.
///
/// Menu: Tools > Brain Dissection > Setup Operating Lights
/// </summary>
public static class SetupOperatingLights
{
    const string LightPrefix = "LD_OperatingLight";
    const string FillName    = "LD_OperatingLight_Fill";

    [MenuItem("Tools/Brain Dissection/Setup Operating Lights")]
    static void Run()
    {
        // ==== Find sm_lights ====
        GameObject smLights = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name.Equals("sm_lights", System.StringComparison.OrdinalIgnoreCase))
            {
                smLights = t.gameObject;
                break;
            }
        }

        if (smLights == null)
        {
            EditorUtility.DisplayDialog(
                "Setup Operating Lights",
                "Could not find 'sm_lights' in the scene. " +
                "Make sure the operating room scene is open.",
                "OK");
            return;
        }

        // ==== Remove old LD lights ====
        int removed = RemoveExisting();

        // ==== Collect lamp pivot transforms ====
        var lamps = new System.Collections.Generic.List<Transform>();
        lamps.Add(smLights.transform);
        foreach (Transform child in smLights.transform)
            lamps.Add(child);

        // ==== Create one Spot per lamp pivot (inactive) ====
        int created = 0;
        Vector3 posSum = Vector3.zero;

        foreach (var lamp in lamps)
        {
            var spotGO = new GameObject(LightPrefix + "_Spot_" + lamp.name);
            Undo.RegisterCreatedObjectUndo(spotGO, "Create Operating Light");

            spotGO.transform.SetParent(lamp, false);
            spotGO.transform.localPosition = Vector3.zero;
            spotGO.transform.rotation = Quaternion.LookRotation(Vector3.down);

            var light = spotGO.AddComponent<Light>();
            light.type           = LightType.Spot;
            light.color          = new Color(1f, 0.97f, 0.92f);
            light.intensity      = 500f;
            light.range          = 15f;
            light.spotAngle      = 120f;
            light.innerSpotAngle = 55f;
            light.shadows        = LightShadows.Soft;
            light.shadowStrength = 0.45f;
            light.renderMode     = LightRenderMode.ForcePixel;

            spotGO.SetActive(false);

            posSum += lamp.position;
            created++;
        }

        // ==== Fill / ambient point light (inactive) ====
        if (created > 0)
        {
            Vector3 center  = posSum / created;
            Vector3 fillPos = center + Vector3.down * 2f;

            var fillGO = new GameObject(FillName);
            Undo.RegisterCreatedObjectUndo(fillGO, "Create Operating Fill Light");
            fillGO.transform.position = fillPos;

            var fill = fillGO.AddComponent<Light>();
            fill.type       = LightType.Point;
            fill.color      = new Color(0.92f, 0.96f, 1f);
            fill.intensity  = 150f;
            fill.range      = 12f;
            fill.shadows    = LightShadows.None;
            fill.renderMode = LightRenderMode.ForcePixel;

            fillGO.SetActive(false);
            created++;
        }

        EditorUtility.DisplayDialog(
            "Setup Operating Lights",
            $"Done!\n\n" +
            $"  Removed {removed} old LD lights\n" +
            $"  Created {created} new lights (disabled by default)\n\n" +
            $"Reposition the lights in the Scene view however you like.\n" +
            $"They will ONLY activate inside Live Dissection mode.\n\n" +
            $"SAVE YOUR SCENE (Ctrl+S).",
            "OK");
    }

    static int RemoveExisting()
    {
        int count = 0;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null) continue;
            if (!t.name.StartsWith(LightPrefix)) continue;
            Undo.DestroyObjectImmediate(t.gameObject);
            count++;
        }
        return count;
    }
}
