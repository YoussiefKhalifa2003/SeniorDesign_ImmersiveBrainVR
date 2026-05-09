using UnityEngine;
using UnityEditor;

/// <summary>
/// Fixes metallic/roughness and adds glass transparency for the operating room.
/// Tools > Brain Dissection > Fix Operating Room Materials
/// </summary>
public static class FixOperatingRoomMaterials
{
    [MenuItem("Tools/Brain Dissection/Fix Operating Room Materials")]
    public static void Fix()
    {
        var opRoom = GameObject.Find("operating_room");
        if (opRoom == null)
        {
            Debug.LogError("[MaterialFix] No 'operating_room' GameObject found in scene.");
            return;
        }

        int fixedCount = 0;
        int glassCount = 0;
        var renderers = opRoom.GetComponentsInChildren<Renderer>(true);

        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            string objName = rend.gameObject.name.ToLower();
            var mats = rend.sharedMaterials;

            foreach (var mat in mats)
            {
                if (mat == null) continue;

                string matName = mat.name.ToLower();
                bool shouldBeMetal = IsMetalSurface(matName);
                bool shouldBeGlass = IsGlassSurface(matName, objName);

                // ---- GLASS: make transparent ----
                if (shouldBeGlass)
                {
                    MakeGlass(mat);
                    glassCount++;
                    Debug.Log($"  Glass: '{mat.name}' on '{rend.gameObject.name}'");
                }
                // ---- GLOW: emissive ceiling panels ----
                else if (matName.Contains("glow"))
                {
                    MakeEmissive(mat);
                    Debug.Log($"  Emissive: '{mat.name}' on '{rend.gameObject.name}'");
                }
                else
                {
                    // ---- Fix Metallic ----
                    if (mat.HasProperty("_Metallic"))
                    {
                        float targetMetal = shouldBeMetal ? 0.75f : 0.0f;
                        mat.SetFloat("_Metallic", targetMetal);
                    }

                    // ---- Fix Smoothness ----
                    if (mat.HasProperty("_Smoothness"))
                        mat.SetFloat("_Smoothness", shouldBeMetal ? 0.65f : GetNonMetalSmoothness(matName));
                    if (mat.HasProperty("_Glossiness"))
                        mat.SetFloat("_Glossiness", shouldBeMetal ? 0.65f : GetNonMetalSmoothness(matName));

                    // ---- Remove misassigned metallic maps ----
                    if (!shouldBeMetal && mat.HasProperty("_MetallicGlossMap"))
                    {
                        if (mat.GetTexture("_MetallicGlossMap") != null)
                        {
                            mat.SetTexture("_MetallicGlossMap", null);
                            Debug.Log($"  Removed metallic map from '{mat.name}'");
                        }
                    }

                    // Ensure opaque
                    if (mat.HasProperty("_Surface"))
                        mat.SetFloat("_Surface", 0);
                }

                EditorUtility.SetDirty(mat);
                fixedCount++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[MaterialFix] Fixed {fixedCount} materials ({glassCount} glass). Ctrl+S to save.");
    }

    // ---- Also provide a tool to list all material names for debugging ----
    [MenuItem("Tools/Brain Dissection/List Operating Room Materials")]
    public static void ListMaterials()
    {
        var opRoom = GameObject.Find("operating_room");
        if (opRoom == null) { Debug.LogError("No operating_room found."); return; }

        var renderers = opRoom.GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.HashSet<string> seen = new();
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            foreach (var mat in rend.sharedMaterials)
            {
                if (mat == null) continue;
                string key = mat.name;
                if (seen.Contains(key)) continue;
                seen.Add(key);

                float metal = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : -1;
                float smooth = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : -1;
                bool hasMMap = mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null;
                Debug.Log($"  MAT: '{mat.name}' | Metal={metal:F2} Smooth={smooth:F2} MetalMap={hasMMap} | On: {rend.gameObject.name}");
            }
        }
        Debug.Log($"[MaterialList] {seen.Count} unique materials listed.");
    }

    // ========================= EMISSIVE (CEILING PANELS) =========================

    /// <summary>
    /// Makes ceiling light panels glow like real fluorescent lights.
    /// </summary>
    static void MakeEmissive(Material mat)
    {
        // Bright white base
        Color panelColor = new Color(0.95f, 0.97f, 1f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", panelColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", panelColor);

        // Enable emission
        mat.EnableKeyword("_EMISSION");
        Color emissive = new Color(2f, 2.1f, 2.2f); // HDR white-blue glow
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", emissive);
        if (mat.HasProperty("_EmissiveColor"))
            mat.SetColor("_EmissiveColor", emissive);

        // Flat, no metallic
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);

        // Make sure emission is flagged
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    // ========================= GLASS SETUP =========================

    /// <summary>
    /// Converts a material to transparent glass in URP.
    /// </summary>
    static void MakeGlass(Material mat)
    {
        // Set surface type to Transparent
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1); // 1 = Transparent

        // Set blend mode
        mat.SetOverrideTag("RenderType", "Transparent");
        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 0);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.renderQueue = 3000;

        // Glass color: slightly tinted, mostly transparent
        Color glassColor = new Color(0.85f, 0.9f, 0.92f, 0.15f); // very transparent, slight blue tint
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", glassColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", glassColor);

        // Glass is smooth and slightly metallic (reflective)
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.1f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.95f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.95f);

        // Remove any metallic map
        if (mat.HasProperty("_MetallicGlossMap"))
            mat.SetTexture("_MetallicGlossMap", null);

        // Remove albedo texture for glass (we want pure transparent)
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", null);
    }

    // ========================= DETECTION =========================

    static bool IsGlassSurface(string matName, string objName)
    {
        // Match by material name
        if (matName.Contains("glass") || matName.Contains("window") ||
            matName.Contains("transparent") || matName.Contains("clear"))
            return true;

        // Match by object name (cabinet glass doors, fridge doors, etc.)
        if (objName.Contains("glass") || objName.Contains("window"))
            return true;

        return false;
    }

    static bool IsMetalSurface(string matName)
    {
        return matName.Contains("metal") ||
               matName.Contains("steel") ||
               matName.Contains("chrome") ||
               matName.Contains("iron") ||
               matName.Contains("alumin") ||
               matName.Contains("stainless") ||
               matName.Contains("faucet") ||
               matName.Contains("tap") ||
               matName.Contains("rail") ||
               matName.Contains("handle") ||
               matName.Contains("hinge");
    }

    static float GetNonMetalSmoothness(string matName)
    {
        // Screens/displays: very glossy
        if (matName.Contains("screen") || matName.Contains("monitor") ||
            matName.Contains("display") || matName.Contains("xray"))
            return 0.9f;

        // Floor tiles: polished hospital floor (shiny!)
        if (matName.Contains("floor"))
            return 0.7f;

        // Wall tiles: semi-glossy ceramic
        if (matName.Contains("tile") || matName.Contains("wall"))
            return 0.5f;

        // Furniture/cabinets: smooth laminate
        if (matName.Contains("furniture") || matName.Contains("cabinet") ||
            matName.Contains("white"))
            return 0.5f;

        // Black surfaces (equipment panels): semi-glossy
        if (matName.Contains("black"))
            return 0.55f;

        // Red (first aid box): matte-ish
        if (matName.Contains("red"))
            return 0.35f;

        // Cloth: matte
        if (matName.Contains("cloth") || matName.Contains("fabric"))
            return 0.1f;

        // Default
        return 0.4f;
    }
}
