using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Ankle Rep Counter — supports Calf Raise (plantarflexion) and Dorsiflexion exercises.
///
/// MediaPipe landmark indices used:
///   25 = Left Knee     26 = Right Knee
///   27 = Left Ankle    28 = Right Ankle
///   31 = Left Foot Index (toe)   32 = Right Foot Index (toe)
///
/// Angle measured at ankle: Knee → Ankle → Toe
///
/// Calf Raise:    ankle angle INCREASES (plantarflexion: toe points down)
/// Dorsiflexion:  ankle angle DECREASES (toe pulls up toward shin)
/// </summary>
public class AnkleJointTracker : MonoBehaviour
{
    public enum AnkleExercise { CalfRaise, Dorsiflexion }
    public enum FootSide { Left, Right, Both }

    // ── Settings ─────────────────────────────────────────────────────
    [Header("Exercise Settings")]
    public AnkleExercise exerciseType = AnkleExercise.CalfRaise;
    public FootSide footSide = FootSide.Both;

    // ── Thresholds ───────────────────────────────────────────────────
    [Header("Calf Raise Thresholds (degrees)")]
    [Tooltip("Flat foot angle. Knee-Ankle-Toe ≈ 90°")]
    [Range(70f, 100f)]
    public float calfRaiseFlatAngle = 90f;

    [Tooltip("Raised heel angle. Knee-Ankle-Toe ≈ 120-140°")]
    [Range(110f, 150f)]
    public float calfRaisePeakAngle = 125f;

    [Header("Dorsiflexion Thresholds (degrees)")]
    [Tooltip("Neutral ankle angle. Knee-Ankle-Toe ≈ 90°")]
    [Range(70f, 100f)]
    public float dorsiFlatAngle = 90f;

    [Tooltip("Dorsiflexed angle (toe pulled up). Knee-Ankle-Toe ≈ 60-70°")]
    [Range(40f, 75f)]
    public float dorsiPeakAngle = 65f;

    // ── UI ───────────────────────────────────────────────────────────
    [Header("UI References")]
    public TextMeshProUGUI repCountText;
    public TextMeshProUGUI angleDisplayText;
    public TextMeshProUGUI stateText;
    public Image progressBar;

    // ── Internal ─────────────────────────────────────────────────────
    private RepCounterStateMachine leftCounter;
    private RepCounterStateMachine rightCounter;

    private Vector3 leftKnee,  leftAnkle,  leftToe;
    private Vector3 rightKnee, rightAnkle, rightToe;
    private bool landmarksReady = false;

    void Start()
    {
        InitCounters();
    }

    void InitCounters()
    {
        if (exerciseType == AnkleExercise.CalfRaise)
        {
            // Calf raise: angle INCREASES → invertLogic = true
            leftCounter  = new RepCounterStateMachine(calfRaisePeakAngle, calfRaiseFlatAngle, invertLogic: true);
            rightCounter = new RepCounterStateMachine(calfRaisePeakAngle, calfRaiseFlatAngle, invertLogic: true);
        }
        else
        {
            // Dorsiflexion: angle DECREASES → invertLogic = false
            leftCounter  = new RepCounterStateMachine(dorsiPeakAngle, dorsiFlatAngle, invertLogic: false);
            rightCounter = new RepCounterStateMachine(dorsiPeakAngle, dorsiFlatAngle, invertLogic: false);
        }

        leftCounter.OnRepCounted  += count => Debug.Log($"[AnkleTracker] Left rep: {count}");
        rightCounter.OnRepCounted += count => Debug.Log($"[AnkleTracker] Right rep: {count}");

        UpdateUI(0, 90f);
    }

    /// <summary>
    /// Feed raw MediaPipe landmarks every frame.
    /// Landmark array format: [x0,y0,z0, x1,y1,z1, ... x32,y32,z32]
    /// </summary>
    public void UpdateLandmarks(float[] landmarks)
    {
        if (landmarks == null || landmarks.Length < 33 * 3) return;

        leftKnee  = GetLandmark(landmarks, 25);
        rightKnee = GetLandmark(landmarks, 26);

        leftAnkle  = GetLandmark(landmarks, 27);
        rightAnkle = GetLandmark(landmarks, 28);

        // Foot index landmarks (toes)
        leftToe  = GetLandmark(landmarks, 31);
        rightToe = GetLandmark(landmarks, 32);

        landmarksReady = true;
    }

    void Update()
    {
        if (!landmarksReady) return;

        // Angle at ankle: Knee → Ankle → Toe
        float leftAngle  = JointAngleCalculator.CalculateAngle(leftKnee,  leftAnkle,  leftToe);
        float rightAngle = JointAngleCalculator.CalculateAngle(rightKnee, rightAnkle, rightToe);

        switch (footSide)
        {
            case FootSide.Left:
                leftCounter.UpdateAngle(leftAngle);
                UpdateUI(leftCounter.RepCount, leftCounter.SmoothedAngle);
                break;

            case FootSide.Right:
                rightCounter.UpdateAngle(rightAngle);
                UpdateUI(rightCounter.RepCount, rightCounter.SmoothedAngle);
                break;

            case FootSide.Both:
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
        if (repCountText)     repCountText.text     = $"Reps: {reps}";
        if (angleDisplayText) angleDisplayText.text = $"Ankle: {angle:F1}°";

        if (stateText)
        {
            var state = (footSide == FootSide.Left) ? leftCounter?.CurrentState : rightCounter?.CurrentState;
            stateText.text = $"State: {state}";
        }

        if (progressBar)
        {
            bool isCalf = (exerciseType == AnkleExercise.CalfRaise);
            float minA  = isCalf ? calfRaiseFlatAngle : dorsiPeakAngle;
            float maxA  = isCalf ? calfRaisePeakAngle : dorsiFlatAngle;
            progressBar.fillAmount = Mathf.InverseLerp(minA, maxA, angle);
        }
    }

    public void ResetCount()
    {
        leftCounter?.Reset();
        rightCounter?.Reset();
        UpdateUI(0, 90f);
    }

    private Vector3 GetLandmark(float[] lm, int index)
    {
        int i = index * 3;
        return JointAngleCalculator.LandmarkToVector3(lm[i], lm[i + 1], lm[i + 2]);
    }
}