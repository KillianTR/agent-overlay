# Agent Overlay for Windows

An always-on-top desktop overlay for Windows that monitors every running **Claude Code** and **Google Antigravity** session at a glance.

See which agents are currently working, which need your confirmation or input, and get instant audio and speech notifications when a process finishes so you can work on other things without checking both windows manually. Click any session to bring its application directly to the front.

<p align="center">
  <img src="docs/screenshot-en-dark.png" alt="Agent Overlay, dark theme" width="340">
  <img src="docs/screenshot-en-light.png" alt="Agent Overlay, light theme" width="340">
</p>

## ✨ Key Features

- **Dual Agent Support**:
  - 🤖 **Google Antigravity**: Real-time tracking of active conversations, projects, and tool execution states directly from local SQLite logs.
  - ⚡ **Claude Code**: Live status monitoring across CLI and Desktop app sessions.
- **Instant Audio & Voice Alerts**:
  - Subtle chime and spoken alert (via Windows built-in TTS) whenever an agent completes its turn (`"Antigravity [Project] done"`) or asks for user authorization/feedback (`"Needs input"`).
  - Automatically mutes while the PC is locked, asleep, or running full-screen games/apps.
- **Click to Jump**:
  - Click any session card to instantly bring its corresponding IDE window (Antigravity or Claude) into foreground focus.
- **Stay Out of the Way**:
  - Compact always-on-top widget, minimizable to the system tray, with dark and light theme options.
- **Offline & Private**:
  - Runs 100% locally on your machine, reading local log databases and files. Zero network calls or credential handling.

## 🚀 Quick Start (Windows)

### Option 1: Run the pre-built executable
1. Open [`Iniciar-Agent-Overlay.bat`](Iniciar-Agent-Overlay.bat) or run `bin\ClaudeSessionOverlay.exe`.
2. The overlay will automatically attach to your screen and detect your active Claude and Antigravity sessions.

### Option 2: Build from Source
Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/KillianTR/agent-overlay.git
cd agent-overlay
dotnet publish app\ClaudeSessionOverlay.csproj -c Release -o bin
.\bin\ClaudeSessionOverlay.exe
```

## 🛠️ Credits & Attribution

- Built and adapted with Google Antigravity agent integration by [KillianTR](https://github.com/KillianTR).
- Based on the original [claude-overlay](https://github.com/jsuk10/claude-overlay) by [jsuk10](https://github.com/jsuk10).
- MIT Licensed.
