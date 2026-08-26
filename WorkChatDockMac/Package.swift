// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "WorkChatDockMac",
    platforms: [.macOS(.v13)],
    products: [
        .executable(name: "WorkChatDockMac", targets: ["WorkChatDockMac"])
    ],
    targets: [
        .executableTarget(
            name: "WorkChatDockMac",
            path: "Sources/WorkChatDockMac"
        )
    ]
)
