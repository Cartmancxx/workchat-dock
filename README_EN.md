# WorkChat Dock

[简体中文](README.md) · [English](README_EN.md)

> An elegant tray organizer for work and messaging apps — built for e-commerce operators and multi-platform teams.

WorkChat Dock brings your workplace messengers into one compact system-tray entry. Launch everything with one click, hover to open a slim vertical dock, jump to the app with new messages, and keep the original tray icons tucked away.

## Downloads & Promo

- [Latest Windows x64 and macOS Apple Silicon builds](https://github.com/Cartmancxx/workchat-dock/releases/latest)
- [Watch the 1080p product promo](https://github.com/Cartmancxx/workchat-dock/releases/download/v2.0.0/WorkChatDock-Promo-BGM.mp4)
- [SFX-only version without BGM](https://github.com/Cartmancxx/workchat-dock/releases/download/v2.0.0/WorkChatDock-Promo-NoBGM.mp4)

The macOS artifact is compiled natively for arm64 on GitHub’s `macos-14` runner and ad-hoc signed. If Gatekeeper prompts on first launch, right-click the app in Finder and choose **Open**.

## Highlights

- One-click launch for every enabled work app
- A single aggregate tray icon with a compact hover flyout
- Opens an existing window instead of creating duplicate instances
- Switches to the source app icon and flashes when unread activity is detected
- Runs Windows toast and hidden-window-title detection in parallel, with a built-in alert test button
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

## Unread Detection

On Windows, `UserNotificationListener` results are merged with a two-second hidden-window-title poll. Common formats such as `(3) Feishu`, `DingTalk (12)`, `5 new messages`, and `8 条新消息` are recognized. The **Test Alert** button simulates an eight-second unread state so icon switching, flashing, and click-through can be verified independently from third-party notification settings.

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
