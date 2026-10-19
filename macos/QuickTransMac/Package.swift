// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "QuickTransMac",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "QuickTransMac", targets: ["QuickTransMac"])],
    targets: [.executableTarget(name: "QuickTransMac")]
)
