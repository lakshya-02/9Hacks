# 🎯 9Hacks VR 3D Model Generator

> **Transform 2D Images into Interactive 3D Models in Virtual Reality**

A Meta Quest VR application that captures images via passthrough camera, generates 3D models using Stable Fast 3D (SF3D), and lets you interact with them in VR — all in real-time.

---

## 🏆 Made By

**Team TBD (To Be Decided)**

---

## 🚀 Overview

This project bridges the gap between 2D photography and 3D modeling by leveraging AI-powered 3D reconstruction directly inside a VR headset. Point, capture, generate, and grab — all within seconds.

### Key Features

- 📸 **Passthrough Camera Capture** — Capture real-world images inside Quest VR
- 🤖 **AI 3D Generation** — SF3D (Stable Fast 3D) backend generates 3D models from single images
- 🎮 **VR Interaction** — Spawn, grab, and manipulate generated models in VR space
- ⚡ **Real-time Pipeline** — Full image → 3D model workflow in under 10 seconds
- 🔌 **Two-Port Architecture** — Efficient upload/download server design for fast streaming

---

## 🏗️ System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Meta Quest VR                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Unity 6 URP + Meta XR SDK + OpenXR                      │  │
│  │                                                            │  │
│  │  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐ │  │
│  │  │ Passthrough  │   │  Snapshot    │   │    Model     │ │  │
│  │  │   Camera     │──▶│   Capture    │──▶│    Loader    │ │  │
│  │  │   Access     │   │              │   │              │ │  │
│  │  └──────────────┘   └───────┬──────┘   └──────▲───────┘ │  │
│  │                              │                  │         │  │
│  │                              │ PNG image        │ .glb    │  │
│  │                              ▼                  │         │  │
│  │                      ┌──────────────┐           │         │  │
│  │                      │  API Client  │───────────┘         │  │
│  │                      └───────┬──────┘                     │  │
│  └──────────────────────────────┼────────────────────────────┘  │
└─────────────────────────────────┼───────────────────────────────┘
                                  │
                    WiFi / Local Network (192.168.x.x)
                                  │
┌─────────────────────────────────▼───────────────────────────────┐
│                     PC (FastAPI Server)                         │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Python Backend (stable-fast-3d)                          │  │
│  │                                                            │  │
│  │  Port 8080 (Upload)          Port 8081 (Download)        │  │
│  │  ┌────────────────┐           ┌────────────────┐         │  │
│  │  │ POST /generate │           │ GET /download/ │         │  │
│  │  │                │           │    {job_id}    │         │  │
│  │  │ Receives PNG   │           │                │         │  │
│  │  │ Removes BG     │           │ Serves .glb    │         │  │
│  │  │ Runs SF3D      │           │                │         │  │
│  │  │ Returns job_id │           │                │         │  │
│  │  └────────────────┘           └────────────────┘         │  │
│  │                                                            │  │
│  │  CUDA GPU: SF3D Model Inference                           │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Pipeline Flow

```
User Action → Image Capture → PNG Upload → AI Processing → .glb Download → Model Load → VR Interaction
```

### Detailed Steps

1. **Input: User presses `1`, `2`, or `3` key**
   - Loads preset test image from `Assets/Image/`
   - (Alternatively: Space key captures from passthrough camera on Quest)

2. **Processing: SnapshotCapture.cs**
   - Displays image preview in RawImage UI
   - Encodes texture as PNG
   - Sends to APIClient

3. **Upload: APIClient.cs → POST http://serverIP:8080/generate**
   - Multipart form upload: `file=snapshot.png`
   - Server returns JSON: `{ job_id, status, download_url }`

4. **Server: FastAPI Backend**
   - Removes image background (rembg)
   - Runs SF3D inference on CUDA GPU
   - Generates 3D mesh as .glb file
   - Saves to `output/jobs/{job_id}.glb`

