using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// MediaPipe WebGL Bridge
/// ──────────────────────
/// This MonoBehaviour is the single entry point for all pose data coming
/// from the JavaScript MediaPipe Pose solution running in the browser.
///
/// How it works:
///   1. JavaScript calls SendMessage("MediaPipeBridge", "OnPoseLandmarks", jsonString)
///   2. This script parses the JSON into a flat float array
///   3. It distributes the landmarks to all joint trackers
///
/// JavaScript side (add to your index.html or a separate .js plugin):
/// ─────────────────────────────────────────────────────────────────────
///   const pose = new Pose({ locateFile: ... });
///   pose.onResults(results => {
///     if (!results.poseLandmarks) return;
///     const flat = results.poseLandmarks.flatMap(lm => [lm.x, lm.y, lm.z]);
///     unityInstance.SendMessage('MediaPipeBridge', 'OnPoseLandmarks', JSON.stringify(flat));
///   });
/// ─────────────────────────────────────────────────────────────────────
/// </summary>
public class MediaPipeBridge : MonoBehaviour
{
    [Header("Joint Trackers — assign in Inspector")]
    public KneeJointTracker     kneeTracker;
    public ShoulderJointTracker shoulderTracker;
    public AnkleJointTracker    ankleTracker;

    [Header("Debug")]
    public bool logRawLandmarks = false;

    // Called by JS via SendMessage every frame a pose is detected
    public void OnPoseLandmarks(string jsonFloatArray)
    {
        if (string.IsNullOrEmpty(jsonFloatArray)) return;

        float[] landmarks = ParseLandmarkJson(jsonFloatArray);
        if (landmarks == null) return;

        if (logRawLandmarks)
            Debug.Log($"[MediaPipeBridge] Received {landmarks.Length / 3} landmarks");

        // Route to all active trackers
        kneeTracker?.UpdateLandmarks(landmarks);
        shoulderTracker?.UpdateLandmarks(landmarks);
        ankleTracker?.UpdateLandmarks(landmarks);
    }

    /// <summary>
    /// Parse a JSON array like [0.1, 0.5, 0.0, 0.2, ...] into float[].
    /// Uses a lightweight manual parser to avoid JSON library overhead on WebGL.
    /// </summary>
    private float[] ParseLandmarkJson(string json)
    {
        try
        {
            // Strip brackets
            json = json.Trim();
            if (json.StartsWith("[")) json = json.Substring(1);
            if (json.EndsWith("]"))   json = json.Substring(0, json.Length - 1);

            string[] parts = json.Split(',');
            float[] result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = float.Parse(parts[i].Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);

            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MediaPipeBridge] Failed to parse landmarks: {e.Message}");
            return null;
        }
    }
}