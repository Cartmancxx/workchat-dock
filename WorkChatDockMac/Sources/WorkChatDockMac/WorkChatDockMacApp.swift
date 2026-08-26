import AppKit
import SwiftUI

@main
struct WorkChatDockMacApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        Settings {
            EmptyView()
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let model = DockModel()
    private var statusBarController: StatusBarController?
    private var settingsWindow: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        statusBarController = StatusBarController(model: model) { [weak self] in
            self?.showSettings()
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    private func showSettings() {
        if settingsWindow == nil {
            let controller = NSHostingController(rootView: SettingsView(model: model))
            let window = NSWindow(contentViewController: controller)
            window.title = "WorkChat Dock 设置"
            window.styleMask = [.titled, .closable, .miniaturizable, .resizable]
            window.setContentSize(NSSize(width: 760, height: 620))
            window.center()
            window.isReleasedWhenClosed = false
            settingsWindow = window
        }
        NSApp.activate(ignoringOtherApps: true)
        settingsWindow?.makeKeyAndOrderFront(nil)
    }
}
