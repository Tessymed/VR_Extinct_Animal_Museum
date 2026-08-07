#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the Museum Tour Guide Manager and Spatial Audio Tour Zones in the active scene.
/// </summary>
public static class MuseumTourGuideSetup
{
    [MenuItem("Tools/Setup Museum Tour Guide Narration in Scene")]
    public static void SetupTourGuide()
    {
        // 1. Locate or create MuseumTourGuideManager
        var existingManager = Object.FindAnyObjectByType<MuseumTourGuideManager>();
        GameObject managerGO;
        if (existingManager == null)
        {
            managerGO = new GameObject("MuseumTourGuide");
            Undo.RegisterCreatedObjectUndo(managerGO, "Create MuseumTourGuide");
            existingManager = managerGO.AddComponent<MuseumTourGuideManager>();
        }
        else
        {
            managerGO = existingManager.gameObject;
        }

        // Load voiceover clips from Assets/Audio/Voiceover/
        var welcomeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voiceover/guide_01_welcome.wav");
        var dodoClip    = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voiceover/guide_02_dodo.wav");
        var mammothClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voiceover/guide_03_mammoth.wav");
        var thylacineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voiceover/guide_04_thylacine.wav");

        existingManager.welcomeClip = welcomeClip;
        existingManager.startDelay = 1.5f;
        existingManager.narrationVolume = 0.95f;
        EditorUtility.SetDirty(existingManager);

        // 2. Create spatial waypoint triggers if they don't already exist
        var rootZones = GameObject.Find("TourGuideZones");
        if (rootZones == null)
        {
            rootZones = new GameObject("TourGuideZones");
            Undo.RegisterCreatedObjectUndo(rootZones, "Create TourGuideZones");
        }

        // Zone 1: Dodo Approach Zone
        CreateOrUpdateZone(rootZones, "Zone_01_DodoApproach",
                           new Vector3(6.5f, 1.2f, -10.3f), new Vector3(6f, 4f, 8f),
                           "zone_dodo", dodoClip);

        // Zone 2: Mammoth Corridor Zone
        CreateOrUpdateZone(rootZones, "Zone_02_MammothCorridor",
                           new Vector3(0f, 1.2f, 0f), new Vector3(8f, 4f, 8f),
                           "zone_mammoth", mammothClip);

        // Zone 3: Thylacine Gallery Zone
        CreateOrUpdateZone(rootZones, "Zone_03_ThylacineGallery",
                           new Vector3(-4.5f, 1.2f, -8.0f), new Vector3(6f, 4f, 8f),
                           "zone_thylacine", thylacineClip);

        Debug.Log("✅ [MuseumTourGuideSetup] Museum Voiceover Tour Guide and Spatial Trigger Zones created successfully! Save your scene!");
    }

    private static void CreateOrUpdateZone(GameObject parent, string name, Vector3 pos, Vector3 size, string zoneId, AudioClip clip)
    {
        Transform existing = parent.transform.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent.transform, false);
        }
        else
        {
            go = existing.gameObject;
        }

        go.transform.position = pos;

        var col = go.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = go.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;
        col.size = size;

        var tourZone = go.GetComponent<MuseumTourZone>();
        if (tourZone == null)
        {
            tourZone = go.AddComponent<MuseumTourZone>();
        }

        tourZone.zoneId = zoneId;
        tourZone.guideClip = clip;
        tourZone.allowReplay = false;

        EditorUtility.SetDirty(go);
    }
}
#endif
