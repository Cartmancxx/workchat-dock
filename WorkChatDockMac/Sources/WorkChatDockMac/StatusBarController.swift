import AppKit
import Combine
import SwiftUI

@MainActor
final class StatusBarController: NSObject {
    private let model: DockModel
    private let onOpenSettings: () -> Void
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    private let popover = NSPopover()
    private var trackingArea: NSTrackingArea?
    private var hideWorkItem: DispatchWorkItem?
    private var cancellables = Set<AnyCancellable>()

    init(model: DockModel, onOpenSettings: @escaping () -> Void) {
        self.model = model
        self.onOpenSettings = onOpenSettings
        super.init()

        popover.behavior = .semitransient
        popover.animates = true
        popover.contentViewController = NSHostingController(rootView: DockPopoverView(
            model: model,
            onHoverChanged: { [weak self] hovering in
                hovering ? self?.cancelHide() : self?.scheduleHide()
            },
            onOpenSettings: { [weak self] in self?.onOpenSettings() }
        ))

        if let button = statusItem.button {
            button.imagePosition = .imageOnly
            button.target = self
            button.action = #selector(statusItemClicked)
            let area = NSTrackingArea(
                rect: .zero,
                options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                owner: self,
                userInfo: nil
            )
            button.addTrackingArea(area)
            trackingArea = area
        }

        model.$menuIcon.sink { [weak self] image in
            self?.statusItem.button?.image = image
        }.store(in: &cancellables)
        model.$apps.sink { [weak self] apps in
            let count = apps.filter { $0.enabled && $0.executablePath != nil }.count
            self?.popover.contentSize = NSSize(width: 202, height: min(410, 48 + count * 36))
        }.store(in: &cancellables)
    }

    @objc private func statusItemClicked() {
        if let unread = model.currentUnreadApp {
            Task { await model.launch(id: unread.id) }
        } else if popover.isShown {
            popover.performClose(nil)
        } else {
            showPopover()
        }
    }

    @objc func mouseEntered(with event: NSEvent) {
        cancelHide()
        showPopover()
    }

    @objc func mouseExited(with event: NSEvent) {
        scheduleHide()
    }

    private func showPopover() {
        guard !popover.isShown, let button = statusItem.button else { return }
        popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
    }

    private func scheduleHide() {
        cancelHide()
        let work = DispatchWorkItem { [weak self] in self?.popover.performClose(nil) }
        hideWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.7, execute: work)
    }

    private func cancelHide() {
        hideWorkItem?.cancel()
        hideWorkItem = nil
    }
}
