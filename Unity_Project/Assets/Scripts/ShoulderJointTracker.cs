using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Shoulder Rep Counter — supports Shoulder Raise (lateral/frontal) and Shoulder Rotation.
///
/// MediaPipe landmark indices used:
///   11 = Left Shoulder    12 = Right Shoulder
///   13 = Left Elbow       14 = Right Elbow
///   15 = Left Wrist       16 = Right Wrist
///   23 = Left Hip         24 = Right Hip
///
/// For raises  → angle at shoulder between hip-shoulder-elbow
/// For rotation → angle at elbow between shoulder-elbow-wrist
/// </summary>
public class ShoulderJointTracker : MonoBehaviour
{
    public enum ShoulderExercise { LateralRaise, FrontalRaise, ExternalRotation }
    public enum ArmSide { Left, Right, Both }

    // ── Settings ─────────────────────────────────────────────────────
    [Header("Exercise Settings")]
    public ShoulderExercise exerciseType = ShoulderExercise.LateralRaise;
    public ArmSide armSide = ArmSide.Both;

    // ── Thresholds ───────────────────────────────────────────────────
    [Header("Raise Thresholds (degrees)")]
    [Tooltip("Arm-at-side angle (resting). Hip-Shoulder-Elbow ≈ 20–30°")]
    [Range(10f, 50f)]
    public float raiseRestAngle = 25f;

    [Tooltip("Arm fully raised angle. Hip-Shoulder-Elbow ≈ 150–170°")]
    [Range(120f, 175f)]
    public float raiseTopAngle = 150f;

    [Header("Rotation Thresholds (degrees)")]
    [Tooltip("External rotation start. Shoulder-Elbow-Wrist ≈ 20°")]
    [Range(10f, 50f)]
    public float rotationRestAngle = 25f;

    [Tooltip("External rotation peak. Shoulder-Elbow-Wrist ≈ 90°")]
    [Range(70f, 130f)]
    public float rotationPeakAngle = 90f;

    // ── UI ───────────────────────────────────────────────────────────
    [Header("UI References")]
    public TextMeshProUGUI repCountText;
    public TextMeshProUGUI angleDisplayText;
    public TextMeshProUGUI stateText;
    public Image progressBar;

    // ── Internal ─────────────────────────────────────────────────────
    private RepCounterStateMachine leftCounter;
    private RepCounterStateMachine rightCounter;

    private Vector3 leftShoulder, leftElbow, leftWrist, leftHip;
    private Vector3 rightShoulder, rightElbow, rightWrist, rightHip;
    private bool landmarksReady = false;

    void Start()
    {
        InitCounters();
    }

    void InitCounters()
    {
        bool isRotation = (exerciseType == ShoulderExercise.ExternalRotation);

        float peakAngle = isRotation ? rotationPeakAngle : raiseTopAngle;
        float restAngle = isRotation ? rotationRestAngle : raiseRestAngle;

        // Shoulder raises: angle INCREASES when arm goes up → invertLogic = true
        // Rotations:       angle INCREASES when externally rotating → invertLogic = true
        leftCounter  = new RepCounterStateMachine(peakAngle, restAngle, invertLogic: true);
        rightCounter = new RepCounterStateMachine(peakAngle, restAngle, invertLogic: true);

        leftCounter.OnRepCounted  += count => Debug.Log($"[ShoulderTracker] Left rep: {count}");
        rightCounter.OnRepCounted += count => Debug.Log($"[ShoulderTracker] Right rep: {count}");

        UpdateUI(0, 0f);
    }

    /// <summary>
    /// Feed raw MediaPipe landmarks every frame.
    /// </summary>
    public void UpdateLandmarks(float[] landmarks)
    {
        if (landmarks == null || landmarks.Length < 33 * 3) return;

        leftShoulder = GetLandmark(landmarks, 11);
        rightShoulder= GetLandmark(landmarks, 12);
        leftElbow    = GetLandmark(landmarks, 13);
        rightElbow   = GetLandmark(landmarks, 14);
        leftWrist    = GetLandmark(landmarks, 15);
        rightWrist   = GetLandmark(landmarks, 16);
        leftHip      = GetLandmark(landmarks, 23);
        rightHip     = GetLandmark(landmarks, 24);

        landmarksReady = true;
    }

    void Update()
    {
        if (!landmarksReady) return;

        float leftAngle, rightAngle;

        if (exerciseType == ShoulderExercise.ExternalRotation)
        {
            // Measure angle at elbow: Shoulder → Elbow → Wrist
            leftAngle  = JointAngleCalculator.CalculateAngle(leftShoulder,  leftElbow,  leftWrist);
            rightAngle = JointAngleCalculator.CalculateAngle(rightShoulder, rightElbow, rightWrist);
        }
        else
        {
            // Measure angle at shoulder: Hip → Shoulder → Elbow
            leftAngle  = JointAngleCalculator.CalculateAngle(leftHip,  leftShoulder,  leftElbow);
            rightAngle = JointAngleCalculator.CalculateAngle(rightHip, rightShoulder, rightElbow);
        }

        switch (armSide)
        {
            case ArmSide.Left:
                leftCounter.UpdateAngle(leftAngle);
                UpdateUI(leftCounter.RepCount, leftCounter.SmoothedAngle);
                break;

            case ArmSide.Right:
                rightCounter.UpdateAngle(rightAngle);
                UpdateUI(rightCounter.RepCount, rightCounter.SmoothedAngle);
                break;

            case ArmSide.Both:
                leftCounter.UpdateAngle(leftAngle);
                rightCounter.UpdateAngle(rightAngle);
                int totalReps  = Mathf.Max(leftCounter.RepCount, rightCounter.RepCount);
                float avgAngle = (leftCounter.SmoothedAngle + rightCounter.SmoothedAngle) / 2f;
                UpdateUI(totalReps, avgAngle);
                break;
        }
    }

    void UpdateUI(int reps, float angle)
    {
        if (repCountText)    repCountText.text    = $"Reps: {reps}";
        if (angleDisplayText) angleDisplayText.text = $"Shoulder: {angle:F1}°";

        if (stateText)
        {
            var state = (armSide == ArmSide.Left) ? leftCounter?.CurrentState : rightCounter?.CurrentState;
            stateText.text = $"State: {state}";
        }

        if (progressBar)
        {
            bool isRotation = (exerciseType == ShoulderExercise.ExternalRotation);
            float minA = isRotation ? rotationRestAngle : raiseRestAngle;
            float maxA = isRotation ? rotationPeakAngle : raiseTopAngle;
            progressBar.fillAmount = Mathf.InverseLerp(minA, maxA, angle);
        }
    }

    public void ResetCount()
    {
        leftCounter?.Reset();
        rightCounter?.Reset();
        UpdateUI(0, 0f);
    }

    private Vector3 GetLandmark(float[] lm, int index)
    {
        int i = index * 3;
        return JointAngleCalculator.LandmarkToVector3(lm[i], lm[i + 1], lm[i + 2]);
    }
}