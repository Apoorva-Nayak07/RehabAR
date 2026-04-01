# working_server.py - SMART PARTIAL BODY DETECTION (FULLY FIXED)
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
import base64
import cv2
import numpy as np
import time
import math

app = Flask(__name__, static_folder='Website')
CORS(app)

# Load MediaPipe
HAS_MEDIAPIPE = False
try:
    import mediapipe as mp
    mp_pose = mp.solutions.pose
    mp_drawing = mp.solutions.drawing_utils
    HAS_MEDIAPIPE = True
    print("✅ MediaPipe loaded - SMART PARTIAL BODY DETECTION ACTIVE!")
except Exception as e:
    print(f"⚠️ MediaPipe not available: {e}")
    print("   Install: pip install mediapipe==0.10.9 opencv-python")

if HAS_MEDIAPIPE:
    pose = mp_pose.Pose(
        static_image_mode=False,
        model_complexity=1,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5,
        enable_segmentation=False
    )

# Patient data
class PatientData:
    def __init__(self):
        self.reps = 0
        self.current_exercise = "knee"
        self.last_angle = 0
        # FIX: Replace broken rep_in_progress bool with 3-phase state machine
        # Phases: "idle" → "in_target" → "returning" → back to "idle"
        self.phase = "idle"
        self.last_rep_time = 0
        self.accuracy_history = []

patient = PatientData()

# Exercise configurations with SMART detection
EXERCISE_CONFIG = {
    "knee": {
        "name": "Knee Extension",
        "joint": "knee",
        "landmarks": {"p1": 24, "p2": 26, "p3": 28},  # hip, knee, ankle
        "target_angle": 90,
        "min_angle": 70,
        "max_angle": 110,
        "feedback_bend": "Bend your knee",
        "feedback_straight": "Straighten your leg",
        "perfect_msg": "Perfect knee bend!",
        "icon": "🦵",
        "partial_detection": True
    },
    "shoulder": {
        "name": "Shoulder Raise",
        "joint": "shoulder",
        "landmarks": {"p1": 12, "p2": 14, "p3": 16},  # shoulder, elbow, wrist
        "target_angle": 90,
        "min_angle": 70,
        "max_angle": 110,
        "feedback_bend": "Raise your arm",
        "feedback_straight": "Lower your arm",
        "perfect_msg": "Perfect shoulder raise!",
        "icon": "💪",
        "partial_detection": True
    },
    "ankle": {
        "name": "Ankle Pump",
        "joint": "ankle",
        "landmarks": {"p1": 26, "p2": 28, "p3": 30},  # knee, ankle, foot
        "target_angle": 45,
        "min_angle": 30,
        "max_angle": 60,
        "feedback_bend": "Flex your foot up",
        "feedback_straight": "Point your foot down",
        "perfect_msg": "Perfect ankle pump!",
        "icon": "🦶",
        "partial_detection": True
    }
}

def calculate_angle(landmarks, indices, exercise_type):
    """Calculate angle even with partial body visibility"""
    try:
        p1 = landmarks[indices["p1"]]
        p2 = landmarks[indices["p2"]]
        p3 = landmarks[indices["p3"]]

        visibility_threshold = 0.3
        if p1.visibility < visibility_threshold or p2.visibility < visibility_threshold or p3.visibility < visibility_threshold:
            if exercise_type == "knee" and p2.visibility > 0.3:
                return estimate_knee_angle(landmarks)
            elif exercise_type == "shoulder" and p2.visibility > 0.3:
                return estimate_shoulder_angle(landmarks)
            return None

        a = np.array([p1.x, p1.y])
        b = np.array([p2.x, p2.y])
        c = np.array([p3.x, p3.y])

        ba = a - b
        bc = c - b

        dot = np.dot(ba, bc)
        mag_ba = np.linalg.norm(ba)
        mag_bc = np.linalg.norm(bc)

        if mag_ba == 0 or mag_bc == 0:
            return None

        cos_angle = dot / (mag_ba * mag_bc)
        cos_angle = np.clip(cos_angle, -1, 1)
        angle = np.arccos(cos_angle)
        return np.degrees(angle)
    except:
        return None

def estimate_knee_angle(landmarks):
    """Estimate knee angle when full leg not visible"""
    try:
        knee = landmarks[26]
        if knee.visibility > 0.3:
            estimated_angle = 90 - (knee.y * 100)
            return float(np.clip(estimated_angle, 20, 120))
    except:
        pass
    return None

