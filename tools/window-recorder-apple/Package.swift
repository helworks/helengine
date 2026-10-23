// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "HelengineWindowRecorderApple",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "helengine-window-recorder", targets: ["WindowRecorderCLI"])],
    dependencies: [.package(path: "../../engine/helengine.recording.apple")],
    targets: [
        .target(name: "RecorderCommand"),
        .executableTarget(name: "WindowRecorderCLI", dependencies: ["RecorderCommand", .product(name: "HelengineRecordingApple", package: "helengine.recording.apple")]),
        .testTarget(name: "RecorderCommandTests", dependencies: ["RecorderCommand"])
    ]
)
