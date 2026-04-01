using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Knee Rep Counter — supports Squat and Leg Curl exercises.
///
/// MediaPipe landmark indices used:
///   23 = Left Hip      24 = Right Hip
///   25 = Left Knee     26 = Right Knee
///   27 = Left Ankle    28 = Right Ankle
///
/// Attach this script to a GameObject in your scene.
/// Wire up the UI references in the Inspector.
/// Call UpdateLandmarks() every frame from your MediaPipe bridge.
/// </summary>
public class KneeJointTracker : MonoBehaviour
{
    // ── Exercise type ───────────────────────────────────────────────
    public enum KneeExercise { Squat, LegCurl }

    [Header("Exercise Settings")]
    [Tooltip("Choose between Squat (hip-knee-ankle) or Leg Curl (thigh-knee-shin)")]
    public KneeExercise exerciseType = KneeExercise.Squat;

    [Tooltip("Track left leg, right leg, or average both")]
    public LegSide legSide = LegSide.Both;
    public enum LegSide { Left, Right, Both }

    // ── Thresholds (degrees) ────────────────────────────────────────
    [Header("Squat Thresholds (degrees)")]
    [Range(60f, 120f)]
    [Tooltip("Knee angle when fully squatted. Typically ~90°")]
    public float squatDownAngle = 90f;

    [Range(140f, 175f)]
    [Tooltip("Knee angle when standing straight. Typically ~160°")]
    public float squatUpAngle = 160f;

    [Header("Leg Curl Thresholds (degrees)")]
    [Range(30f, 80f)]
    public float legCurlDownAngle = 45f;
    [Range(140f, 175f)]
    public float legCurlUpAngle = 160f;

    // ── UI References ────────────────────────────────────────────────
    [Header("UI")]
    public TextMeshProUGUI repCountText;
    public TextMeshProUGUI angleDisplayText;
    public TextMeshProUGUI stateText;
    public Image progressArc;   // optional: fill amount shows squat depth

    // ── Internal ─────────────────────────────────────────────────────
    private RepCounterStateMachine leftCounter;
    private RepCounterStateMachine rightCounter;

    // Latest landmarks from MediaPipe (set via UpdateLandmarks)
    private Vector3 leftHip, leftKnee, leftAnkle;
    private Vector3 rightHip, rightKnee, rightAnkle;
    private bool landmarksReady = false;

    void Start()
    {
        InitCounters();
    }

    void InitCounters()
    {
        float downAngle = (exerciseType == KneeExercise.Squat) ? squatDownAngle : legCurlDownAngle;
        float upAngle   = (exerciseType == KneeExercise.Squat) ? squatUpAngle   : legCurlUpAngle;

        // Knee angle DECREASES when bending → invertLogic = false
        leftCounter  = new RepCounterStateMachine(downAngle, upAngle, invertLogic: false);
        rightCounter = new RepCounterStateMachine(downAngle, upAngle, invertLogic: false);

        leftCounter.OnRepCounted  += OnRepCounted;
        rightCounter.OnRepCounted += OnRepCounted;

        UpdateUI(0, 180f);
    }

    /// <summary>
    /// Call this every frame from your MediaPipe WebGL bridge.
    /// Pass all 33 landmarks as flat float arrays [x0,y0,z0, x1,y1,z1, ...].
    /// </summary>
    public void UpdateLandmarks(float[] landmarks)
    {
        if (landmarks == null || landmarks.Length < 33 * 3) return;

        leftHip   = GetLandmark(landmarks, 23);
        leftKnee  = GetLandmark(landmarks, 25);
        leftAnkle = GetLandmark(landmarks, 27);

        rightHip   = GetLandmark(landmarks, 24);
        rightKnee  = GetLandmark(landmarks, 26);
        rightAnkle = GetLandmark(landmarks, 28);

        landmarksReady = true;
    }

    void Update()
    {
        if (!landmarksReady) return;

        float leftAngle  = JointAngleCalculator.CalculateAngle(leftHip,  leftKnee,  leftAnkle);
        float rightAngle = JointAngleCalculator.CalculateAngle(rightHip, rightKnee, rightAnkle);

        switch (legSide)
        {
            case LegSide.Left:
                leftCounter.UpdateAngle(leftAngle);
                UpdateUI(leftCounter.RepCount, leftCounter.SmoothedAngle);
                break;

            case LegSide.Right:
                rightCounter.UpdateAngle(rightAngle);
                UpdateUI(rightCounter.RepCount, rightCounter.SmoothedAngle);
                break;

            case LegSide.Both:
                leftCounter.UpdateAngle(leftAngle);
                rightCounter.UpdateAngle(rightAngle);
                // Show the average, count from either side
                float avgAngle = (leftCounter.SmoothedAngle + rightCounter.SmoothedAngle) / 2f;
                int totalReps  = Mathf.Max(leftCounter.RepCount, rightCounter.RepCount);
                UpdateUI(totalReps, avgAngle);
                break;
        }
    }

    void OnRepCounted(int count)
    {
        Debug.Log($"[KneeTracker] Rep counted! Total: {count}");
    }

    void UpdateUI(int reps, float angle)
    {
        if (repCountText)   repCountText.text   = $"Reps: {reps}";
        if (angleDisplayText) angleDisplayText.text = $"Knee: {angle:F1}°";
        if (stateText)
        {
            var state = (legSide == LegSide.Left) ? leftCounter.CurrentState : rightCounter.CurrentState;
            stateText.text = $"State: {state}";
        }

        // Progress arc shows how deep the squat is
        if (progressArc)
        {
            float minA = (exerciseType == KneeExercise.Squat) ? squatDownAngle : legCurlDownAngle;
            float maxA = (exerciseType == KneeExercise.Squat) ? squatUpAngle   : legCurlUpAngle;
            progressArc.fillAmount = 1f - Mathf.InverseLerp(minA, maxA, angle);
        }
    }

    public void ResetCount()
    {
        leftCounter?.Reset();
        rightCounter?.Reset();
        UpdateUI(0, 180f);
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private Vector3 GetLandmark(float[] lm, int index)
    {
        int i = index * 3;
        return JointAngleCalculator.LandmarkToVector3(lm[i], lm[i + 1], lm[i + 2]);
    }
}