# Veriflow Pro - Avalonia Edition

Professional Audio/Video Workflow Application

## Overview

Veriflow Pro is a cross-platform professional application designed for sound engineers, video editors, and post-production professionals. Built with Avalonia UI for true cross-platform compatibility.

## Features

### 🎬 Core Modules

- **OFFLOAD (F1)** - Secure file offloading with MD5 checksum verification
- **MEDIA (F2)** - Browse and preview media files with metadata
- **PLAYER (F3)** - Professional playback with LibVLC integration
- **SYNC (F4)** - Audio/Video synchronization
- **TRANSCODE (F5)** - Professional media transcoding (ProRes, H.264, DNxHD, etc.)
- **REPORTS (F6)** - Camera and Sound report generation with PDF/EDL/ALE export

### ✨ Key Features

- Cross-platform (Windows, macOS, Linux)
- LibVLC-powered video playback
- Professional dark theme
- Keyboard shortcuts (F1-F6)
- Comprehensive in-app documentation
- Modern MVVM architecture

## Technology Stack

- **UI Framework**: Avalonia 11.x
- **Audio Engine**: MiniAudio (cross-platform)
- **Video Playback**: LibVLC 3.9.5
- **MVVM**: CommunityToolkit.Mvvm
- **Documentation**: Markdown.Avalonia
- **Target**: .NET 8.0

## Building

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or JetBrains Rider (recommended)

### Build Instructions

```bash
# Clone repository
git clone https://github.com/Herve-obe/Veriflow.git
cd Veriflow

# Restore dependencies
dotnet restore

# Build
dotnet build src/Veriflow.Avalonia/Veriflow.Avalonia.csproj

# Run
dotnet run --project src/Veriflow.Avalonia/Veriflow.Avalonia.csproj
```

## Project Structure

```
Veriflow/
├── src/
│   ├── Veriflow.Core/          # Shared business logic
│   └── Veriflow.Avalonia/      # Avalonia UI application
│       ├── Views/              # XAML views
│       ├── ViewModels/         # MVVM ViewModels
│       ├── Services/           # Application services
│       ├── Styles/             # Theme and styles
│       └── Assets/             # Resources and documentation
```

## Migration Status

This is the Avalonia port of the original WPF application.

**Completion**: ~85%

- ✅ Phase 1: Project Skeleton (100%)
- ✅ Phase 2: Services Layer (100%)
- ✅ Phase 3: View Migration (100%)
- ✅ Phase 4: Functionality (90%)
- ✅ Phase 5: Documentation (100%)
- 🔄 Phase 6: Platform Polish (In Progress)

## Platform Support

- ✅ **Windows** - Fully tested and supported
- ⏳ **macOS** - Ready for testing
- ⏳ **Linux** - Ready for testing

## License

Proprietary - © 2025 Veriflow Pro

## Version

**2.0.0** - Avalonia Cross-Platform Edition

---

Built with ❤️ using Avalonia UI
