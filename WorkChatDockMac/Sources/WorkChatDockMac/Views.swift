import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct DockPopoverView: View {
    @ObservedObject var model: DockModel
    let onHoverChanged: (Bool) -> Void
    let onOpenSettings: () -> Void

    var body: some View {
        VStack(spacing: 3) {
            if model.activeApps.isEmpty {
                Text("未识别到已启用的软件")
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .padding(.vertical, 10)
            } else {
                ScrollView {
                    LazyVStack(spacing: 2) {
                        ForEach(model.activeApps) { app in
                            Button {
                                Task { await model.launch(id: app.id) }
                            } label: {
                                HStack(spacing: 7) {
                                    Image(nsImage: icon(for: app))
                                        .resizable()
                                        .scaledToFit()
                                        .frame(width: 23, height: 23)
                                    Text(app.displayName)
                                        .font(.system(size: 12, weight: .semibold))
                                        .lineLimit(1)
                                    Spacer(minLength: 3)
                                    Circle()
                                        .fill(app.isRunning ? Color.green : Color.gray.opacity(0.55))
                                        .frame(width: 6, height: 6)
                                    if app.unreadCount > 0 {
                                        Text(app.unreadCount > 99 ? "99+" : "\(app.unreadCount)")
                                            .font(.system(size: 9, weight: .bold))
                                            .foregroundStyle(.white)
                                            .padding(.horizontal, 5)
                                            .frame(height: 17)
                                            .background(Color.red, in: Capsule())
                                    }
                                }
                                .contentShape(Rectangle())
                                .padding(.horizontal, 7)
                                .frame(height: 34)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
                .frame(maxHeight: 360)
            }

            Divider()
            HStack(spacing: 4) {
                Button("全部启动") { model.launchAll() }
                Spacer()
                Button("设置") { onOpenSettings() }
                Button("退出") { NSApp.terminate(nil) }
            }
            .buttonStyle(.borderless)
            .font(.system(size: 11))
            .padding(.horizontal, 5)
            .frame(height: 29)
        }
        .padding(6)
        .frame(width: 202)
        .onHover(perform: onHoverChanged)
    }

    private func icon(for app: DockApp) -> NSImage {
        guard let path = app.executablePath else { return NSImage() }
        return NSWorkspace.shared.icon(forFile: path)
    }
}

struct SettingsView: View {
    @ObservedObject var model: DockModel

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("WorkChat Dock")
                        .font(.title2.weight(.semibold))
                    Text("Apple Silicon · 主流办公与聊天软件，一个入口")
                        .font(.system(size: 12))
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Button("重新扫描") { Task { await model.rescan() } }
                Button("添加应用…") { addApplication() }
                    .buttonStyle(.borderedProminent)
            }

            List {
                ForEach(model.apps) { app in
                    HStack(spacing: 11) {
                        Image(nsImage: icon(for: app))
                            .resizable()
                            .scaledToFit()
                            .frame(width: 30, height: 30)
                        VStack(alignment: .leading, spacing: 3) {
                            Text(app.displayName).font(.system(size: 13, weight: .semibold))
                            Text(app.executablePath ?? "尚未安装或未定位")
                                .font(.system(size: 10))
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                        Spacer()
                        if app.isCustom {
                            Button("移除") { model.removeCustomApplication(id: app.id) }
                                .buttonStyle(.borderless)
                        }
                        Toggle("", isOn: model.bindingForEnabled(id: app.id))
                            .labelsHidden()
                            .disabled(app.executablePath == nil)
                    }
                    .padding(.vertical, 3)
                }
            }
            .listStyle(.inset)

            HStack {
                Text(model.statusText)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                Spacer()
                if !model.accessibilityAllowed {
                    Button("允许消息状态检测") { model.requestAccessibility() }
                } else {
                    Label("辅助功能检测已开启", systemImage: "checkmark.circle.fill")
                        .font(.system(size: 11))
                        .foregroundStyle(.green)
                }
            }
        }
        .padding(20)
        .frame(minWidth: 720, minHeight: 560)
    }

    private func addApplication() {
        let panel = NSOpenPanel()
        panel.title = "选择聊天或办公应用"
        panel.allowedContentTypes = [.application]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        if panel.runModal() == .OK, let url = panel.url {
            model.addCustomApplication(url: url)
        }
    }

    private func icon(for app: DockApp) -> NSImage {
        guard let path = app.executablePath else {
            return NSImage(systemSymbolName: "app.dashed", accessibilityDescription: nil) ?? NSImage()
        }
        return NSWorkspace.shared.icon(forFile: path)
    }
}
