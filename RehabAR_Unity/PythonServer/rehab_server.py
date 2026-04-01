"""
rehab_server.py  –  Pose detection server for RehabAR Unity app
Fixed rep counting: requires FULL squat cycle (stand -> squat -> stand)
Prevents auto-counting when person is just standing still.

Install:
    pip install flask mediapipe opencv-python numpy

Run:
    python rehab_server.py
"""

from flask import Flask, request, jsonify
import mediapipe as mp
import numpy as np
import base64
import cv2
import re

app = Flask(__name__)

mp_pose    = mp.solutions.pose
pose_model = mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,
    min_detection_confidence=0.65,
    min_tracking_confidence=0.65
)

# ── Rep State Machine ──────────────────────────────────────────────────────────
#
#   STANDING --(knee drops below 130)--> SQUATTING --(knee rises above 160)--> STANDING + rep++
#
#   Rep is ONLY counted when:
#   1. Person is confirmed STANDING (knee > 160 deg)
#   2. Knee DROPS below 130 deg for at least 3 frames (prevents noise)
#   3. Knee RISES back above 160 deg  <-- rep counted here
#
# ──────────────────────────────────────────────────────────────────────────────

STAND_ANGLE      = 160   # knee angle = standing straight
SQUAT_ANGLE      = 130   # knee angle = in squat
MIN_SQUAT_FRAMES = 3     # noise filter: must stay squatted for N frames

rep_count                    = 0
phase                        = "STANDING"
consecutive_frames_squatting = 0


def angle_between(a, b, c):
    """Angle at point B, formed by A-B-C."""
    a, b, c = np.array(a[:2]), np.array(b[:2]), np.array(c[:2])
    ba = a - b
    bc = c - b
    cosine = np.dot(ba, bc) / (np.linalg.norm(ba) * np.linalg.norm(bc) + 1e-9)
    return float(np.degrees(np.arccos(np.clip(cosine, -1.0, 1.0))))


def lm(landmarks, idx):
    p = landmarks[idx]
    return (p.x, p.y, p.z)


def decode_image(data_uri: str):
    b64 = re.sub(r"^data:image/[^;]+;base64,", "", data_uri)
    arr = np.frombuffer(base64.b64decode(b64), dtype=np.uint8)
    return cv2.imdecode(arr, cv2.IMREAD_COLOR)


@app.route("/detect_pose", methods=["POST"])
def detect_pose():
    global rep_count, phase, consecutive_frames_squatting

    data = request.get_json(force=True)
    if not data or "image" not in data:
        return jsonify(error="no image"), 400

    frame = decode_image(data["image"])
    if frame is None:
        return jsonify(error="bad image"), 400

    rgb     = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = pose_model.process(rgb)

    # ── No person in frame ────────────────────────
    if not results.pose_landmarks:
        consecutive_frames_squatting = 0
        return jsonify(
            pose_detected  = False,
            reps           = rep_count,
            accuracy       = 0,
            feedback       = "Stand in front of camera",
            knee_angle     = 0.0,
            shoulder_angle = 0.0,
            ankle_angle    = 0.0,
            phase          = phase
        )

    lms = results.pose_landmarks.landmark

    # ── Joint angles (right-side landmarks) ───────
    # Shoulder: elbow(13) - shoulder(11) - hip(23)
    shoulder_angle = angle_between(lm(lms, 13), lm(lms, 11), lm(lms, 23))
    # Knee: hip(23) - knee(25) - ankle(27)
    knee_angle     = angle_between(lm(lms, 23), lm(lms, 25), lm(lms, 27))
    # Ankle: knee(25) - ankle(27) - foot_index(31)
    ankle_angle    = angle_between(lm(lms, 25), lm(lms, 27), lm(lms, 31))

    # ── Rep State Machine ─────────────────────────
    rep_counted_this_frame = False

    if phase == "STANDING":
        if knee_angle <= SQUAT_ANGLE:
            consecutive_frames_squatting += 1
            if consecutive_frames_squatting >= MIN_SQUAT_FRAMES:
                phase = "SQUATTING"
                consecutive_frames_squatting = 0
        else:
            consecutive_frames_squatting = 0   # reset on any standing frame

    elif phase == "SQUATTING":
        if knee_angle >= STAND_ANGLE:
            rep_count += 1
            phase = "STANDING"
            rep_counted_this_frame = True
            consecutive_frames_squatting = 0

    # ── Form feedback ─────────────────────────────
    accuracy = 100
    feedback = "Good form!"

    if phase == "SQUATTING":
        feedback = "Going down..."
        if knee_angle > SQUAT_ANGLE + 10:
            feedback = "Squat deeper for a full rep"
            accuracy = 75
        elif ankle_angle < 65:
            feedback = "Keep heels on the ground"
            accuracy = 80

    elif phase == "STANDING":
        if rep_counted_this_frame:
            feedback = "Rep " + str(rep_count) + " done! Great work!"
        elif shoulder_angle < 140:
            feedback = "Keep your back straight"
            accuracy = 85

    return jsonify(
        pose_detected  = True,
        reps           = rep_count,
        accuracy       = accuracy,
        feedback       = feedback,
        knee_angle     = round(knee_angle, 1),
        shoulder_angle = round(shoulder_angle, 1),
        ankle_angle    = round(ankle_angle, 1),
        phase          = phase
    )


@app.route("/reset", methods=["POST"])
def reset():
    global rep_count, phase, consecutive_frames_squatting
    rep_count                    = 0
    phase                        = "STANDING"
    consecutive_frames_squatting = 0
    return jsonify(status="reset", reps=0)


if __name__ == "__main__":
    print("=" * 55)
    print("  Rehab Pose Server  ->  http://localhost:5000")
    print("  Rep rule: knee < 130deg (squat) -> knee > 160deg (stand)")
    print("  Reps only count on FULL squat cycle. No auto-counting.")
    print("=" * 55)
    app.run(host="0.0.0.0", port=5000, debug=False)