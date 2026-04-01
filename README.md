# 🏥 Rehab.AR - AI Physiotherapy Assistant

![Hackathon](https://img.shields.io/badge/Virtuovation%202.0-2nd%20Place-brightgreen)
![Python](https://img.shields.io/badge/Python-3.9+-blue)
![MediaPipe](https://img.shields.io/badge/MediaPipe-Pose%20Detection-orange)
![Unity](https://img.shields.io/badge/Unity-AR%20Integration-black)

## 🎯 About the Project

**Rehab.AR** is an AI-powered physiotherapy assistant that turns any smartphone into a personal physiotherapist. Built in **24 hours** at **Virtuovation 2.0** hackathon, where we secured **🥈 2nd Place**.

### The Problem
- 2 crore+ surgeries happen in India every year
- 70% of patients abandon physiotherapy due to cost, travel, and lack of motivation
- Current solutions are expensive (₹2-5 lakhs for motion capture suits)

### Our Solution
- Browser-based AR physiotherapy (no app download!)
- Real-time pose detection with 33 body landmarks
- Voice feedback and joint angle tracking
- Doctor dashboard for remote monitoring
- Gamification with points, streaks, and badges

---

## 🛠️ Tech Stack

| Tool | Purpose |
|------|---------|
| **Blender** | 3D avatar modeling |
| **Maya** | Exercise animations |
| **Unity** | AR integration + WebGL build |
| **MediaPipe** | AI pose detection (33 landmarks) |
| **Python Flask** | Backend server |
| **HTML/CSS/JS** | Frontend website |
| **Web Speech API** | Voice feedback |

---

## ✨ Features

- ✅ **33 Body Landmarks** - Full pose detection with MediaPipe
- ✅ **Real-time Angle Tracking** - Shows joint angle in degrees
- ✅ **Voice Feedback** - AI assistant speaks corrections
- ✅ **3 Exercise Types** - Knee Extension, Shoulder Raise, Ankle Pump
- ✅ **Rep Counter** - Only counts when form is correct
- ✅ **Doctor Dashboard** - Monitor patient progress
- ✅ **Gamification** - Points, streaks, badges
- ✅ **Zero Download** - Works entirely in browser

---

## 🚀 How to Run

### Prerequisites
- Python 3.9+
- MediaPipe 0.10.13
- Chrome browser (for camera access)

### Installation

```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/RehabAR.git
cd RehabAR

# Create virtual environment
python -m venv .venv
.venv\Scripts\activate  # On Windows

# Install dependencies
pip install mediapipe==0.10.13 opencv-python flask flask-cors numpy

# Run the server
python working_server.py
