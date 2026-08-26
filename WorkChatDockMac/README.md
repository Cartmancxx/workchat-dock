# WorkChat Dock for macOS（Apple Silicon）

原生 Swift 5.9 + SwiftUI/AppKit 状态栏版本，目标为 macOS 13 及以上和 M1/M2/M3/M4 系列芯片。

## 功能

- 自动识别 `/Applications`、`~/Applications` 与应用 Bundle ID
- 内置国内外主流办公/聊天软件目录
- 未收录应用可在设置中通过 `.app` 文件手动添加
- 状态栏单一聚合图标，鼠标移入后展开紧凑列表
- 一键启动、已运行应用前置、单项打开
- 辅助功能权限开启后，从窗口标题检测未读数字并闪烁来源图标
- 配置保存在 `~/Library/Application Support/WorkChatDock/config.json`

## 在 Apple Silicon Mac 上构建

安装 Xcode Command Line Tools 后执行：

```zsh
cd WorkChatDockMac
chmod +x build-macos.sh
./build-macos.sh
```

产物：

```text
dist/WorkChat Dock.app
dist/WorkChatDock-macOS-arm64.zip
```

首次启动后，在 **系统设置 → 隐私与安全性 → 辅助功能** 中允许 WorkChat Dock，即可启用窗口标题未读检测。普通应用不能读取其他应用的通知正文，本项目也不会保存或上传聊天内容。
