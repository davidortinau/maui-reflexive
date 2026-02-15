# MauiReflexive — AI Agent Instructions

## Project Overview
This is a .NET MAUI Blazor Hybrid application created from the `maui-reflexive` template. It includes an embedded GitHub Copilot overlay for AI-assisted development.

**Bundle ID:** `com.companyname.MauiReflexive`
**Target Framework:** .NET 10
**Platforms:** Mac Catalyst, iOS, Android, Windows

## Architecture

### UI Stack
- **MAUI layer:** App.xaml → MainPage.xaml (BlazorWebView host)
- **Blazor layer:** Components/Routes.razor → Components/Layout/MainLayout.razor → Pages
- **Copilot overlay:** Components/CopilotOverlay.razor (DEBUG-only, embedded in MainLayout)

### Key Directories
| Directory | Purpose |
|-----------|---------|
| `Components/Pages/` | Blazor routable pages (add new pages here) |
| `Components/Layout/` | Layout components (MainLayout wraps all pages) |
| `Components/` | Shared Blazor components |
| `Services/` | Business logic and DI-registered services |
| `Models/` | Data models and DTOs |
| `Platforms/` | Platform-specific code (MacCatalyst, iOS, Android, Windows) |
| `wwwroot/` | Static web assets (CSS, JS, images) |
| `Scripts/` | Relaunch scripts for hot-swap deployment |

### DEBUG-only Infrastructure
All Copilot and development infrastructure is wrapped in `#if DEBUG` and excluded from release builds:
- `Services/CopilotService.cs` — Copilot SDK session management
- `Services/CopilotInitService.cs` — First-launch tool verification
- `Services/RelaunchService.cs` — Build and hot-swap orchestration
- `Services/DevTunnelService.cs` — Azure DevTunnel for remote access
- `Services/WsBridgeServer.cs` / `WsBridgeClient.cs` — WebSocket bridge for mobile
- `Components/CopilotOverlay.razor` — Floating Copilot UI

### DI Registration
All services are registered in `MauiProgram.cs`. Add new services there:
```csharp
builder.Services.AddSingleton<MyService>();
// or
builder.Services.AddTransient<MyService>();
```

## Build & Run

### Mac Catalyst
```bash
dotnet build -f net10.0-maccatalyst
dotnet run -f net10.0-maccatalyst
```

### Hot-swap relaunch
```bash
bash Scripts/relaunch.sh          # macOS
powershell Scripts/relaunch.ps1   # Windows
bash Scripts/relaunch-ios.sh      # iOS simulator
bash Scripts/relaunch-android.sh  # Android emulator
```

## AI Debugging (maui-devflow)

The project includes `Redth.MauiDevFlow.Agent` for AI-powered debugging.
Port: configured in `.mauidevflow` (default 9223).

### Key Commands
```bash
dotnet maui-devflow MAUI status          # Check connectivity
dotnet maui-devflow MAUI tree            # Visual tree with element IDs
dotnet maui-devflow MAUI screenshot      # Take screenshot
dotnet maui-devflow MAUI element <id>    # Get element details
dotnet maui-devflow MAUI logs --limit 20 # Read app logs
dotnet maui-devflow cdp snapshot         # Blazor DOM tree
dotnet maui-devflow cdp Runtime evaluate "<js>"  # Run JS in WebView
```

## Coding Conventions
- Nullable enabled project-wide
- Implicit usings enabled
- Pure Blazor UI (no XAML for page content)
- Services use constructor injection
- Async/await for all I/O operations
