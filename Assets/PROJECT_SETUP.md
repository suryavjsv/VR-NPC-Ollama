# VR AI NPC — Hardware Assembly Assistant
## Quest 3 + Unity 6 + Llama 3.2 1B

---

## Architecture Overview

```
Quest 3 (Unity Client)                Local PC Server (Python)
┌──────────────────────┐              ┌─────────────────────────┐
│  MicrophoneCapture   │──wav/pcm──►  │  FastAPI WebSocket       │
│                      │              │     ↓                     │
│                      │              │  Whisper.cpp (STT)        │
│                      │              │     ↓                     │
│                      │              │  Ollama / Llama 3.2 1B    │
│                      │              │     ↓                     │
│  AudioSource ◄───────│◄──wav─────── │  Piper TTS               │
│  OVRLipSync          │              └─────────────────────────┘
│  NPCAnimator         │
│  Meta Avatar / Custom│
└──────────────────────┘
      Connected via WebSocket over local Wi-Fi
```

---

## Open-Source Stack

| Component       | Tool                | License      | Notes                          |
|----------------|---------------------|--------------|--------------------------------|
| STT            | Whisper.cpp / faster-whisper | MIT   | Use `base` or `small` model    |
| LLM            | Llama 3.2 1B via Ollama     | Meta  | Quantized Q4_K_M recommended   |
| TTS            | Piper TTS                   | MIT   | Fast, offline, many voices     |
| Lip Sync       | Oculus Lip Sync SDK          | Meta  | Free with Meta XR SDK          |
| Avatars        | Meta Avatars SDK / ReadyPlayerMe | Free tier | RPM is easier for NPCs   |
| Server         | FastAPI + WebSocket          | MIT   | Python async server            |
| Communication  | NativeWebSocket (Unity)      | MIT   | Quest-compatible WebSocket     |

---

## Prerequisites

### PC Server
- Python 3.10+
- NVIDIA GPU (RTX 2060+ recommended) OR decent CPU
- Ollama installed (https://ollama.ai)
- ~4GB disk for models

### Unity Client
- Unity 6000.3.6f1
- Meta XR All-in-One SDK (from Unity Package Manager)
- Meta XR Audio SDK (contains OVR Lip Sync)
- Oculus Integration SDK (v68+)
- Target: Android (Quest 3)

---

## Setup Instructions

### STEP 1 — Server Setup

```bash
# 1. Install Ollama
curl -fsSL https://ollama.ai/install.sh | sh

# 2. Pull Llama 3.2 1B
ollama pull llama3.2:1b

# 3. Install Piper TTS
pip install piper-tts

# 4. Download a Piper voice (example: en_US-lessac-medium)
mkdir -p ~/piper-voices
cd ~/piper-voices
wget https://github.com/rhasspy/piper/releases/download/2023.11.14-2/voice-en_US-lessac-medium.tar.gz
tar -xzf voice-en_US-lessac-medium.tar.gz

# 5. Install faster-whisper (GPU-accelerated Whisper)
pip install faster-whisper

# 6. Install server dependencies
cd /path/to/Server
pip install -r requirements.txt

# 7. Run the server
python main.py
```

### STEP 2 — Unity Project Setup

```
1. Create new Unity 6 project (3D URP)
2. Build Settings → Android → Switch Platform
3. Player Settings:
   - Minimum API Level: 32
   - Target: ARM64
   - Color Space: Linear
4. Install via Package Manager:
   - Meta XR All-in-One SDK
   - Meta XR Audio (for OVR Lip Sync)
5. Project Settings → XR Plug-in Management → Enable Oculus
6. Copy all C# scripts from UnityScripts/ into Assets/Scripts/
7. Configure scene (see SCENE_SETUP section below)
```

### STEP 3 — Scene Setup

```
Scene Hierarchy:
├── XR Origin (from Meta XR SDK)
│   └── Camera Offset
│       └── Main Camera
├── NPC_Character
│   ├── Avatar Mesh (with SkinnedMeshRenderer)
│   ├── AudioSource (for TTS playback)
│   ├── OVRLipSyncContext (component)
│   ├── OVRLipSyncContextMorphTarget (component)
│   ├── NPCController.cs
│   ├── NPCAnimator.cs
│   └── LipSyncController.cs
├── NetworkManager (empty GameObject)
│   └── WebSocketClient.cs
├── MicrophoneCapture (empty GameObject)
│   └── MicrophoneCapture.cs
├── AssemblyStation
│   └── (Your hardware model / workbench)
└── UI
    └── StatusCanvas (World Space)
        └── StatusText
```

---

## Configuration

Edit `WebSocketClient.cs` → set server IP:
```csharp
private string serverUrl = "ws://YOUR_PC_IP:8765";
```

Find your PC's local IP:
- Windows: `ipconfig` → look for IPv4 under Wi-Fi
- Linux/Mac: `ifconfig` or `ip addr`

Both Quest 3 and PC must be on the **same Wi-Fi network**.

---

## Piper Voice Options (all free)

| Voice                    | Quality | Speed  | Style        |
|--------------------------|---------|--------|--------------|
| en_US-lessac-medium      | Good    | Fast   | Neutral      |
| en_US-amy-medium         | Good    | Fast   | British      |
| en_US-ryan-medium        | Good    | Fast   | Male         |
| en_GB-alan-medium        | Good    | Fast   | British Male |

Browse all: https://rhasspy.github.io/piper-samples/

---

## Performance Targets

| Metric                    | Target        |
|--------------------------|---------------|
| STT latency              | < 500ms       |
| LLM response (1B, GPU)   | < 800ms       |
| TTS generation            | < 400ms       |
| Total end-to-end          | < 2 seconds   |
| Quest 3 framerate         | 72-90 FPS     |

---

## Troubleshooting

- **No mic input on Quest**: Ensure `android.permission.RECORD_AUDIO` in AndroidManifest.xml
- **WebSocket won't connect**: Check firewall, ensure same Wi-Fi network, verify IP
- **Lip sync not moving**: Ensure OVRLipSyncContext audio loopback is set to the correct AudioSource
- **LLM slow responses**: Try `llama3.2:1b-q4_0` for faster inference, or use GPU
- **Piper voice not found**: Verify voice .onnx path in server config
