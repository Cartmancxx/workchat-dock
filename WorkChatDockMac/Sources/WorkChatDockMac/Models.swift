import Foundation

struct DockApp: Codable, Identifiable, Hashable {
    var id: String
    var displayName: String
    var accentHex: String
    var bundleIdentifiers: [String]
    var candidatePaths: [String]
    var executablePath: String?
    var enabled: Bool
    var isCustom: Bool

    var isRunning = false
    var unreadCount = 0
    var lastNotificationTime = Date.distantPast

    enum CodingKeys: String, CodingKey {
        case id, displayName, accentHex, bundleIdentifiers, candidatePaths
        case executablePath, enabled, isCustom
    }
}

struct DockConfiguration: Codable {
    var version = 1
    var apps: [DockApp]
}
