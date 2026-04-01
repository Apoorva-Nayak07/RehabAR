# pose_server.py - FIXED VERSION (COPY EVERYTHING)
import cv2
import numpy as np
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
import mediapipe as mp
import base64
import os
import time

import cv2
import mediapipe as mp

# Initialize MediaPipe Pose
mp_pose = mp.solutions.pose
pose = mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,  # 0=Lite, 1=Full, 2=Heavy
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)

# Start camera
cap = cv2.VideoCapture(0)

while cap.isOpened():
    success, frame = cap.read()
    if not success:
        break
    
    # Convert to RGB
    rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = pose.process(rgb_frame)
    
    if results.pose_landmarks:
        # Get all 33 landmarks
        landmarks = results.pose_landmarks.landmark
        
        # Get specific landmark (e.g., Right Knee - index 26)
        right_knee = landmarks[26]
        print(f"Knee position: x={right_knee.x}, y={right_knee.y}, z={right_knee.z}")
    
    cv2.imshow('Pose Detection', frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
app = Flask(__name__, static_folder='Website')
CORS(app)  # Allow connections from Unity

# Initialize MediaPipe Pose (CORRECTED IMPORT)
mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles

# Initialize pose detector
pose = mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,  # 0=Lite, 1=Full, 2=Heavy (use 1 for balance)
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)

# Store patient progress
patient_data = {
    'reps': 0,
    'accuracy': [],
    'current_exercise': 'knee_extension',
    'last_angle': 0,
    'rep_in_progress': False
}

@app.route('/')
def index():
    return "Rehab.AR AI Server is Running! 🚀"

@app.route('/website/<path:path>')
def serve_website(path):
    return send_from_directory('Website', path)

