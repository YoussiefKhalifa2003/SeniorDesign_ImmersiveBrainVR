using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// One-click lighting to match the Blender render look.
/// Tools > Brain Dissection > Fix Room Lighting
/// </summary>
public static class LightingSetup
{
    [MenuItem("Tools/Brain Dissection/Fix Room Lighting")]
    public static void FixRoomLighting()
    {
        // ---- 1. Directional Light: soft overhead ----
        var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in allLights)
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 0.5f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.25f; // very subtle shadows for depth
                light.color = new Color(0.95f, 0.97f, 1f);
                light.transform.rotation = Quaternion.Euler(80f, -30f, 0f);
                EditorUtility.SetDirty(light);
                EditorUtility.SetDirty(light.gameObject);
            }
        }

        // ---- 2. Ambient: bright but with room for contrast ----
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.82f, 0.85f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.65f, 0.62f);
        RenderSettings.ambientGroundColor = new Color(0.70f, 0.72f, 0.70f);
        RenderSettings.ambientIntensity = 2.0f;
        RenderSettings.reflectionIntensity = 0.6f; // slight reflections for polished surfaces

        // ---- 3. Fill Lights ----
        GameObject opRoom = GameObject.Find("operating_room");
        Vector3 center = Vector3.zero;
        float ceilY = 3.0f;
        float halfW = 2.5f;
        float halfD = 2.5f;
        float midY = 1.5f;

        if (opRoom != null)
        {
            Bounds b = new Bounds(opRoom.transform.position, Vector3.zero);
            foreach (var r in opRoom.GetComponentsInChildren<Renderer>(true))
                if (r != null) b.Encapsulate(r.bounds);
            center = b.center;
            ceilY = b.max.y - 0.3f;
            midY = b.center.y;
            halfW = b.extents.x * 0.6f;
            halfD = b.extents.z * 0.6f;
            Debug.Log($"[Lighting] Room center={center}, size={b.size}");
        }

        // Clean up old lights
        string[] names = {
            "RoomFill_Center", "RoomFill_Left", "RoomFill_Right",
            "RoomFill_Back", "RoomFill_Front", "RoomFill_Mid",
            "RoomFill_WallL", "RoomFill_WallR", "RoomFill_WallB", "RoomFill_WallF"
        };
        foreach (var n in names)
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        Color coolWhite = new Color(0.95f, 0.97f, 1f);     // ceiling panels
        Color warmWhite = new Color(1f, 0.98f, 0.95f);      // surgical lamp feel

        // Ceiling lights (simulating the bright panel lights visible in Blender)
        CreateFillLight("RoomFill_Center", new Vector3(center.x, ceilY, center.z),
            30f, 4.0f, coolWhite);
        CreateFillLight("RoomFill_Left", new Vector3(center.x - halfW, ceilY, center.z),
            22f, 3.0f, coolWhite);
        CreateFillLight("RoomFill_Right", new Vector3(center.x + halfW, ceilY, center.z),
            22f, 3.0f, coolWhite);
        CreateFillLight("RoomFill_Back", new Vector3(center.x, ceilY, center.z - halfD),
            22f, 3.0f, coolWhite);
        CreateFillLight("RoomFill_Front", new Vector3(center.x, ceilY, center.z + halfD),
            22f, 3.0f, coolWhite);

        // Mid-height fill (simulates light bouncing off floor back onto walls)
        CreateFillLight("RoomFill_Mid", new Vector3(center.x, midY, center.z),
            25f, 2.0f, warmWhite);

        // Wall-level fill lights (pushes light into the teal walls like GI bounce)
        float wallY = midY + 0.5f;
        CreateFillLight("RoomFill_WallL", new Vector3(center.x - halfW * 1.2f, wallY, center.z),
            15f, 1.8f, coolWhite);
        CreateFillLight("RoomFill_WallR", new Vector3(center.x + halfW * 1.2f, wallY, center.z),
            15f, 1.8f, coolWhite);
        CreateFillLight("RoomFill_WallB", new Vector3(center.x, wallY, center.z - halfD * 1.2f),
            15f, 1.8f, coolWhite);
        CreateFillLight("RoomFill_WallF", new Vector3(center.x, wallY, center.z + halfD * 1.2f),
            15f, 1.8f, coolWhite);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[Lighting] Blender-match lighting applied. Ctrl+S to save.");
    }

    static void CreateFillLight(string name, Vector3 position, float range, float intensity, Color color)
    {
        var go = new GameObject(name);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.color = color;
        go.transform.position = position;
        EditorUtility.SetDirty(go);
    }
}