def estimate_shoulder_angle(landmarks):
    """Estimate shoulder angle when full arm not visible"""
    try:
        shoulder = landmarks[12]
        elbow = landmarks[14]
        if shoulder.visibility > 0.3 and elbow.visibility > 0.3:
            dy = elbow.y - shoulder.y
            estimated_angle = 90 - (dy * 100)
            return float(np.clip(estimated_angle, 20, 160))
    except:
        pass
    return None

def get_landmarks_list(results):
    """Convert ALL 33 MediaPipe landmarks to list for frontend — floats for safe JSON"""
    landmarks = []
    for idx, lm in enumerate(results.pose_landmarks.landmark):
        landmarks.append({
            'x': float(lm.x),
            'y': float(lm.y),
            'z': float(lm.z),
            'visibility': float(lm.visibility),
            'index': idx
        })
    return landmarks  # Always exactly 33 items

def evaluate_rep(angle, exercise):
    """
    FIX: 3-phase state machine replaces the broken rep_in_progress boolean.

    idle      → angle enters target zone → count rep, move to in_target
    in_target → angle leaves target zone → move to returning
    returning → angle clears 15° buffer outside zone → reset to idle

    This correctly handles exits from BOTH sides of the target zone,
    which the old code failed to do (only reset when angle went low).
    """
    global patient

    config = EXERCISE_CONFIG[exercise]
    current_time = time.time()
    in_zone = config["min_angle"] <= angle <= config["max_angle"]

    rep_counted = False

    if patient.phase == "idle":
        if in_zone:
            if current_time - patient.last_rep_time > 0.8:
                patient.reps += 1
                patient.last_rep_time = current_time
                rep_counted = True

                accuracy = 100 - min(100, abs(angle - config["target_angle"]) * 2)
                patient.accuracy_history.append(accuracy)
                if len(patient.accuracy_history) > 10:
                    patient.accuracy_history.pop(0)

            patient.phase = "in_target"

    elif patient.phase == "in_target":
        if not in_zone:
            patient.phase = "returning"

    elif patient.phase == "returning":
        # Must clear a 15° buffer on EITHER side before next rep is counted
        outside_low  = angle < config["min_angle"] - 15
        outside_high = angle > config["max_angle"] + 15
        if outside_low or outside_high:
            patient.phase = "idle"

    avg_accuracy = int(sum(patient.accuracy_history) / len(patient.accuracy_history)) if patient.accuracy_history else 0
    current_accuracy = int(max(0, min(100, 100 - abs(angle - config["target_angle"]) * 2)))
    display_accuracy = avg_accuracy if avg_accuracy > 0 else current_accuracy

    return rep_counted, current_accuracy, display_accuracy

def get_feedback(angle, exercise, rep_counted, partial):
    """Generate feedback message"""
    config = EXERCISE_CONFIG[exercise]
    target = config["target_angle"]

    if rep_counted:
        return f"🎉 REP COUNTED! {config['perfect_msg']} 🎉"

    if partial:
        return f"🎯 {config['name']}: Keep moving! Angle: {int(angle)}°"

    diff = angle - target

    if abs(diff) <= 5:
        return f"✅ PERFECT! {int(angle)}° - Great form! Hold it!"
    elif diff < -20:
        return f"⬆️ {config['feedback_bend']} more! Need {int(abs(diff))}° more. Current: {int(angle)}°"
    elif diff < -10:
        return f"⬆️ Getting closer! {int(angle)}° / {target}° - Keep going!"
    elif diff < 0:
        return f"⬆️ Almost there! {int(angle)}° / {target}° - Just a little more!"
    elif diff <= 15:
        return f"⬇️ Too high! Lower to {target}° (current: {int(angle)}°)"
    else:
        return f"⬇️ {config['feedback_straight']} more! Current: {int(angle)}°, Target: {target}°"

@app.route('/')
def index():
    return "Rehab.AR Server Running! 🚀"

@app.route('/website/<path:path>')
def serve_website(path):
    return send_from_directory('Website', path)

