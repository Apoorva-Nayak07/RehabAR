using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;

public class RehabARController : MonoBehaviour
{
    [Header("UI References")]
    public Text repsText;
    public Text feedbackText;
    public RawImage cameraDisplay;

    [Header("Avatar Root")]
    public GameObject avatar;   // drag the Avatar GameObject here in Inspector

    [Header("Settings")]
    public float requestInterval = 0.5f;
    public int targetReps = 10;

    // ── Avatar joint names — must match your Hierarchy exactly ──
    // From your screenshot: Body, Head, LeftArm, RightArm, LeftLeg, RightLeg
    private const string RIGHT_ARM = "RightArm";
    private const string LEFT_ARM = "LeftArm";
    private const string RIGHT_LEG = "RightLeg";
    private const string LEFT_LEG = "LeftLeg";

    private WebCamTexture webcamTexture;
    private string serverURL = "http://localhost:5000/detect_pose";

    // Rep display — driven by SERVER only, never incremented in Unity
    private int lastKnownReps = 0;
    private bool celebrationPlaying = false;

    void Start()
    {
        webcamTexture = new WebCamTexture();
        cameraDisplay.texture = webcamTexture;
        webcamTexture.Play();

        feedbackText.text = "Starting camera...";
        UpdateRepsUI(0);

        StartCoroutine(SendFrameLoop());
    }

    // ─────────────────────────────────────────────
    //  CAMERA LOOP
    // ─────────────────────────────────────────────
    IEnumerator SendFrameLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(requestInterval);

            if (webcamTexture == null || !webcamTexture.isPlaying) continue;

            Texture2D snap = new Texture2D(webcamTexture.width, webcamTexture.height);
            snap.SetPixels(webcamTexture.GetPixels());
            snap.Apply();

            byte[] jpg = snap.EncodeToJPG(75);
            string base64 = System.Convert.ToBase64String(jpg);
            Destroy(snap);

