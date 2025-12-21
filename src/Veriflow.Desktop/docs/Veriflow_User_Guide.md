# Veriflow User Guide

**Version 1.1.0**

---

## Table of Contents

1. Introduction
2. Installation
3. Getting Started
4. Features Overview
5. Detailed Module Guide
6. Keyboard Shortcuts
7. Session Management
8. Troubleshooting
9. Technical Specifications
10. Legal Information

---

## 1. Introduction

Veriflow is a professional media verification and workflow tool designed for video and audio production environments. It provides comprehensive tools for media playback, quality control, transcoding, and secure file operations.

---

## 2. Installation

### System Requirements
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- 8GB RAM minimum (16GB recommended)
- 500MB free disk space

### Installation Steps
1. Run the Veriflow installer
2. Follow the installation wizard
3. Launch Veriflow from the Start menu

---

## 3. Getting Started

### First Launch
Upon first launch, Veriflow opens to the **SECURE COPY** page. You can navigate between different modules using:
- Function keys (F1-F6)
- Menu: View > [Module Name]
- Bottom navigation bar

### User Interface
- **Top Menu**: File, Edit, View, Help
- **Main Content Area**: Active module view
- **Bottom Navigation**: Quick access to all modules
- **Profile Toggle**: Switch between Audio and Video modes (Ctrl+Tab)

---

## 4. Features Overview

### SECURE COPY (F1)
Dual-destination file copying with hash verification for secure media transfers.

### MEDIA (F2)
File browser and media library management.

### PLAYER (F3)
Professional audio/video playback with metadata display.

### SYNC (F4)
Synchronization tools for multi-camera and multi-track workflows.

### TRANSCODE (F5)
Format conversion and encoding tools.

### REPORTS (F6)
Quality control reporting and EDL generation.

---

## 5. Detailed Module Guide

### SECURE COPY
**Purpose**: Copy media files to two destinations simultaneously with integrity verification.

**Features**:
- Source file selection
- Dual destination paths (A and B)
- Hash verification (xxHash64)
- MHL 1.1 Generation (Automatic Media Hash List)
- Progress tracking
- Copy history

**Workflow**:
1. Select source file/folder
2. Set destination A (main)
3. Set destination B (secondary/backup)
4. Click "START COPY"
5. Monitor progress
6. Verify completion

### Verify Only Mode
Veriflow 1.1.0 introduces a dedicated **Verify Only** mode for checking the integrity of previously offloaded media using MHL files.

**How to use:**
1.  Go to the **Secure Copy** page.
2.  Switch the mode toggle at the top from **OFFLOAD** to **VERIFY**.
3.  Click **Open Folder** to select the directory containing your media and the `.mhl` file.
    - Veriflow supports folder drag-and-drop.
4.  Click **Verify**.

Veriflow will parse the existing MHL file and re-calculate xxHash64 checksums for all referenced files, reporting any discrepancies or missing files.

### MEDIA
**Purpose**: Browse and manage media files.

**Features**:
- File system browser
- Media file preview
- Metadata display
- Quick file loading

**Workflow**:
1. Navigate file system
2. Select media files
3. View metadata
4. Load to Player or other modules

**Editing Metadata**:
1. Select a media file.
2. Click the **Edit Metadata** button at the bottom of the metadata panel.
3. Modify the available fields (Scene, Take, Tape, etc.) in the popup window.
4. Click **Save** to write changes back to the file's BWF/iXML headers (WAV/BWF only).

### PLAYER
**Purpose**: Professional playback with frame-accurate control.

**Audio Profile Features**:
- Multi-track audio playback
- Waveform display
- VU meters
- Track controls (mute, solo, volume, pan)

**Video Profile Features**:
- Frame-accurate playback
- Timecode display
- Metadata panel
- Logged clips list
- Transport controls

**Keyboard Shortcuts**:
- Space: Play/Pause
- Enter: Stop
- Left/Right arrows: Frame step
- J/K/L: Shuttle control

### SYNC
**Purpose**: Synchronize multiple audio and video files.

**Features**:
- **Smart Sync**: Automatically aligns media using one of two methods.
- **Redundancy Protection**: If you run a second sync pass (e.g., Waveform after Timecode), **Veriflow only processes the remaining unmatched files**. It intelligently ignores files that are already synchronized.
- **Multi-Source**: Import video and audio files from different folders.
- **Batch Export**: Export all synchronized clips as new master files.