5. **Download: APIClient.cs → GET http://serverIP:8081/download/{job_id}**
   - Retry logic (30 attempts, 1s delay)
   - Receives binary .glb data

6. **Load: ModelLoader.cs**
   - Parses .glb with glTFast library
   - Instantiates mesh at spawn point
   - Adds physics: BoxCollider + Rigidbody (kinematic)
   - Adds MeshCollider on every child mesh
   - Attaches OVRGrabbable component

7. **Interaction: VR Controllers**
   - User can grab model with grip button
   - Move, rotate, inspect in 3D space

---

## 📁 Project Structure

```
d:/9Hacks/
├── Assets/
│   ├── Scripts/
│   │   ├── SnapshotCapture.cs    # Image capture & input handling
│   │   ├── APIClient.cs          # HTTP upload/download to SF3D server
│   │   ├── ModelLoader.cs        # .glb loading & grabbable setup
│   │   └── ServerConfig.cs       # ScriptableObject for server IP/ports
│   │
│   ├── Scenes/
│   │   └── SampleScene.unity     # Main VR scene with passthrough
│   │
│   ├── Image/                    # Test images (1.jpeg, 2.jpeg, 3.jpeg)
│   │
│   └── Plugins/Android/
│       └── AndroidManifest.xml   # Quest permissions & features
│
├── backend/
│   └── server.py                 # FastAPI SF3D inference server
│
└── README.md
```

---

## 🎮 Controls

### Editor (Testing)
| Key | Action |
|-----|--------|
| **1** | Load image `1.jpeg` → send to server → spawn 3D model |
| **2** | Load image `2.jpeg` → send to server → spawn 3D model |
| **3** | Load image `3.jpeg` → send to server → spawn 3D model |
| **Space** | Capture test image (no server) |

### Meta Quest VR
| Input | Action |
|-------|--------|
| **A Button** | Capture image from passthrough camera |
| **Right Trigger** | Capture image (alternative) |
| **Grip Button** | Grab spawned 3D model |
| **Space** (BT Keyboard) | Capture image |

---

## 🛠️ Tech Stack

### Unity (Client)
- **Unity 6 (2022.3 LTS)** — Game engine
- **Meta XR All-in-One SDK** — Quest VR support + passthrough camera API
- **OpenXR** — Cross-platform VR standard
- **glTFast** — Runtime .glb loading
- **Unity New Input System** — Keyboard + XR controller input
- **UnityWebRequest** — HTTP client for API calls

### Python (Server)
- **FastAPI** — High-performance async web framework
- **SF3D (Stable Fast 3D)** — STTR Labs' single-image 3D reconstruction
- **rembg** — Background removal preprocessing
- **PyTorch** — Deep learning framework
- **CUDA** — GPU acceleration
- **uvicorn** — ASGI server (dual-port architecture)

---

## 🔧 Configuration

### ServerConfig (Unity Inspector)
```yaml
Server IP: 127.0.0.1           # Change to PC's local IP for Quest (e.g., 192.168.0.182)
Upload Port: 8080
Download Port: 8081
Use Separate Download Port: ✓
```

### AndroidManifest.xml
```xml
<uses-permission android:name="horizonos.permission.HEADSET_CAMERA"/>
<uses-feature android:name="com.oculus.feature.PASSTHROUGH"/>
```