@app.route('/detect_pose', methods=['POST'])
def detect_pose():
    try:
        # Get image from Unity/browser
        data = request.json
        image_data = data['image'].split(',')[1]
        
        # Convert base64 to image
        image_bytes = base64.b64decode(image_data)
        nparr = np.frombuffer(image_bytes, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        # Convert BGR to RGB for MediaPipe
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        
        # Process with MediaPipe
        results = pose.process(rgb_frame)
        
        if results.pose_landmarks:
            # Get landmarks
            landmarks = []
            for idx, lm in enumerate(results.pose_landmarks.landmark):
                landmarks.append({
                    'x': lm.x,
                    'y': lm.y,
                    'z': lm.z,
                    'visibility': lm.visibility,
                    'index': idx
                })
            
            # Calculate angles
            angles = calculate_angles(landmarks)
            
            # Check exercise form
            feedback = check_exercise_form(angles, patient_data['current_exercise'])
            
            # Update rep count based on feedback
            if feedback['rep_completed']:
                patient_data['reps'] += 1
                patient_data['accuracy'].append(feedback['accuracy'])
                # Keep only last 10 for average
                if len(patient_data['accuracy']) > 10:
                    patient_data['accuracy'].pop(0)
            
            # Calculate average accuracy
            avg_accuracy = 0
            if patient_data['accuracy']:
                avg_accuracy = sum(patient_data['accuracy']) / len(patient_data['accuracy'])
            
            return jsonify({
                'success': True,
                'landmarks': landmarks,
                'angles': angles,
                'feedback': feedback['message'],
                'reps': patient_data['reps'],
                'accuracy': int(avg_accuracy),
                'timestamp': time.time()
            })
        else:
            return jsonify({
                'success': False,
                'message': 'No person detected. Please stand in camera view (full body visible)',
                'reps': patient_data['reps'],
                'accuracy': 0
            })
            
    except Exception as e:
        print("Error:", str(e))  # Print error to console
        return jsonify({
            'success': False,
            'message': f'Error: {str(e)}',
            'reps': patient_data['reps'],
            'accuracy': 0
        })

def calculate_angles(landmarks):
    """Calculate joint angles from landmarks"""
    angles = {}
    
    try:
        # Helper function to get angle between 3 points
        def get_angle(p1, p2, p3):
            """Calculate angle at p2 between lines p1-p2 and p2-p3"""
            # Get coordinates
            x1, y1 = p1['x'], p1['y']
            x2, y2 = p2['x'], p2['y']
            x3, y3 = p3['x'], p3['y']
            
            # Calculate vectors
            v1 = (x1 - x2, y1 - y2)
            v2 = (x3 - x2, y3 - y2)
            
            # Calculate dot product
            dot_product = v1[0]*v2[0] + v1[1]*v2[1]
            
            # Calculate magnitudes
            mag_v1 = np.sqrt(v1[0]**2 + v1[1]**2)
            mag_v2 = np.sqrt(v2[0]**2 + v2[1]**2)
            
            # Avoid division by zero
            if mag_v1 == 0 or mag_v2 == 0:
                return 0
            
            # Calculate angle in degrees
            cos_angle = dot_product / (mag_v1 * mag_v2)
            # Clip to valid range to avoid numerical errors
            cos_angle = max(-1.0, min(1.0, cos_angle))
            angle = np.arccos(cos_angle)
            return np.degrees(angle)
        
        # MediaPipe Pose Landmark Indices:
        # 0: nose, 11: left shoulder, 12: right shoulder
        # 13: left elbow, 14: right elbow
        # 15: left wrist, 16: right wrist
        # 23: left hip, 24: right hip
        # 25: left knee, 26: right knee
        # 27: left ankle, 28: right ankle
        
        # Dictionary for easy access
        lm = {i: landmarks[i] for i in range(len(landmarks))}
        
        # Calculate angles only if landmarks are visible
        if 13 in lm and 11 in lm and 15 in lm and lm[13]['visibility'] > 0.5:
            angles['left_elbow'] = get_angle(lm[11], lm[13], lm[15])
        
        if 14 in lm and 12 in lm and 16 in lm and lm[14]['visibility'] > 0.5:
            angles['right_elbow'] = get_angle(lm[12], lm[14], lm[16])
        
        if 25 in lm and 23 in lm and 27 in lm and lm[25]['visibility'] > 0.5:
            angles['left_knee'] = get_angle(lm[23], lm[25], lm[27])
        
        if 26 in lm and 24 in lm and 28 in lm and lm[26]['visibility'] > 0.5:
            angles['right_knee'] = get_angle(lm[24], lm[26], lm[28])
        
        if 23 in lm and 11 in lm and 25 in lm and lm[23]['visibility'] > 0.5:
            angles['left_hip'] = get_angle(lm[11], lm[23], lm[25])
        
        if 24 in lm and 12 in lm and 26 in lm and lm[24]['visibility'] > 0.5:
            angles['right_hip'] = get_angle(lm[12], lm[24], lm[26])
            
    except Exception as e:
        print(f"Angle calculation error: {e}")
    
    return angles

def check_exercise_form(angles, exercise):
    """Check if user is doing exercise correctly"""
    feedback = {
        'message': 'Move into position...',
        'accuracy': 0,
        'rep_completed': False
    }
    
    if not angles:
        feedback['message'] = 'Cannot see your joints clearly. Adjust position.'
        return feedback
    
    if exercise == 'knee_extension':
        # Use right knee angle (can also use left)
        if 'right_knee' in angles:
            angle = angles['right_knee']
            patient_data['last_angle'] = angle
            
            # Target: 90 degrees for knee bend
            if 80 < angle < 100:
                feedback['message'] = '✅ PERFECT FORM! Hold...'
                feedback['accuracy'] = 100
            elif angle <= 80:
                diff = 90 - angle
                feedback['message'] = f'⬆️ Straighten knee (target: 90°, current: {int(angle)}°)'
                feedback['accuracy'] = max(0, int((angle/90)*100))
            elif angle >= 100:
                feedback['message'] = f'⬇️ Bend knee more (target: 90°, current: {int(angle)}°)'
                feedback['accuracy'] = max(0, int((180-angle)/90*100))
            
            # Count rep when angle passes through 90
            if 'last_knee_angle' not in check_exercise_form.__dict__:
                check_exercise_form.last_knee_angle = angle
            
            # Detect when knee bends past 90 then returns
            if check_exercise_form.last_knee_angle < 85 and angle > 90:
                feedback['rep_completed'] = True
                feedback['message'] = '🎉 GREAT REP! Keep going!'
            
            check_exercise_form.last_knee_angle = angle
            
        elif 'left_knee' in angles:
            angle = angles['left_knee']
            patient_data['last_angle'] = angle
            
            if 80 < angle < 100:
                feedback['message'] = '✅ PERFECT!'
                feedback['accuracy'] = 100
            else:
                feedback['message'] = f'Adjust knee to 90° (current: {int(angle)}°)'
                feedback['accuracy'] = max(0, 100 - abs(90 - angle))
    
    elif exercise == 'shoulder_raise':
        if 'right_elbow' in angles:
            angle = angles['right_elbow']
            if angle > 160:
                feedback['message'] = '✅ PERFECT RAISE!'
                feedback['accuracy'] = 100
            else:
                feedback['message'] = f'⬆️ Raise arm higher ({int(angle)}°/180°)'
                feedback['accuracy'] = int((angle/180)*100)
    
    elif exercise == 'ankle_pump':
        # Simplified - just use knee angle as proxy
        if 'right_knee' in angles:
            angle = angles['right_knee']
            if 70 < angle < 110:
                feedback['message'] = '✅ Good movement'
                feedback['accuracy'] = 100
            else:
                feedback['message'] = 'Move ankle up and down'
                feedback['accuracy'] = 50
    
    return feedback

@app.route('/reset_reps', methods=['POST'])
def reset_reps():
    patient_data['reps'] = 0
    patient_data['accuracy'] = []
    patient_data['rep_in_progress'] = False
    return jsonify({'success': True, 'message': 'Reps reset'})

@app.route('/set_exercise', methods=['POST'])
def set_exercise():
    data = request.json
    exercise = data.get('exercise', 'knee_extension')
    patient_data['current_exercise'] = exercise
    patient_data['reps'] = 0
    patient_data['accuracy'] = []
    return jsonify({'success': True, 'message': f'Exercise set to {exercise}'})

if __name__ == '__main__':
    print("\n" + "="*60)
    print("🚀 REHAB.AR AI SERVER STARTING...")
    print("="*60)
    print("\n✅ MediaPipe initialized successfully!")
    print("\n📡 Server running at: http://localhost:5000")
    print("\n🌐 To access from your phone:")
    print("   1. Find your computer's IP address:")
    print("      - Windows: Open CMD and type 'ipconfig'")
    print("      - Look for 'IPv4 Address' (e.g., 192.168.1.5)")
    print("   2. On your phone, open: http://[YOUR-IP]:5000/website/index.html")
    print("\n📱 For demo:")
    print("   - Stand 2 meters from camera")
    print("   - Make sure full body is visible")
    print("   - Good lighting helps detection!")
    print("\n🔴 Keep this window OPEN while using the app!")
    print("🟢 Press Ctrl+C to stop server\n")
    print("="*60)
    
    # Try different ports if 5000 is busy
    port = 5000
    while True:
        try:
            app.run(host='0.0.0.0', port=port, debug=False, threaded=True)
            break
        except OSError:
            print(f"⚠️ Port {port} is busy, trying {port+1}...")
            port += 1