            yield return StartCoroutine(PostToServer(base64));
        }
    }

    IEnumerator PostToServer(string base64Image)
    {
        string json = "{\"image\":\"data:image/jpeg;base64," + base64Image + "\"}";
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(serverURL, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                ProcessResponse(req.downloadHandler.text);
            else
                feedbackText.text = "Connecting to server...";
        }
    }

    // ─────────────────────────────────────────────
    //  PARSE RESPONSE
    // ─────────────────────────────────────────────
    void ProcessResponse(string json)
    {
        try
        {
            bool poseDetected = ExtractBool(json, "pose_detected");

            if (!poseDetected)
            {
                feedbackText.text = "Stand in front of camera";
                return;
            }

            // ── Data from server ──
            int serverReps = ExtractInt(json, "reps");
            int accuracy = ExtractInt(json, "accuracy");
            string feedback = ExtractString(json, "feedback");
            float kneeAngle = ExtractFloat(json, "knee_angle");
            float shoulderAngle = ExtractFloat(json, "shoulder_angle");
            float ankleAngle = ExtractFloat(json, "ankle_angle");

            // ── Update UI from server data ONLY ──
            bool newRepJustCounted = (serverReps > lastKnownReps);
            lastKnownReps = serverReps;

            UpdateRepsUI(serverReps);
            feedbackText.text = feedback + "  |  " + accuracy + "%";

            // ── Celebrate on new rep ──
            if (newRepJustCounted && !celebrationPlaying)
                StartCoroutine(JumpAnimation());

            // ── Drive avatar joints ──
            UpdateAvatarPose(kneeAngle, shoulderAngle, ankleAngle);
        }
        catch (System.Exception e)
        {
            Debug.LogError("ParseError: " + e.Message);
        }
    }

    void UpdateRepsUI(int reps)
    {
        repsText.text = "Reps: " + reps + " / " + targetReps;
        if (reps >= targetReps)
            feedbackText.text = "Goal reached! Excellent work!";
    }

    // ─────────────────────────────────────────────
    //  AVATAR JOINTS
    //  Your hierarchy: Avatar > Body > RightArm / RightLeg / LeftArm / LeftLeg
    // ─────────────────────────────────────────────
    void UpdateAvatarPose(float kneeAngle, float shoulderAngle, float ankleAngle)
    {
        if (avatar == null) return;

        // ── Right Leg (knee) ──
        Transform rightLeg = avatar.transform.Find(RIGHT_LEG);
        if (rightLeg != null)
        {
            // knee angle ~180 = straight, ~90 = deep squat
            float bend = Mathf.Clamp(180f - kneeAngle, 0f, 90f);
            rightLeg.localRotation = Quaternion.Lerp(
                rightLeg.localRotation,
                Quaternion.Euler(bend, 0, 0),
                Time.deltaTime * 8f   // smooth follow
            );
        }

        // ── Left Leg (mirror) ──
        Transform leftLeg = avatar.transform.Find(LEFT_LEG);
        if (leftLeg != null)
        {
            float bend = Mathf.Clamp(180f - kneeAngle, 0f, 90f);
            leftLeg.localRotation = Quaternion.Lerp(
                leftLeg.localRotation,
                Quaternion.Euler(bend, 0, 0),
                Time.deltaTime * 8f
            );
        }

        // ── Right Arm (shoulder) ──
        Transform rightArm = avatar.transform.Find(RIGHT_ARM);
        if (rightArm != null)
        {
            float lift = Mathf.Clamp(180f - shoulderAngle, -45f, 90f);
            rightArm.localRotation = Quaternion.Lerp(
                rightArm.localRotation,
                Quaternion.Euler(lift, 0, 0),
                Time.deltaTime * 8f
            );
        }

        // ── Left Arm (mirror) ──
        Transform leftArm = avatar.transform.Find(LEFT_ARM);
        if (leftArm != null)
        {
            float lift = Mathf.Clamp(180f - shoulderAngle, -45f, 90f);
            leftArm.localRotation = Quaternion.Lerp(
                leftArm.localRotation,
                Quaternion.Euler(lift, 0, 0),
                Time.deltaTime * 8f
            );
        }
    }

    // ─────────────────────────────────────────────
    //  CELEBRATION JUMP
    // ─────────────────────────────────────────────
    IEnumerator JumpAnimation()
    {
        if (avatar == null) yield break;
        celebrationPlaying = true;

        Vector3 origin = avatar.transform.position;
        float duration = 0.35f;
        float height = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float y = origin.y + Mathf.Sin(Mathf.PI * elapsed / duration) * height;
            avatar.transform.position = new Vector3(origin.x, y, origin.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        avatar.transform.position = origin;
        celebrationPlaying = false;
    }

    // ─────────────────────────────────────────────
    //  JSON HELPERS
    // ─────────────────────────────────────────────
    int ExtractInt(string json, string key)
    {
        string search = "\"" + key + "\":";
        int s = json.IndexOf(search); if (s == -1) return 0;
        s += search.Length;
        int e = json.IndexOf(",", s); if (e == -1) e = json.IndexOf("}", s);
        if (int.TryParse(json.Substring(s, e - s).Trim(), out int v)) return v;
        return 0;
    }

    float ExtractFloat(string json, string key)
    {
        string search = "\"" + key + "\":";
        int s = json.IndexOf(search); if (s == -1) return 0f;
        s += search.Length;
        int e = json.IndexOf(",", s); if (e == -1) e = json.IndexOf("}", s);
        if (float.TryParse(json.Substring(s, e - s).Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
        return 0f;
    }

    string ExtractString(string json, string key)
    {
        string search = "\"" + key + "\":\"";
        int s = json.IndexOf(search); if (s == -1) return "";
        s += search.Length;
        int e = json.IndexOf("\"", s);
        return json.Substring(s, e - s);
    }

    bool ExtractBool(string json, string key)
    {
        string search = "\"" + key + "\":";
        int s = json.IndexOf(search); if (s == -1) return false;
        s += search.Length;
        int e = json.IndexOf(",", s); if (e == -1) e = json.IndexOf("}", s);
        return json.Substring(s, e - s).Trim().ToLower() == "true";
    }
}