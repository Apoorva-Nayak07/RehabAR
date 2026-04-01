using UnityEngine;
using System;

/// <summary>
/// Generic state-machine rep counter.
/// Prevents random counting by requiring the joint to fully pass
/// both an UP threshold and a DOWN threshold before counting a rep.
///
/// State flow:
///   NEUTRAL → DOWN (angle crosses downThreshold) → UP (angle crosses upThreshold) → rep counted → NEUTRAL
///
/// "Angle" here is the joint angle in degrees (e.g. knee angle).
/// "Down" = contracted/bent position (small angle for knee/elbow, large for shoulder raise).
/// "Up"   = extended/rest position.
/// </summary>
public class RepCounterStateMachine
{
    public enum RepState { Neutral, Down, Up }

    // ── Configuration ──────────────────────────────────────────────
    private float downThreshold;   // angle must fall BELOW this to enter Down state
    private float upThreshold;     // angle must rise ABOVE this to complete a rep
    private bool invertLogic;      // set true when "down" means a LARGER angle (shoulder raise)

    // ── Runtime state ──────────────────────────────────────────────
    public RepState CurrentState { get; private set; } = RepState.Neutral;
    public int RepCount          { get; private set; } = 0;

    // Smoothing
    private float smoothedAngle   = 0f;
    private float smoothingFactor = 0.25f;   // lower = smoother but more lag

    // Events
    public event Action<int> OnRepCounted;   // fires with new rep count
    public event Action<RepState> OnStateChanged;

    /// <summary>
    /// Create a rep counter.
    /// </summary>
    /// <param name="downThreshold">
    ///   Angle threshold for the "contracted" position.
    ///   For knee squat : ~90°  (knee bends to ~90°)
    ///   For shoulder raise: ~150° (arm raises, angle increases)
    ///   For ankle calf raise: ~110° (plantarflexion, angle increases)
    /// </param>
    /// <param name="upThreshold">
    ///   Angle threshold for the "extended / return" position.
    ///   Should have a gap from downThreshold (hysteresis gap) to avoid jitter.
    ///   Knee: ~160°, Shoulder: ~60°, Ankle: ~80°
    /// </param>
    /// <param name="invertLogic">
    ///   false (default) = rep starts when angle DROPS below downThreshold (knee, elbow).
    ///   true            = rep starts when angle RISES above downThreshold (shoulder raise, ankle).
    /// </param>
    public RepCounterStateMachine(float downThreshold, float upThreshold, bool invertLogic = false)
    {
        this.downThreshold = downThreshold;
        this.upThreshold   = upThreshold;
        this.invertLogic   = invertLogic;
        smoothedAngle      = invertLogic ? 0f : 180f; // sensible starting value
    }

    /// <summary>
    /// Feed a new raw joint angle every frame.
    /// </summary>
    public void UpdateAngle(float rawAngle)
    {
        // Exponential moving average to smooth noisy MediaPipe landmarks
        smoothedAngle = Mathf.Lerp(smoothedAngle, rawAngle, smoothingFactor);

        RepState previousState = CurrentState;

        if (!invertLogic)
        {
            // Normal logic: "down" = angle is small (knee bends → angle shrinks)
            switch (CurrentState)
            {
                case RepState.Neutral:
                case RepState.Up:
                    if (smoothedAngle < downThreshold)
                        CurrentState = RepState.Down;
                    break;

                case RepState.Down:
                    if (smoothedAngle > upThreshold)
                    {
                        CurrentState = RepState.Up;
                        RepCount++;
                        OnRepCounted?.Invoke(RepCount);
                    }
                    break;
            }
        }
        else
        {
            // Inverted logic: "down" = angle is LARGE (shoulder raises → angle grows)
            switch (CurrentState)
            {
                case RepState.Neutral:
                case RepState.Up:
                    if (smoothedAngle > downThreshold)
                        CurrentState = RepState.Down;
                    break;

                case RepState.Down:
                    if (smoothedAngle < upThreshold)
                    {
                        CurrentState = RepState.Up;
                        RepCount++;
                        OnRepCounted?.Invoke(RepCount);
                    }
                    break;
            }
        }

        if (CurrentState != previousState)
            OnStateChanged?.Invoke(CurrentState);
    }

    public void Reset()
    {
        RepCount     = 0;
        CurrentState = RepState.Neutral;
        smoothedAngle = invertLogic ? 0f : 180f;
    }

    public float SmoothedAngle => smoothedAngle;
}