import AppKit
import ApplicationServices
import Combine
import Foundation
import SwiftUI

@MainActor
final class DockModel: ObservableObject {
    @Published private(set) var apps: [DockApp] = []
    @Published private(set) var menuIcon = NSImage()
    @Published var statusText = "正在自动识别软件…"
    @Published private(set) var accessibilityAllowed = false

    private var stateTimer: Timer?
    private var flashTimer: Timer?
    private var flashPhase = false
    private let configURL: URL
    private let unreadPattern = try! NSRegularExpression(
        pattern: #"(?:^|\s)[\(（\[]([0-9]{1,3})[\)）\]]"#
    )

    init() {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("WorkChatDock", isDirectory: true)
        configURL = support.appendingPathComponent("config.json")
        apps = loadAndMergeCatalog()
        refreshMenuIcon()

        Task {
            await rescan()
            refreshRuntimeState()
            startTimers()
        }
    }

    deinit {
        stateTimer?.invalidate()
        flashTimer?.invalidate()
    }

    var activeApps: [DockApp] {
        apps.filter { $0.enabled && $0.executablePath != nil }
    }

    var currentUnreadApp: DockApp? {
        activeApps.filter { $0.unreadCount > 0 }
            .max { $0.lastNotificationTime < $1.lastNotificationTime }
    }

    func bindingForEnabled(id: String) -> Binding<Bool> {
        Binding(
            get: { [weak self] in self?.apps.first(where: { $0.id == id })?.enabled ?? false },
            set: { [weak self] value in
                guard let self, let index = self.apps.firstIndex(where: { $0.id == id }) else { return }
                self.apps[index].enabled = value
                self.save()
                self.refreshMenuIcon()
            }
        )
    }

    func rescan() async {
        var found = 0
        for index in apps.indices {
            let wasFound = validApplicationPath(apps[index].executablePath) != nil
            if let path = discover(apps[index]) {
                apps[index].executablePath = path
                if !wasFound && !apps[index].isCustom {
                    apps[index].enabled = true
                }
                found += 1
            }
        }
        save()
        refreshRuntimeState()
        statusText = "已识别 \(found)/\(apps.count) 个软件"
        refreshMenuIcon()
    }

    func launchAll() {
        let ids = activeApps.map(\.id)
        Task {
            for id in ids {
                await launch(id: id)
                try? await Task.sleep(nanoseconds: 350_000_000)
            }
        }
    }

    func launch(id: String) async {
        guard let index = apps.firstIndex(where: { $0.id == id }) else { return }
        let app = apps[index]
        if let running = runningApplication(for: app) {
            running.activate(options: [.activateIgnoringOtherApps])
            acknowledge(id: id)
            return
        }

        guard let path = app.executablePath else {
            statusText = "未找到 \(app.displayName)，可在设置中手动添加"
            return
        }
        let url = URL(fileURLWithPath: path)
        if NSWorkspace.shared.open(url) {
            acknowledge(id: id)
        } else {
            statusText = "打开 \(app.displayName) 失败"
        }
    }

    func addCustomApplication(url: URL) {
        let bundle = Bundle(url: url)
        let displayName = (bundle?.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String)
            ?? (bundle?.object(forInfoDictionaryKey: "CFBundleName") as? String)
            ?? url.deletingPathExtension().lastPathComponent
        let bundleIdentifier = bundle?.bundleIdentifier
        var base = displayName.lowercased().map { $0.isLetter || $0.isNumber ? $0 : "-" }
        var id = String(base).trimmingCharacters(in: CharacterSet(charactersIn: "-"))
        if id.isEmpty { id = "custom-app" }
        var suffix = 2
        let original = id
        while apps.contains(where: { $0.id == id }) {
            id = "\(original)-\(suffix)"
            suffix += 1
        }

        apps.append(DockApp(
            id: id,
            displayName: displayName,
            accentHex: colorHex(for: displayName),
            bundleIdentifiers: bundleIdentifier.map { [$0] } ?? [],
            candidatePaths: [url.path],
            executablePath: url.path,
            enabled: true,
            isCustom: true
        ))
        save()
        refreshRuntimeState()
        refreshMenuIcon()
        statusText = "已添加 \(displayName)"
    }