@app.route('/detect_pose', methods=['POST'])
def detect_pose():
    global patient

    try:
        data = request.json
        image_data = data['image'].split(',')[1]
        exercise = data.get('exercise', patient.current_exercise)

        # Reset state cleanly if exercise changed mid-session
        if exercise != patient.current_exercise:
            patient.current_exercise = exercise
            patient.reps = 0
            patient.phase = "idle"
            patient.accuracy_history = []
            patient.last_angle = 0

        image_bytes = base64.b64decode(image_data)
        nparr = np.frombuffer(image_bytes, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

        if frame is None:
            return jsonify({
                'success': False,
                'message': 'Invalid image',
                'reps': patient.reps,
                'accuracy': 0,
                'angle': patient.last_angle
            })

        if HAS_MEDIAPIPE:
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = pose.process(rgb_frame)

            if results.pose_landmarks:
                # FIX: Always extract all 33 landmarks with explicit float cast
                landmarks = get_landmarks_list(results)
                config = EXERCISE_CONFIG[exercise]

                angle = calculate_angle(results.pose_landmarks, config["landmarks"], exercise)

                partial_detection = False
                if angle is None:
                    partial_detection = True
                    if exercise == "knee":
                        angle = estimate_knee_angle(results.pose_landmarks.landmark)
                    elif exercise == "shoulder":
                        angle = estimate_shoulder_angle(results.pose_landmarks.landmark)
                    else:
                        angle = config["target_angle"]

                if angle is not None:
                    rep_counted, current_accuracy, display_accuracy = evaluate_rep(angle, exercise)
                    feedback = get_feedback(angle, exercise, rep_counted, partial_detection)
                    patient.last_angle = angle

                    return jsonify({
                        'success': True,
                        'feedback': feedback,
                        'reps': patient.reps,
                        'accuracy': display_accuracy,
                        'angle': int(angle),
                        'landmarks': landmarks,        # All 33, always
                        'num_landmarks': len(landmarks),
                        'exercise': exercise,
                        'partial': partial_detection,
                        'rep_counted': rep_counted,
                        'phase': patient.phase         # Useful for debugging
                    })
                else:
                    return jsonify({
                        'success': False,
                        'message': f'Show your {config["joint"]} to the camera',
                        'reps': patient.reps,
                        'accuracy': 0,
                        'angle': patient.last_angle,
                        'landmarks': landmarks
                    })
            else:
                return jsonify({
                    'success': False,
                    'message': 'Move your body to show the joint',
                    'reps': patient.reps,
                    'accuracy': 0,
                    'angle': patient.last_angle,
                    'landmarks': []
                })

        else:
            # DEMO MODE - simulates detection without MediaPipe
            t = time.time()
            config = EXERCISE_CONFIG[exercise]

            if exercise == "shoulder":
                simulated_angle = config["target_angle"] + 40 * math.sin(t * 1.5)
            elif exercise == "ankle":
                simulated_angle = config["target_angle"] + 25 * math.sin(t * 1.2)
            else:
                simulated_angle = config["target_angle"] + 40 * math.sin(t * 1.8)

            simulated_angle = float(np.clip(simulated_angle, config["min_angle"] - 20, config["max_angle"] + 20))

            rep_counted, current_accuracy, display_accuracy = evaluate_rep(simulated_angle, exercise)
            feedback = get_feedback(simulated_angle, exercise, rep_counted, False)
            patient.last_angle = simulated_angle

            return jsonify({
                'success': True,
                'feedback': feedback,
                'reps': patient.reps,
                'accuracy': display_accuracy,
                'angle': int(simulated_angle),
                'landmarks': [],
                'exercise': exercise,
                'partial': False,
                'rep_counted': rep_counted,
                'phase': patient.phase
            })

    except Exception as e:
        print(f"Error in detect_pose: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({
            'success': False,
            'message': 'Processing error — check server logs.',
            'reps': patient.reps,
            'accuracy': 0,
            'angle': patient.last_angle
        })

@app.route('/reset_reps', methods=['POST'])
def reset_reps():
    global patient
    patient.reps = 0
    patient.phase = "idle"
    patient.accuracy_history = []
    patient.last_angle = 0
    return jsonify({'success': True})

@app.route('/set_exercise', methods=['POST'])
def set_exercise():
    global patient
    data = request.json
    exercise = data.get('exercise', 'knee')
    patient.current_exercise = exercise
    patient.reps = 0
    patient.phase = "idle"
    patient.accuracy_history = []
    patient.last_angle = 0
    return jsonify({'success': True})

if __name__ == '__main__':
    print("\n" + "="*70)
    print("🏥 REHAB.AR - SMART PARTIAL BODY DETECTION (FULLY FIXED)")
    print("="*70)
    print("\n✅ REP COUNTER FIXED — 3-phase state machine (no more stuck reps!)")
    print("✅ LANDMARKS FIXED  — All 33 sent as proper floats every frame")
    print("✅ Partial body detection still works!")
    print("\n🦵 KNEE:     Bend knee to 90°  → return to start → repeat")
    print("💪 SHOULDER: Raise arm to 90°  → return to start → repeat")
    print("🦶 ANKLE:    Flex foot to 45°  → return to start → repeat")
    print("\n📡 SERVER: http://localhost:5000")
    print("🔴 Keep this window OPEN!")
    print("="*70)

    app.run(host='0.0.0.0', port=5000, debug=False)