# WorkChat Dock

[简体中文](README.md) · [English](README_EN.md)

> 最优雅的工作软件收纳小工具，电商人与多平台团队的桌面必备。

把国内外主流办公与聊天软件收进一个托盘入口：一键全部启动、鼠标移入纵向展开、自动索引安装位置，并在检测到新消息时切换和闪烁主图标。仓库同时包含 Windows 和 Apple Silicon macOS 两套原生实现。

## 下载与宣传片

- [下载最新版：Windows x64 / macOS Apple Silicon](https://github.com/Cartmancxx/workchat-dock/releases/latest)
- [观看 1080p 产品宣传片](https://github.com/Cartmancxx/workchat-dock/releases/download/v2.0.0/WorkChatDock-Promo-BGM.mp4)
- [无 BGM、保留音效版](https://github.com/Cartmancxx/workchat-dock/releases/download/v2.0.0/WorkChatDock-Promo-NoBGM.mp4)

macOS 成品由 GitHub Actions 在 `macos-14` 上原生编译为 arm64，并使用 ad-hoc 签名。首次打开若被 Gatekeeper 提示，请在 Finder 中右键应用并选择“打开”。

## 已实现

- 桌面生成 **“一键打开办公软件”** 快捷方式
- 已运行时恢复窗口，未运行时启动程序，避免重复打开主窗口
- 单个聚合托盘图标
- 鼠标移到聚合图标后，已安装且启用的软件沿纵轴紧凑展开
- 每个软件显示运行状态、未读状态和消息数量
- 新消息到来后，主图标切换为消息来源并闪烁
- 点击主图标直接打开最近产生消息的软件
- Windows 通知与隐藏窗口标题并行检测；控制面板提供“测试提醒”按钮
- 内置 24 个主流软件定义：微信、企业微信、QQ、TIM、钉钉、飞书/Lark、京ME、腾讯会议、Teams、Slack、Zoom、Webex、Outlook、Notion、Telegram、WhatsApp、Signal、Discord、LINE、Viber、KakaoTalk、Mattermost、Rocket.Chat、Zalo
- 从运行进程、App Paths、开始菜单快捷方式、卸载注册表和软件专用目录自动索引
- 未收录的软件可直接选择 exe 手动添加，也可移除自定义条目
- Windows 11 下自动固定 WorkChat Dock 图标，并把已接管软件的原托盘图标收进隐藏区
- JSON 配置，无硬编码安装路径
- 单实例和命名管道命令转发
- Windows 10/11 高 DPI 界面

## 直接运行

当前电脑可直接启动：

```powershell
.\dist\WorkChatDock\WorkChatDock.exe
```

首次运行会：

1. 自动定位内置目录中的已安装软件；
2. 创建本地配置；
3. 请求 Windows 通知读取权限；
4. 在通知区域保留一个聚合图标；
5. 自动收纳已接管客户端的原通知区域图标。

控制面板中的“桌面快捷方式”按钮用于创建一键启动入口。自动收纳使用 Windows 11 当前用户的通知区域设置；个别客户端重新注册图标后，程序会在下次启动或重新扫描时再次应用设置。

新安装时会自动启用首次识别到的软件；从旧版升级时保留原四项选择，其余识别结果展示在控制面板中，由用户勾选后才会加入“一键启动”和聚合弹层。

## 命令行

```text
WorkChatDock.exe                 打开控制面板
WorkChatDock.exe --launch-all    一键启动全部已启用软件
WorkChatDock.exe --background    后台启动，只显示托盘图标
WorkChatDock.exe --smoke-test    本地冒烟测试后退出
```

第二次运行会把命令发送给现有实例，不会产生第二个常驻进程。

## 配置

默认配置位置：

```text
%LOCALAPPDATA%\WorkChatDock\config.json
```

每个应用由下面的字段描述：

```json
{
  "Id": "example-chat",
  "DisplayName": "Example Chat",
  "AccentColor": "#3B82F6",
  "Keywords": ["Example Chat"],
  "ProcessNames": ["ExampleChat"],
  "ExecutableNames": ["ExampleChat.exe"],
  "NotificationNames": ["Example Chat"],
  "SearchRoots": ["%LOCALAPPDATA%\\Programs\\ExampleChat"],
  "ExecutablePath": null,
  "ExecutablePathIsManual": false,
  "Enabled": true,
  "IsCustom": false
}
```

新增聊天软件时，可在控制面板点击 **添加软件** 并选择 exe；也可以直接向 `Apps` 数组添加定义，然后点击“重新扫描”。

## 消息检测

程序并行使用 `UserNotificationListener` 与隐藏窗口标题检测，不会因为 Windows 返回了通知权限却没有返回桌面客户端通知而跳过后备检测。程序只保留应用 ID、通知 ID 和计数，不保存聊天正文，也不进行联网传输。

窗口检测支持 `(3) 飞书`、`钉钉 (12)`、`5 new messages`、`8 条新消息` 等常见格式，并以 2 秒间隔刷新。控制面板底部的 **测试提醒** 会依次模拟各软件的 8 秒提醒，可单独验证图标切换、闪烁和点击直达链路。部分客户端同时关闭系统通知且不暴露未读窗口状态时，检测精度仍取决于客户端自身能力。

## 从源码构建

要求：Windows 10/11、.NET 8 SDK。

```powershell
dotnet build .\WorkChatDock.sln -c Debug
dotnet run --project .\WorkChatDock\WorkChatDock.csproj
```

生成无需预装 .NET 的单文件版本：

```powershell
dotnet publish .\WorkChatDock\WorkChatDock.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o .\dist\WorkChatDock
```

运行内置冒烟测试：

```powershell
dotnet run --project .\WorkChatDock.SmokeTests\WorkChatDock.SmokeTests.csproj
```

## macOS Apple Silicon

`WorkChatDockMac/` 是 Swift 5.9 + SwiftUI/AppKit 的原生状态栏版本，支持 macOS 13+ 和 M1/M2/M3/M4：

```zsh
cd WorkChatDockMac
chmod +x build-macos.sh
./build-macos.sh
```

生成 `WorkChatDockMac/dist/WorkChat Dock.app` 和 `WorkChatDock-macOS-arm64.zip`。macOS 普通应用没有读取其他应用通知中心正文的接口，因此该版本在用户授权辅助功能后，以窗口标题中的未读数字作为本地检测信号。详见 `WorkChatDockMac/README.md`。

## 项目结构

```text
WorkChatDock/
  Models/          配置和应用定义
  Services/        索引、启动、消息、托盘、快捷方式和单实例
  ViewModels/      控制面板与浮层状态
  MainWindow.*     设置控制面板
  DockFlyout.*     托盘纵向展开菜单
WorkChatDock.SmokeTests/
  Program.cs       无第三方测试框架的冒烟测试
WorkChatDockMac/
  Sources/         macOS 原生状态栏、发现、启动、设置与未读检测
  build-macos.sh   Apple Silicon arm64 应用打包脚本
```

应用图标从本机已安装程序动态提取，仓库不分发第三方软件的图标文件。

## License

MIT
