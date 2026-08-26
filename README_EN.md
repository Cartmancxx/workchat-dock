# WorkChat Dock

[简体中文](README.md) · [English](README_EN.md)

> An elegant tray organizer for work and messaging apps — built for e-commerce operators and multi-platform teams.

WorkChat Dock brings your workplace messengers into one compact system-tray entry. Launch everything with one click, hover to open a slim vertical dock, jump to the app with new messages, and keep the original tray icons tucked away.

## Highlights

- One-click launch for every enabled work app
- A single aggregate tray icon with a compact hover flyout
- Opens an existing window instead of creating duplicate instances
- Switches to the source app icon and flashes when unread activity is detected
- Automatically discovers 24 popular domestic and international apps
- Manual app picker for software that is not yet in the built-in catalog
- Automatically pins WorkChat Dock and moves managed app icons into tray overflow on Windows 11
- Local JSON configuration with no hard-coded installation paths
- Windows 10/11 high-DPI support
- Native Apple Silicon macOS implementation in SwiftUI/AppKit

## Built-in App Catalog

WeChat, WeCom, QQ, TIM, DingTalk, Feishu/Lark, JDME, Tencent Meeting, Microsoft Teams, Slack, Zoom Workplace, Cisco Webex, Outlook, Notion, Telegram, WhatsApp, Signal, Discord, LINE, Viber, KakaoTalk, Mattermost, Rocket.Chat, and Zalo.

If an app is missing, open the control panel, choose **Add App**, and select its executable.

## Windows

Run the self-contained executable:

```powershell
.\dist\WorkChatDock\WorkChatDock.exe
```

Commands:

```text
WorkChatDock.exe                 Open the control panel
WorkChatDock.exe --launch-all    Launch all enabled apps
WorkChatDock.exe --background    Start in the tray
WorkChatDock.exe --smoke-test    Run the local smoke test
```

Configuration is stored at:

```text
%LOCALAPPDATA%\WorkChatDock\config.json
```

Build from source with .NET 8:

```powershell
dotnet build .\WorkChatDock.sln -c Release
dotnet run --project .\WorkChatDock.SmokeTests\WorkChatDock.SmokeTests.csproj -c Release
.\publish.ps1
```

## macOS — Apple Silicon

The native Swift 5.9 implementation supports macOS 13+ on M1/M2/M3/M4:

```zsh
cd WorkChatDockMac
chmod +x build-macos.sh
./build-macos.sh
```

Outputs:

```text
WorkChatDockMac/dist/WorkChat Dock.app
WorkChatDockMac/dist/WorkChatDock-macOS-arm64.zip
```

The included GitHub Actions workflow can build the arm64 artifact on a `macos-14` runner.

## Privacy

WorkChat Dock runs locally. It does not upload chat content or account data. Windows notification monitoring stores only app/notification identifiers and counts. The macOS version optionally uses Accessibility permission to detect unread numbers exposed in window titles.

## Project Layout

```text
WorkChatDock/             Windows WPF application
WorkChatDock.SmokeTests/  Dependency-free smoke tests
WorkChatDockMac/          Native macOS status-bar application
.github/workflows/        Apple Silicon CI build
```

## License

[MIT](LICENSE)
