# ImmersiveBrainVR — Senior Design Project

An immersive VR brain anatomy learning application built in **Unity 6000.3.6f1** using **OpenXR**, **XR Interaction Toolkit 3.3**, and **XR Hands 1.7**.  
Players explore a 3D brain model in a virtual operating room, peel apart anatomical layers, complete quizzes, and receive real-time haptic/audio feedback — all with hand-tracked interactions on Meta Quest.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Clone the Repository](#clone-the-repository)
3. [Open the Project in Unity](#open-the-project-in-unity)
4. [Restore the Large Brain Asset](#restore-the-large-brain-asset)
5. [Project Structure](#project-structure)
6. [Build & Deploy to Quest](#build--deploy-to-quest)
7. [Running in the Editor (Link / Simulator)](#running-in-the-editor-link--simulator)
8. [Scenes Overview](#scenes-overview)
9. [Key Systems](#key-systems)
10. [Troubleshooting](#troubleshooting)

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| **Unity Editor** | **6000.3.6f1** (Unity 6 LTS) | Install via Unity Hub — must be this exact version |
| **Unity Hub** | 3.x+ | For managing installs and projects |
| **Android Build Support** module | (installed with Unity) | Includes Android SDK, NDK, JDK |
| **Git** | 2.x+ | For cloning the repository |
| **Meta Quest headset** | Quest 2 / Quest 3 / Quest Pro | Target hardware |
| **Meta Quest Developer Hub** or **SideQuest** | Latest | Optional — for sideloading APKs |
| **Meta Quest Link** (cable or Air Link) | Latest | Optional — for testing in-editor |

### Installing Unity 6000.3.6f1

1. Open **Unity Hub** → **Installs** → **Install Editor**.
2. Select version **6000.3.6f1**. If it's not listed, use the **Archive** tab or download from [Unity Download Archive](https://unity.com/releases/editor/archive).
3. During install, check:
   - **Android Build Support**
   - **Android SDK & NDK Tools**
   - **OpenJDK**

---

## Clone the Repository

```bash
git clone git@github.com:YoussiefKhalifa2003/SeniorDesign_ImmersiveBrainVR.git
```

Or using HTTPS:

```bash
git clone https://github.com/YoussiefKhalifa2003/SeniorDesign_ImmersiveBrainVR.git
```

This will create a folder called `SeniorDesign_ImmersiveBrainVR/` containing the project.

---

## Open the Project in Unity

1. Launch **Unity Hub**.
2. Click **Open** → navigate to the cloned `SeniorDesign_ImmersiveBrainVR/` folder → select it.
3. Unity Hub will detect the required version (**6000.3.6f1**). If you don't have it installed, Hub will prompt you to install it — do so with Android Build Support checked.
4. Click the project to open it.
5. **First open will take 5–15 minutes** while Unity regenerates the `Library/` cache, imports assets, and compiles scripts. This is normal.

---

## Restore the Large Brain Asset

The main brain model (`Assets/Allen_brain_final.fbx`, ~229 MB) was removed from the repository because GitHub rejects files over 100 MB.

**To restore it:**

1. Obtain `Allen_brain_final.fbx` from the project team (shared drive, USB, etc.).
2. Place it in `Assets/` at the root level:
   ```
   Assets/Allen_brain_final.fbx
   ```
3. Unity will auto-import it and generate a matching `.meta` file (the `.meta` reference is already in the repo, so references in scenes will reconnect automatically).

> **If you skip this step:** Scenes that reference the brain model will show pink/missing mesh warnings, but the project will still compile.

---

## Project Structure

```
SeniorDesign_ImmersiveBrainVR/
├── Assets/
│   ├── Scenes/
│   │   ├── BasicScene.unity          — Minimal test scene
│   │   ├── BrainDissectionScene.unity — Main dissection experience
│   │   ├── SampleScene.unity         — XR template starter
│   │   └── TutorialScene.unity       — Guided onboarding
│   ├── Scripts/
│   │   ├── BrainDissection/          — Core dissection logic, layers, tools
│   │   ├── Assessment/               — Quizzes, achievements, leaderboard
│   │   └── StartMenu/                — Login, menus, progress, scene flow
│   ├── Materials/                    — Shared materials
│   ├── Data/                         — ScriptableObjects / JSON data
│   ├── XR/ & XRI/                   — XR rig prefabs & interaction config
│   ├── Samples/                      — XR Toolkit sample assets (hands, etc.)
│   └── VRTemplateAssets/            — Unity VR template defaults
├── Packages/                        — Package manifest (auto-resolved by Unity)
├── ProjectSettings/                 — Quality, input, physics, XR settings
├── UserSettings/                    — Editor layout & preferences
├── Project_Progress.csv             — Sprint/task progress log
└── .gitignore
```

---

## Build & Deploy to Quest

### One-time setup

1. **File → Build Settings** → select **Android** → click **Switch Platform** (takes a few minutes the first time).
2. **Edit → Project Settings → XR Plug-in Management**:
   - Under the **Android** tab, ensure **OpenXR** is checked.
   - Under OpenXR settings, ensure **Meta Quest Touch Pro Controller Profile** and **Hand Tracking Subsystem** are enabled.
3. Connect your Quest via USB (or enable **wireless ADB** in Quest Developer Hub).
4. Enable **Developer Mode** on the headset (Settings → System → Developer).

### Build

1. **File → Build Settings** → verify scenes are listed (add any missing via drag-and-drop):
   - `Scenes/BasicScene` (index 0 — or your desired entry scene)
   - `Scenes/BrainDissectionScene`
   - `Scenes/TutorialScene`
2. Click **Build and Run**.
3. Choose an output folder (e.g., `Builds/`).  
4. Unity compiles, packages, and deploys the APK directly to the connected Quest.

### Manual install

If you built an APK without "Run":

```bash
adb install -r path/to/your.apk
```

The app will appear in **Unknown Sources** on the Quest.

---

## Running in the Editor (Link / Simulator)

You can test without building to the Quest:

### Option A — Meta Quest Link

1. Connect Quest to PC via USB-C or enable Air Link.
2. In Unity: **Edit → Project Settings → XR Plug-in Management → Windows** tab → enable **OpenXR**.
3. Press **Play** in the Editor — the scene streams to your headset.

### Option B — XR Device Simulator

1. Unity includes an XR Device Simulator with the XR Interaction Toolkit samples.
2. Press **Play** — use keyboard + mouse to simulate head/hand movement (WASD + mouse look, T/Y to toggle hands).

---

## Scenes Overview

| Scene | Purpose |
|-------|---------|
| **BasicScene** | Lightweight scene for testing XR rig setup and controllers |
| **BrainDissectionScene** | Main experience — brain model, layer peeling, region inspection, tools |
| **TutorialScene** | Guided walkthrough teaching the user how to interact |
| **SampleScene** | Default Unity VR template scene (XR origin, teleportation) |

---

## Key Systems

| System | Location | Description |
|--------|----------|-------------|
| Brain Layer Management | `Scripts/BrainDissection/AnatomyLayerService.cs` | Controls exploded views, layer visibility |
| Region Inspection | `Scripts/BrainDissection/RegionInspector.cs` | Shows info panels when a region is selected |
| Hand Tracking | XR Hands + `BlueHandVisuals.cs` | Visual hand mesh with haptic feedback |
| Lab Tools (scalpel, tweezers) | `Scripts/BrainDissection/LabTool*.cs` | Grabbable tools for dissection interaction |
| Quiz / Assessment | `Scripts/Assessment/QuizManager.cs` | Timed quizzes on brain anatomy |
| Progress Tracking | `Scripts/StartMenu/ProgressTracker.cs` | Saves achievements and session data locally |
| Speech / Narration | `Scripts/BrainDissection/Speech/` | Text-to-speech region descriptions (Windows SAPI) |
| Scene Flow | `Scripts/StartMenu/SceneFlowManager.cs` | Manages transitions between scenes |

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| **Unity says wrong version** | Install exactly **6000.3.6f1** via Unity Hub |
| **Pink/missing materials** | Open **Edit → Rendering → Render Pipeline Converter** and upgrade materials to URP |
| **Missing brain mesh** | See [Restore the Large Brain Asset](#restore-the-large-brain-asset) |
| **Compilation errors on open** | Wait for full import; if errors persist, delete `Library/` and reopen |
| **Quest not detected** | Enable Developer Mode on Quest; check USB drivers; run `adb devices` to confirm |
| **Hand tracking not working** | Ensure OpenXR Hand Tracking is enabled in XR Plug-in Management; Quest firmware must be up to date |
| **Low FPS on Quest** | Reduce quality in **Edit → Project Settings → Quality** (use the Android profile); check Profiler |
| **Scripts reference `Allen_brain_final` but object is missing** | The `.meta` GUID will reconnect once you place the `.fbx` back in `Assets/` |

---

## Additional Notes

- **Do NOT commit the `Library/` folder** — it is auto-generated and multi-gigabyte.
- **All `.meta` files must be committed** — they store Unity's GUIDs linking assets to scene references.
- The project targets **Universal Render Pipeline (URP) 17.3** — all materials must use URP-compatible shaders.
- Quest builds use **OpenXR** (not the deprecated Oculus SDK). The Meta OpenXR package (`com.unity.xr.meta-openxr 2.5.0`) handles Quest-specific features.

---

## Contact / Team

Youssief Khalifa — [GitHub](https://github.com/YoussiefKhalifa2003)
Yahya Elsawi - [GitHub](https://github.com/Yahyaelsawii)
---

*Last updated: May 2026*