    func removeCustomApplication(id: String) {
        guard let app = apps.first(where: { $0.id == id }), app.isCustom else { return }
        apps.removeAll { $0.id == id }
        save()
        refreshMenuIcon()
        statusText = "已移除 \(app.displayName)"
    }

    func requestAccessibility() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        accessibilityAllowed = AXIsProcessTrustedWithOptions(options)
        statusText = accessibilityAllowed ? "辅助功能权限已开启" : "请在系统设置中允许辅助功能访问"
    }

    private func startTimers() {
        accessibilityAllowed = AXIsProcessTrusted()
        stateTimer?.invalidate()
        stateTimer = Timer.scheduledTimer(withTimeInterval: 3, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.refreshRuntimeState() }
        }
        flashTimer?.invalidate()
        flashTimer = Timer.scheduledTimer(withTimeInterval: 0.62, repeats: true) { [weak self] _ in
            Task { @MainActor in
                guard let self else { return }
                self.flashPhase.toggle()
                self.refreshMenuIcon()
            }
        }
    }

    private func refreshRuntimeState() {
        accessibilityAllowed = AXIsProcessTrusted()
        for index in apps.indices {
            guard apps[index].enabled, apps[index].executablePath != nil else {
                apps[index].isRunning = false
                apps[index].unreadCount = 0
                continue
            }
            let running = runningApplication(for: apps[index])
            apps[index].isRunning = running != nil
            guard accessibilityAllowed, let running else {
                apps[index].unreadCount = 0
                continue
            }
            let count = unreadCount(in: windowTitles(processIdentifier: running.processIdentifier))
            if count > apps[index].unreadCount {
                apps[index].lastNotificationTime = Date()
            }
            apps[index].unreadCount = count
        }
        refreshMenuIcon()
    }

    private func acknowledge(id: String) {
        guard let index = apps.firstIndex(where: { $0.id == id }) else { return }
        apps[index].unreadCount = 0
        refreshMenuIcon()
    }

    private func runningApplication(for app: DockApp) -> NSRunningApplication? {
        for bundleIdentifier in app.bundleIdentifiers {
            if let running = NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier).first {
                return running
            }
        }
        guard let path = app.executablePath else { return nil }
        return NSWorkspace.shared.runningApplications.first {
            $0.bundleURL?.standardizedFileURL.path == URL(fileURLWithPath: path).standardizedFileURL.path
        }
    }

    private func discover(_ app: DockApp) -> String? {
        if let path = validApplicationPath(app.executablePath) { return path }
        for bundleIdentifier in app.bundleIdentifiers {
            if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleIdentifier) {
                return url.path
            }
        }
        for candidate in app.candidatePaths {
            if let path = validApplicationPath(candidate) { return path }
            let userPath = candidate.replacingOccurrences(of: "/Applications/", with: "~/Applications/")
            if let path = validApplicationPath(userPath) { return path }
        }
        return nil
    }

    private func validApplicationPath(_ path: String?) -> String? {
        guard let path else { return nil }
        let expanded = NSString(string: path).expandingTildeInPath
        var isDirectory: ObjCBool = false
        return FileManager.default.fileExists(atPath: expanded, isDirectory: &isDirectory) && isDirectory.boolValue
            ? expanded
            : nil
    }

    private func windowTitles(processIdentifier: pid_t) -> [String] {
        let application = AXUIElementCreateApplication(processIdentifier)
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(application, kAXWindowsAttribute as CFString, &value) == .success,
              let windows = value as? [AXUIElement] else { return [] }
        return windows.compactMap { window in
            var titleValue: CFTypeRef?
            guard AXUIElementCopyAttributeValue(window, kAXTitleAttribute as CFString, &titleValue) == .success else {
                return nil
            }
            return titleValue as? String
        }
    }

    private func unreadCount(in titles: [String]) -> Int {
        titles.reduce(0) { result, title in
            let range = NSRange(title.startIndex..<title.endIndex, in: title)
            let values = unreadPattern.matches(in: title, range: range).compactMap { match -> Int? in
                guard let swiftRange = Range(match.range(at: 1), in: title) else { return nil }
                return Int(title[swiftRange])
            }
            return max(result, values.max() ?? 0)
        }
    }

    private func refreshMenuIcon() {
        if let unread = currentUnreadApp, flashPhase,
           let path = unread.executablePath {
            menuIcon = alertIcon(for: path)
        } else {
            menuIcon = aggregateIcon()
        }
    }

    private func aggregateIcon() -> NSImage {
        let size = NSSize(width: 22, height: 22)
        let image = NSImage(size: size)
        image.lockFocus()
        NSColor(calibratedWhite: 0.12, alpha: 1).setFill()
        NSBezierPath(ovalIn: NSRect(x: 1, y: 1, width: 20, height: 20)).fill()
        let colors = Array(activeApps.prefix(4).map { NSColor(hex: $0.accentHex) }) +
            [.systemBlue, .systemIndigo, .systemGreen, .systemPink]
        let points = [NSPoint(x: 7.5, y: 14.5), NSPoint(x: 14.5, y: 14.5),
                      NSPoint(x: 7.5, y: 7.5), NSPoint(x: 14.5, y: 7.5)]
        for index in 0..<4 {
            colors[index].setFill()
            NSBezierPath(ovalIn: NSRect(x: points[index].x - 2.7, y: points[index].y - 2.7,
                                        width: 5.4, height: 5.4)).fill()
        }
        image.unlockFocus()
        image.isTemplate = false
        return image
    }

    private func alertIcon(for path: String) -> NSImage {
        let source = NSWorkspace.shared.icon(forFile: path)
        let image = NSImage(size: NSSize(width: 22, height: 22))
        image.lockFocus()
        source.draw(in: NSRect(x: 1, y: 1, width: 20, height: 20))
        NSColor.systemRed.setFill()
        NSBezierPath(ovalIn: NSRect(x: 14, y: 14, width: 7, height: 7)).fill()
        image.unlockFocus()
        image.isTemplate = false
        return image
    }

    private func loadAndMergeCatalog() -> [DockApp] {
        let stored: [DockApp]
        if let data = try? Data(contentsOf: configURL),
           let decoded = try? JSONDecoder().decode(DockConfiguration.self, from: data) {
            stored = decoded.apps
        } else {
            stored = []
        }

        var result = stored
        for builtIn in AppCatalog.defaults() {
            if let index = result.firstIndex(where: { $0.id == builtIn.id }) {
                result[index].displayName = builtIn.displayName
                result[index].accentHex = builtIn.accentHex
                result[index].bundleIdentifiers = Array(Set(result[index].bundleIdentifiers + builtIn.bundleIdentifiers))
                result[index].candidatePaths = Array(Set(result[index].candidatePaths + builtIn.candidatePaths))
                result[index].isCustom = false
            } else {
                result.append(builtIn)
            }
        }
        return result
    }

    private func save() {
        do {
            try FileManager.default.createDirectory(at: configURL.deletingLastPathComponent(),
                                                    withIntermediateDirectories: true)
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            try encoder.encode(DockConfiguration(apps: apps)).write(to: configURL, options: .atomic)
        } catch {
            statusText = "保存设置失败：\(error.localizedDescription)"
        }
    }

    private func colorHex(for value: String) -> String {
        let colors = ["#3B82F6", "#8B5CF6", "#06B6D4", "#10B981", "#F97316", "#EC4899"]
        return colors[abs(value.hashValue) % colors.count]
    }
}

private extension NSColor {
    convenience init(hex: String) {
        let value = UInt64(hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted), radix: 16) ?? 0x3B82F6
        self.init(
            red: CGFloat((value >> 16) & 0xFF) / 255,
            green: CGFloat((value >> 8) & 0xFF) / 255,
            blue: CGFloat(value & 0xFF) / 255,
            alpha: 1
        )
    }
}
