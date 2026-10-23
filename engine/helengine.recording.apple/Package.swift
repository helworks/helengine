// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "HelengineRecordingApple",
    platforms: [.macOS(.v14)],
    products: [.library(name: "HelengineRecordingApple", targets: ["HelengineRecordingApple"])],
    targets: [
        .target(name: "HelengineRecordingApple"),
        .testTarget(name: "HelengineRecordingAppleTests", dependencies: ["HelengineRecordingApple"])
    ],
    swiftLanguageVersions: [.v5]
)
