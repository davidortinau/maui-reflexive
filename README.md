# maui-reflexive

A `dotnet new` template that creates a **.NET MAUI Blazor Hybrid** app with an embedded **GitHub Copilot** overlay. Describe what you want to build in natural language, and Copilot writes the code and hot-swaps the running app — no context switching required.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)
![MAUI](https://img.shields.io/badge/MAUI-Blazor_Hybrid-purple)
![Copilot SDK](https://img.shields.io/badge/Copilot_SDK-0.1.23-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## ✨ What It Does

1. **Create** a new project from the template
2. **Run** the app on any platform (Mac Catalyst, iOS, Android, Windows)
3. **Tap the 🤖 button** to open the Copilot overlay
4. **Describe** the app or feature you want
5. **Copilot builds it** and the app reloads with your changes

The entire Copilot integration is **DEBUG-only** — it compiles to no-ops in Release builds, so your production app stays clean.

## 🚀 Quick Start

### Install the template

```bash
dotnet new install maui-reflexive
```

Or install from source:

```bash
git clone https://github.com/davidortinau/maui-reflexive.git
dotnet new install ./maui-reflexive/templates/maui-reflexive/
```

### Create a new project

```bash
dotnet new maui-reflexive -n MyApp
cd MyApp
```

### Authenticate with Copilot

```bash
copilot auth login
```

### Run it

```bash
# Mac Catalyst
dotnet run -f net10.0-maccatalyst

# Or target other platforms
dotnet build -f net10.0-ios
dotnet build -f net10.0-android
```

Tap the **🤖** floating button in the bottom-left corner to open the Copilot chat panel.

## 🏗️ Architecture

```
┌─────────────────────────────────┐
│        MAUI Blazor Hybrid       │
│  ┌───────────────────────────┐  │
│  │      Your App UI          │  │
│  │   (Home.razor, etc.)      │  │
│  │                           │  │
│  │  ┌─────────────────────┐  │  │
│  │  │  🤖 Copilot Overlay │  │  │
│  │  │  ┌───────────────┐  │  │  │
│  │  │  │  Chat Panel    │  │  │  │
│  │  │  │  Streaming     │  │  │  │
│  │  │  │  Tool Status   │  │  │  │
│  │  │  └───────────────┘  │  │  │
│  │  └─────────────────────┘  │  │
│  └───────────────────────────┘  │
│                                 │
│  Services (DEBUG only):         │
│  • CopilotService (SDK wrapper) │
│  • RelaunchService (hot-swap)   │
│  • DevTunnelService (remote)    │
│  • WsBridge (server/client)     │
└─────────────────────────────────┘
         │
         ▼
   GitHub Copilot SDK
   (stdio transport)
```

### Key Components

| Component | Description |
|---|---|
| **CopilotOverlay.razor** | Floating FAB + expandable chat panel with streaming markdown, tool indicators, and intent display |
| **CopilotService** | Single-session SDK wrapper — handles events, permissions, session resume |
| **CopilotInitService** | First-launch checks — CLI auth, tool restore, devtunnel availability |
| **SystemPromptBuilder** | Builds context-aware system prompt with project structure and relaunch instructions |
| **RelaunchService** | Orchestrates build → stage → hot-swap via platform-specific scripts |
| **DevTunnelService** | Enables remote development via Azure DevTunnels (for mobile device iteration) |
| **WsBridge** | WebSocket bridge for remote Copilot sessions over devtunnel |

### Relaunch Scripts

Platform-specific scripts handle the build-and-replace cycle:

| Script | Platform |
|---|---|
| `Scripts/relaunch.sh` | macOS (Mac Catalyst) |
| `Scripts/relaunch.ps1` | Windows |
| `Scripts/relaunch-ios.sh` | iOS Simulator |
| `Scripts/relaunch-android.sh` | Android Emulator |

## 🔧 Configuration

### Environment Variables

| Variable | Purpose |
|---|---|
| `MAUI_REFLEXIVE_PROJECT_DIR` | Override project directory detection (useful for non-standard layouts) |

### Agent Configuration

The template includes agent configuration files:

- **AGENTS.md** — Instructions for Copilot on how to work with the project, including relaunch procedures
- **.mauidevflow** — Port configuration for maui-devflow agent integration
- **.claude/skills/** — AI debugging skills for build-deploy-inspect workflows

### MauiDevFlow Integration

The template includes [maui-devflow](https://github.com/nicvercellis/maui-devflow) tooling for enhanced AI-assisted development:

```bash
# Restore the CLI tool
dotnet tool restore

# The agent package is included in the project for runtime inspection
```

## 📱 Remote Development (DevTunnel)

Iterate on a physical device while Copilot runs on your dev machine:

1. Install Azure DevTunnels: `devtunnel --version`
2. The app auto-detects devtunnel availability on first launch
3. When running on a remote device, the WebSocket bridge connects back to your dev machine

## 🔒 DEBUG-Only Design

All Copilot infrastructure is conditionally compiled:

- **`#if DEBUG`** wraps all SDK implementation in services
- **RELEASE builds** compile to empty no-op stubs — zero runtime overhead
- **No SDK packages** are required for production deployment
- The overlay FAB is only rendered when `IsDebug` is true

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET MAUI workload](https://learn.microsoft.com/dotnet/maui/get-started/installation): `dotnet workload install maui`
- [GitHub Copilot CLI](https://docs.github.com/en/copilot): `npm install -g @github/copilot` or via Homebrew
- Copilot authentication: `copilot auth login`
- Platform SDKs (Xcode for Mac/iOS, Android SDK, Windows SDK)

## 🤝 Acknowledgments

This template draws inspiration from:

- **[PolyPilot](https://github.com/nicvercellis/polypilot)** — Multi-session Copilot orchestrator with relaunch scripts and DevTunnel support
- **[MAUI.Sherpa](https://github.com/nicvercellis/maui.sherpa)** — Single-session Copilot with floating overlay UI

## 📄 License

MIT
