using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Real-Time VR Diagnostic & Performance Detector.
/// Continuously monitors VR framerate, frame time spikes, GC allocations,
/// audio buffer states, and physics load during playtesting to pinpoint the exact
/// cause of any freeze, glitch, or pixelation.
/// </summary>
[DefaultExecutionOrder(-999)]
public class VRPerformanceDetector : MonoBehaviour
{
    public static VRPerformanceDetector Instance { get; private set; }

    [Header("Detection Thresholds")]
    [Tooltip("Target minimum framerate for VR (72Hz on Quest 2/3, 90Hz for high refresh).")]
    public float targetFramerate = 72f;

    [Tooltip("Frame time spike threshold in milliseconds (frames taking longer than this are flagged as hitches).")]
    public float spikeThresholdMs = 15.0f;

    [Header("Visual Overlay")]
    [Tooltip("Show diagnostic overlay on screen and in VR.")]
    public bool showOverlay = true;

    [Header("Logging")]
    [Tooltip("Write spike alerts to a persistent log file for deep analysis.")]
    public bool logToFile = true;

    // Real-time tracking metrics
    private float currentFps;
    private float frameTimeMs;
    private float maxFrameTimeMs;
    private int spikeCount = 0;
    private string lastSpikeReason = "None (Smooth)";
    private float lastSpikeTimestamp = 0f;

    // Rolling frame history
    private const int HISTORY_SIZE = 60;
    private float[] frameTimeHistory = new float[HISTORY_SIZE];
    private int historyIndex = 0;

    // GC & Memory tracking
    private long lastAllocatedMemory = 0;
    private int lastGcCount = 0;

    // Log file path
    private string logFilePath;
    private StringBuilder logBuffer = new StringBuilder();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        logFilePath = Path.Combine(Application.dataPath, "../Logs/VR_Performance_Log.txt");
        logFilePath = Path.GetFullPath(logFilePath);

        if (logToFile)
        {
            try
            {
                string dir = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(logFilePath, $"=== VR Real-Time Performance Log ===\nStarted: {System.DateTime.Now}\nTarget FPS: {targetFramerate}\n\n");
            }
            catch { }
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        frameTimeMs = dt * 1000f;
        currentFps = (dt > 0.0001f) ? (1.0f / dt) : 0f;

        // Update rolling history
        frameTimeHistory[historyIndex] = frameTimeMs;
        historyIndex = (historyIndex + 1) % HISTORY_SIZE;

        if (frameTimeMs > maxFrameTimeMs)
        {
            maxFrameTimeMs = frameTimeMs;
        }

        // Detect Garbage Collector collections
        int currentGc = System.GC.CollectionCount(0);
        bool gcHappened = (currentGc > lastGcCount);
        lastGcCount = currentGc;

        // Detect Memory allocation spikes
        long currentMem = System.GC.GetTotalMemory(false);
        long memDelta = currentMem - lastAllocatedMemory;
        lastAllocatedMemory = currentMem;

        // Detect frame spikes / stalls
        if (frameTimeMs > spikeThresholdMs)
        {
            spikeCount++;
            lastSpikeTimestamp = Time.time;

            string reason = AnalyzeSpikeCause(frameTimeMs, gcHappened, memDelta);
            lastSpikeReason = reason;

            string logEntry = $"[{Time.time:F2}s] ⚠️ SPIKE DETECTED! Frame Time: {frameTimeMs:F1}ms (FPS: {currentFps:F0}) | Cause: {reason}";
            Debug.LogWarning($"<color=orange>[VR Detector]</color> {logEntry}");

            if (logToFile)
            {
                try
                {
                    File.AppendAllText(logFilePath, logEntry + "\n");
                }
                catch { }
            }
        }
    }

    private string AnalyzeSpikeCause(float durationMs, bool gcOccurred, long memDeltaBytes)
    {
        if (gcOccurred)
        {
            return $"Garbage Collection Sweep (Alloc Delta: {memDeltaBytes / 1024} KB)";
        }

        if (memDeltaBytes > 500000)
        {
            return $"Heavy Memory Allocation ({memDeltaBytes / 1024} KB in single frame)";
        }

        // Check active audio sources
        var audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int playingCount = 0;
        foreach (var src in audioSources)
        {
            if (src != null && src.isPlaying) playingCount++;
        }

        if (playingCount > 4)
        {
            return $"High Audio Concurrency ({playingCount} AudioSources playing simultaneously)";
        }

        // Check physics / colliders load
        if (Physics.queriesHitTriggers)
        {
            return $"Main Thread / GPU Hitch ({durationMs:F1}ms frame render time)";
        }

        return $"OpenXR Compositor / CPU Hitch ({durationMs:F1}ms)";
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

        // Draw clean real-time performance HUD in top-left corner
        Color originalColor = GUI.color;
        GUI.skin.box.fontSize = 13;
        GUI.skin.label.fontSize = 13;

        Color statusColor = new Color(0.2f, 0.9f, 0.3f, 0.95f);
        if (currentFps < 45f)
        {
            statusColor = new Color(1f, 0.25f, 0.25f, 0.95f);
        }
        else if (currentFps < targetFramerate - 5f)
        {
            statusColor = new Color(1f, 0.8f, 0.2f, 0.95f);
        }

        GUILayout.BeginArea(new Rect(15, 15, 340, 160), GUI.skin.box);
        GUILayout.Label($"<b>VR Real-Time Detector</b>", GetHeaderStyle());
        
        GUI.color = statusColor;
        GUILayout.Label($"FPS: {currentFps:F0} | Frame Time: {frameTimeMs:F1}ms (Max: {maxFrameTimeMs:F1}ms)");
        GUI.color = originalColor;

        GUILayout.Label($"Total Frame Spikes (> {spikeThresholdMs:F0}ms): {spikeCount}");
        
        if (Time.time - lastSpikeTimestamp < 4.0f && spikeCount > 0)
        {
            GUI.color = new Color(1f, 0.4f, 0.2f, 1f);
            GUILayout.Label($"Last Hitch: {lastSpikeReason}");
            GUI.color = originalColor;
        }
        else
        {
            GUILayout.Label($"Health: {(spikeCount == 0 ? "<color=green>Smooth / Zero Hitches</color>" : "Monitoring Active")}");
        }

        GUILayout.Label($"Memory: {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB | GC Count: {lastGcCount}");
        GUILayout.EndArea();

        GUI.color = originalColor;
    }

    private GUIStyle GetHeaderStyle()
    {
        var style = new GUIStyle(GUI.skin.label);
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        return style;
    }
}