### Server Endpoints
- **POST /generate** — Upload image, returns `{ job_id, download_url }`
- **POST /generate-from-image** — Alias endpoint (supports "image" field name)
- **GET /download/{job_id}** — Download generated .glb file
- **GET /** — Server health check (port 8081)

---

## 🚦 Setup & Running

### 1. Start the SF3D Server
```bash
cd D:\stable-fast-3d
python backend/server.py
```
Server will start on:
- **Port 8080** — Upload endpoint
- **Port 8081** — Download endpoint

### 2. Configure Unity
1. Open project in Unity 6
2. Select `Assets/ServerConfig` asset
3. Set `Server IP` to your PC's local IP (e.g., `192.168.0.182`)
4. Verify scene setup in `SampleScene`

### 3. Build & Deploy to Quest
```
File → Build Settings → Android
✓ Build and Run
```

### 4. Test in Editor (Fast)
1. Press **Play**
2. Press **1**, **2**, or **3**
3. Watch Console logs
4. Model appears in scene

---

## 🎯 Key Implementation Details

### PassthroughCameraAccess Fix
**Problem:** Field was wrapped in `#if !UNITY_EDITOR`, making it invisible in Inspector.

**Solution:** Removed preprocessor directives from `passthroughCamera` field declaration so it's always visible and assignable.

### Input Detection
**Multi-method approach** ensures A button works regardless of SDK:
1. `OVRInput.Get(OVRInput.Button.One)` — Meta XR SDK
2. `InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primaryButton)` — OpenXR
3. `OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)` — Fallback to trigger

### Texture Lifecycle
- `CaptureSnapshot()` stores `_lastCapturedTexture` for preview
- `SendToServer()` makes a **copy** before sending, preventing preview destruction
- APIClient's `Destroy(texture)` only affects the copy

### Grabbable Setup
Every spawned model gets:
- **MeshCollider (convex)** on every child with a MeshFilter
- **BoxCollider** on root as fallback
- **Rigidbody** (kinematic, no gravity)
- **OVRGrabbable** OR **Oculus.Interaction.Grabbable** (runtime detection)
- **Layer = Default** for raycasting

---

## 📊 Performance Metrics

| Stage | Time | Notes |
|-------|------|-------|
| Image Capture | < 100ms | PassthroughCameraAccess.GetColors() |
| Network Upload | 200-500ms | PNG, ~2MB, local network |
| SF3D Inference | 3-8 seconds | CUDA GPU, 512x512 input |
| Network Download | 300-700ms | .glb, ~1-3MB |
| Model Load | 200-400ms | glTFast async instantiation |
| **Total Pipeline** | **4-10 seconds** | End-to-end |

---

## 🐛 Troubleshooting

### Unity Pausing on Model Load
**Fix:** Console → uncheck **"Error Pause"** button (top bar)

### PassthroughCameraAccess Not Assigned
**Fix:** Drag PassthroughCameraAccess component into SnapshotCapture's Inspector field

### Models Not Grabbable
**Fix:** Ensure rig has `OVRGrabber` on `RightHandAnchor` or `LeftHandAnchor`

### Server Connection Timeout
**Fix:**
1. Check server is running: `curl http://localhost:8080`
2. Verify Quest and PC are on same WiFi
3. Update ServerConfig IP to PC's local IP
4. Check Windows Firewall allows ports 8080/8081

---

## 📝 Future Enhancements

- [ ] Multi-view capture for better 3D reconstruction
- [ ] Model caching to avoid re-generation
- [ ] Scale/rotation controls via controller joysticks
- [ ] Export captured models to Quest storage
- [ ] Multiplayer model sharing in shared VR space
- [ ] Voice commands for capture
- [ ] Model gallery browser in VR

---

## 📄 License

This project was built for **9Hacks Hackathon** by **Team TBD (To Be Decided)**.

### Dependencies
- Unity 6 — Unity Personal/Pro License
- Meta XR SDK — Meta Platform Technologies License
- SF3D — Apache 2.0 / Research License (check STTR Labs repo)
- glTFast — Apache 2.0

---

## 🙏 Acknowledgments

- **STTR Labs** for Stable Fast 3D model
- **Meta** for Quest VR platform and passthrough API
- **Unity Technologies** for the game engine
- **glTFast contributors** for runtime GLTF loading
- **Claude Opus 4.6** for development assistance

---

## 📞 Contact

**Team TBD (To Be Decided)**

Built with ❤️ for 9Hacks

---

⭐ **Star this repo if you find it helpful!**
