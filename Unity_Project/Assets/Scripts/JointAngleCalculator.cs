using UnityEngine;

/// <summary>
/// Utility class to calculate angles between three 3D points (joint landmarks).
/// Used by all joint trackers for accurate angle-based rep counting.
/// </summary>
public static class JointAngleCalculator
{
    /// <summary>
    /// Calculates the angle (in degrees) at the middle point B,
    /// formed by the vectors BA and BC.
    /// </summary>
    /// <param name="A">First point (e.g. Hip)</param>
    /// <param name="B">Middle/joint point (e.g. Knee)</param>
    /// <param name="C">Third point (e.g. Ankle)</param>
    /// <returns>Angle in degrees [0, 180]</returns>
    public static float CalculateAngle(Vector3 A, Vector3 B, Vector3 C)
    {
        Vector3 BA = A - B;
        Vector3 BC = C - B;

        float angle = Vector3.Angle(BA, BC);
        return angle;
    }

    /// <summary>
    /// Converts MediaPipe normalized landmark [x,y,z] to a Unity Vector3.
    /// MediaPipe gives x,y in [0,1] screen space; we scale to world space.
    /// </summary>
    public static Vector3 LandmarkToVector3(float x, float y, float z)
    {
        // Flip Y because MediaPipe Y increases downward, Unity Y increases upward
        return new Vector3(x, 1f - y, z);
    }
}