// swift-tools-version:6.0
import PackageDescription

let package = Package(
    name: "FlockBar",
    platforms: [.macOS(.v15)],
    dependencies: [
        // The app's updates (Updater.swift); build-app.sh embeds and signs the framework.
        .package(url: "https://github.com/sparkle-project/Sparkle", exact: "2.10.0"),
    ],
    targets: [
        // Models and the API client, kept free of UI so they can be unit tested.
        .target(name: "FlockKit"),
        .executableTarget(name: "FlockBar", dependencies: ["FlockKit", .product(name: "Sparkle", package: "Sparkle")]),
        .testTarget(name: "FlockKitTests", dependencies: ["FlockKit"]),
    ]
)
