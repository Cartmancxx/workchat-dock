# Contributing

Thanks for helping improve WorkChat Dock.

## Common contributions

- Add or correct application discovery hints
- Improve notification matching
- Test Windows or Apple Silicon macOS builds
- Refine translations, accessibility, or UI behavior

## Development

Windows requires .NET 8:

```powershell
dotnet build .\WorkChatDock.sln -c Release
dotnet run --project .\WorkChatDock.SmokeTests\WorkChatDock.SmokeTests.csproj -c Release
```

macOS requires Xcode Command Line Tools:

```zsh
cd WorkChatDockMac
swift build -c release --arch arm64
```

Please keep discovery definitions generic, avoid committing third-party icons, and do not include personal paths, chat content, tokens, or account data in tests or screenshots.
