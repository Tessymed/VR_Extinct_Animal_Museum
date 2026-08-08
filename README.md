# 🦤 VR Extinct Animal Museum (Meta Quest & PC VR)

[![Unity 6000.5](https://img.shields.io/badge/Unity-6000.5.0f1-blue.svg?style=flat&logo=unity)](https://unity.com/)
[![Meta Quest](https://img.shields.io/badge/Meta%20Quest-2%20%7C%203%20%7C%203S%20%7C%20Pro-0081FB.svg?style=flat&logo=meta)](https://www.meta.com/quest/)
[![OpenXR](https://img.shields.io/badge/OpenXR-Native%206DoF-black.svg?style=flat)](https://www.khronos.org/openxr/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An immersive virtual reality museum experience that brings extinct species back to life through interactive 3D exhibits, holographic artifact inspections, conservation lessons, and spatial docent audio narration.

---

## 🌟 Key Features

- **🏛️ Full 6DoF Immersive Museum**:
  - Step directly into a gallery environment with natural human eye-level height calibration and smooth 6DoF tracking.
  - Seamless raycast teleportation with spatial boundary collision protection.
- **🎙️ Spatial Museum Docent Audio Tour**:
  - 4 high-fidelity studio voiceover narrations with automatic trigger zones and audio ducking upon crystal inspection:
    1. `guide_01_welcome.wav` – Museum entrance & mission orientation.
    2. `guide_02_dodo.wav` – Dodo Bird exhibit & Island of Mauritius history.
    3. `guide_03_mammoth.wav` – Woolly Mammoth ice-age ecology & climate impact.
    4. `guide_04_thylacine.wav` – Tasmanian Tiger (Thylacine) extinction & human encroachment.
- **💎 Interactive Holographic Exhibit Crystals**:
  - Grab, lift, and inspect exhibit memory crystals using controller grip or raycast interaction.
  - Activates floating story tablets with historical timelines, extinction causes, and modern conservation takeaways.
- **⚡ Ultra-Optimized VR Performance Engine**:
  - Zero garbage collection overhead using `Physics.RaycastNonAlloc` buffers (eliminating 144 heap allocations/sec).
  - Cached player transforms via `XRPlayerCache` for rock-solid 72 / 90 / 120 FPS frame pacing on standalone Quest hardware.

---

## 🦣 Featured Exhibits

| Exhibit | Era / Region | Primary Extinction Cause | Interactive Element |
| :--- | :--- | :--- | :--- |
| **Dodo Bird** (*Raphus cucullatus*) | 17th Century / Mauritius | Introduced predators & habitat destruction | Interactive crystal & island lore card |
| **Woolly Mammoth** (*Mammuthus primigenius*) | Pleistocene / Arctic Tundra | Climate warming & human hunting | Giant tusk inspection & glacial timeline |
| **Tasmanian Tiger** (*Thylacine*) | 20th Century / Australia & Tasmania | Government bounties & habitat fragmentation | 1936 extinction archives & DNA study |

---

## 📦 How to Install & Play (Standalone Meta Quest APK)

### Method 1: SideQuest (Recommended)
1. Download the standalone APK from the **[GitHub Releases](https://github.com/Tessymed/VR_Extinct_Animal_Museum/releases)** section (or build using the 1-click build tool).
2. Connect your Meta Quest 2 / 3 / 3S / Pro to your PC via USB-C.
3. Open **SideQuest** and click the **"Install APK file from folder on computer"** button in the top-right toolbar.
4. Select `VR_Extinct_Animal_Museum.apk`.
5. In your headset, open **App Library ➔ Filter: "Unknown Sources" ➔ Launch `VR_Extinct_Animal_Museum`**.

### Method 2: Meta Quest Developer Hub (MQDH)
1. Open **Meta Quest Developer Hub** on your PC.
2. Connect your headset via USB-C or Wi-Fi.
3. In the **Apps** tab, drag & drop `VR_Extinct_Animal_Museum.apk` directly into the window.
4. Click **Launch App** or launch it directly from inside the headset.

### Method 3: Direct ADB Command Line
```powershell
adb install -r Builds/MetaQuest/VR_Extinct_Animal_Museum.apk
```

---

## 🛠️ Building From Source

### Prerequisites
- **Unity 6000.5.0f1** (or compatible Unity 6 LTS).
- **Modules Installed in Unity Hub**:
  - `Android Build Support`
  - `Android SDK & NDK Tools`
  - `OpenJDK`

### 1-Click Automated Build
1. Clone this repository:
   ```bash
   git clone https://github.com/Tessymed/VR_Extinct_Animal_Museum.git
   ```
2. Open the project in **Unity 6000.5.0f1**.
3. In Unity's top menu bar, click:
   - 👉 **`Tools ➔ Build ➔ Build Meta Quest Standalone APK`** (For native Quest 2/3/Pro APK).
   - 👉 **`Tools ➔ Build ➔ Build Windows PC VR (Quest Link)`** (For PC VR / AirLink).
4. The output `.apk` or `.exe` will automatically build and open in Windows File Explorer!

---

## 📁 Repository Structure

```
VR_Extinct_Animal_Museum/
├── Assets/
│   ├── Audio/Voiceover/       # Studio docent narration audio (WAV)
│   ├── Editor/                # 1-Click build helpers and VR setup tools
│   ├── Plugins/Android/       # Meta Quest 6DoF AndroidManifest.xml
│   ├── Scenes/                # Main museum scene (Museum_Main.unity)
│   ├── Scripts/               # Core gameplay, 6DoF pose enforcers, and audio managers
│   └── XR/                    # OpenXR & Oculus XR loader configurations
├── Builds/MetaQuest/          # Standalone Meta Quest APK output directory
├── ProjectSettings/           # URP, Quality, and OpenXR settings
└── README.md                  # Project documentation and guide
```

---

## 👥 Contributors & Team

- **Lead XR Development & Engineering**: Lawal
- **3D Modeling & Environment Art**: Museum Art Team
- **Historical Research & Storytelling**: Conservation Research Team

---

*Preserving history and inspiring modern wildlife conservation through immersive virtual reality.*