**How to Sync**:
1.  **Import Media**: Drag & Drop video and audio files into their respective pools.
2.  **Select Method**:
    *   Click **Sync by Timecode** (Recommended first step): Instantly matches files with matching embedded timecode.
    *   Click **Sync by Waveform**: Analyzes audio content to match remaining unsynced files. This uses parallel processing for maximum speed.
3.  **Export**: Select a destination folder and export the synchronized clips.

> **Note**: For optimal performance, synchronize by Timecode first to match the majority of clips, then run Waveform Sync to catch the remaining files that may have drifted or lack timecode.

### TRANSCODE
**Purpose**: Convert media files to different formats.

**Features**:
- Batch processing
- Format presets
- Custom encoding settings
- Timecode Burn-in (for Proxy formats)
- Queue management

**Supported Formats**:
- Input: Most common video/audio formats
- Output: MP4, MOV, MXF, WAV, etc.

### REPORTS
**Purpose**: Generate quality control reports and EDLs.

**Audio Profile**:
- Audio analysis reports
- Technical specifications
- Export to PDF

**Video Profile**:
- Video quality reports
- EDL generation
- Logged clips export
- Video quality reports
- EDL generation
- Logged clips export
- Technical metadata

**Daily Dailies Workflow**:
1.  **Review**: Load daily rushes into the Reports > Config list.
2.  **Verify**: Toggle "Verification" mode to check checksums if needed.
3.  **Export Session**: Use the **"Export Session EDL/ALE"** button to generate a master file containing all logged clips from the day, ready for editorial import.

---

## 6. Keyboard Shortcuts

### Global
- **Ctrl+N**: New Session
- **Ctrl+O**: Open Session
- **Ctrl+S**: Save Session
- **Ctrl+Z**: Undo (placeholder)
- **Ctrl+Y**: Redo (placeholder)
- **Ctrl+X**: Cut
- **Ctrl+C**: Copy
- **Ctrl+V**: Paste
- **Ctrl+Tab**: Toggle Audio/Video mode
- **F1**: Help

### Navigation
- **F1**: SECURE COPY
- **F2**: MEDIA
- **F3**: PLAYER
- **F4**: SYNC
- **F5**: TRANSCODE
- **F6**: REPORTS

### Player
- **Space**: Play/Pause
- **Enter**: Stop
- **Left/Right**: Frame step
- **Left/Right**: Frame step
- **J/K/L**: Shuttle
- **Metadata Panel**: Displays detailed technical info including **UCS (Universal Category System)** data for audio files.

---

## 7. Session Management

### What is a Session?
A session saves your current workspace state including:
- Loaded media files
- Reports
- Transcode queue
- Current page and mode

### Session Operations

**New Session** (Ctrl+N):
- Clears current workspace
- Prompts to save if modified

**Open Session** (Ctrl+O):
- Loads saved session file (.vfsession)
- Restores all workspace state

**Save Session** (Ctrl+S):
- Saves current workspace
- Creates .vfsession file

---

## 8. Troubleshooting

### Application won't start
- Verify .NET 8.0 Runtime is installed
- Check crash log on Desktop: `Veriflow_CrashLog.txt`
- Reinstall application (All dependencies are included)

### Media won't play
- Verify file format is supported
- Check codec compatibility
- Note: FFmpeg and LibVLC are embedded, no external installation required

### Performance issues
- Close unused applications
- Reduce preview quality
- Check system resources

### Log Files
Access logs via: **Help > Open Log Folder**
Location: `%APPDATA%\Veriflow\Logs`

---

## 9. Technical Specifications

### Supported Formats
**Video**: MP4, MOV, MXF, AVI, MKV, etc.
**Audio**: WAV, MP3, AAC, FLAC, etc.

> **Note**: Apple ProRes RAW is detected but requires external transcoding via specialized software before import.

### Dependencies
- FFmpeg (LGPL v2.1)
- LibVLC (LGPL v2.1+)
- .NET 8.0 Runtime

---

## 10. Legal Information

### Third-Party Software
Veriflow uses the following open-source libraries:

**FFmpeg** - LGPL v2.1 / GPL v2+
Source: https://ffmpeg.org

**LibVLC** - LGPL v2.1+
Source: https://www.videolan.org

**CSCore** - MS-PL
**QuestPDF** - MIT
**MathNet.Numerics** - MIT

For full license information, see **Help > About Veriflow**.

### Copyright
© 2025 Veriflow. All rights reserved.

---

**For support, visit: [Support URL]**
**Documentation version: 1.0.0**
