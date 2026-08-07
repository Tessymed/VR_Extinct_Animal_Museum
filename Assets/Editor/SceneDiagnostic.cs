using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

[InitializeOnLoad]
public static class SceneDiagnostic
{
    static SceneDiagnostic()
    {
        // Delay execution slightly to ensure scene is fully loaded
        EditorApplication.delayCall += RunDiagnostic;
    }

    public static void RunDiagnostic()
    {
        string reportPath = @"C:\Users\PC\.gemini\antigravity-ide\brain\6bb8383a-84b8-4835-abab-a39809c2ce12\scene_diagnostic.txt";
        
        // Ensure the directory exists
        string dir = Path.GetDirectoryName(reportPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (StreamWriter writer = new StreamWriter(reportPath, false))
        {
            writer.WriteLine("=== Unity Scene Diagnostic Report ===");
            writer.WriteLine($"Timestamp: {System.DateTime.Now}");
            writer.WriteLine($"Active Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            writer.WriteLine();

            // 1. Scan for VRExhibitTrigger
            writer.WriteLine("--- VRExhibitTrigger Components ---");
            var triggers = Object.FindObjectsByType<VRExhibitTrigger>(FindObjectsInactive.Include);
            writer.WriteLine($"Found {triggers.Length} VRExhibitTrigger components:");
            foreach (var t in triggers)
            {
                writer.WriteLine($"- GameObject: '{t.gameObject.name}' (Active: {t.gameObject.activeInHierarchy}, Enabled: {t.enabled})");
                writer.WriteLine($"  Exhibit Panel Ref: '{(t.exhibitPanel != null ? t.exhibitPanel.gameObject.name : "NULL")}'");
                writer.WriteLine($"  Animal Name: '{t.animalData.animalName}'");
                writer.WriteLine($"  Scientific Name: '{t.animalData.scientificName}'");
                writer.WriteLine($"  Status: '{t.animalData.status}'");
                writer.WriteLine($"  Page Count: {(t.animalData.pages != null ? t.animalData.pages.Count : 0)}");
                
                var grabbable = t.GetComponent<VRGrabbable>();
                writer.WriteLine($"  Has VRGrabbable: {grabbable != null}");
                if (grabbable != null)
                {
                    writer.WriteLine($"    VRGrabbable Enabled: {grabbable.enabled}");
                }
                
                var colliders = t.GetComponentsInChildren<Collider>(true);
                writer.WriteLine($"    Colliders count: {colliders.Length}");
                foreach (var col in colliders)
                {
                    writer.WriteLine($"      Collider: '{col.name}' | Type: {col.GetType().Name} | IsTrigger: {col.isTrigger} | Layer: {LayerMask.LayerToName(col.gameObject.layer)}");
                }
                writer.WriteLine();
            }

            // 2. Scan for UIDocuments and Panel controllers
            writer.WriteLine("--- UI Document GameObjects ---");
            var uiDocs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
            writer.WriteLine($"Found {uiDocs.Length} UIDocument components:");
            foreach (var doc in uiDocs)
            {
                writer.WriteLine($"- GameObject: '{doc.gameObject.name}' (Active: {doc.gameObject.activeInHierarchy}, Enabled: {doc.enabled})");
                writer.WriteLine($"  Source Asset: '{(doc.visualTreeAsset != null ? doc.visualTreeAsset.name : "NULL")}'");
                writer.WriteLine($"  Panel Settings: '{(doc.panelSettings != null ? doc.panelSettings.name : "NULL")}'");
                
                var panelUI = doc.GetComponent<VRExhibitPanelUI>();
                writer.WriteLine($"  Has VRExhibitPanelUI: {panelUI != null}");
                if (panelUI != null)
                {
                    writer.WriteLine($"    VRExhibitPanelUI Enabled: {panelUI.enabled}");
                }

                var panelRaycaster = doc.GetComponent<UnityEngine.UIElements.PanelRaycaster>();
                writer.WriteLine($"  Has PanelRaycaster: {panelRaycaster != null}");
                writer.WriteLine();
            }

            // 3. Scan for VRControllerGrab
            writer.WriteLine("--- VRControllerGrab Components ---");
            var grabbers = Object.FindObjectsByType<VRControllerGrab>(FindObjectsInactive.Include);
            writer.WriteLine($"Found {grabbers.Length} VRControllerGrab components:");
            foreach (var g in grabbers)
            {
                writer.WriteLine($"- GameObject: '{g.gameObject.name}' (Active: {g.gameObject.activeInHierarchy}, Enabled: {g.enabled})");
                writer.WriteLine($"  Laser Color: {g.laserColor}");
                writer.WriteLine($"  Show Laser: {g.showLaserPointer}");
                writer.WriteLine();
            }
        }
        Debug.Log($"[Diagnostic] Scene diagnostic report written to {reportPath}");
    }
}
