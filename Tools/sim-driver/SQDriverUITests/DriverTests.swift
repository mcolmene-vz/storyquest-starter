import XCTest

/// Lead's verification driver (PLAN-M1 L1). Not part of the game.
/// Drives any installed app by bundle id with a step script passed through the
/// environment, so battles can be played on the simulator from the terminal.
///
/// Environment (xcodebuild forwards TEST_RUNNER_* to the test process):
///   SQ_BUNDLE_ID  bundle id to activate (default com.vendoza.storyquest)
///   SQ_SCRIPT     steps separated by ';'
///       wait:<seconds>          sleep
///       tap:<nx>,<ny>           tap at normalized app coordinates (0..1, origin top-left)
///       taps:<nx>,<ny>,<count>  the same tap repeated with 0.3 s gaps (about 0.65 s apart in practice, so three
///                               taps span 1.3 s; for the 1 s difficulty corner use three tap: steps instead)
///       shot:<name>             attach a screenshot named <name> (exported from the xcresult)
///       relaunch                terminate and launch the app again
final class DriverTests: XCTestCase {

    func testScript() throws {
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["SQ_BUNDLE_ID"] ?? "com.vendoza.storyquest"
        let script = env["SQ_SCRIPT"] ?? "wait:3;shot:start"

        let app = XCUIApplication(bundleIdentifier: bundleId)
        app.activate()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 60), "app did not reach the foreground")

        for raw in script.split(separator: ";") {
            let step = raw.trimmingCharacters(in: .whitespaces)
            if step.isEmpty { continue }
            let parts = step.split(separator: ":", maxSplits: 1).map(String.init)
            let op = parts[0]
            let arg = parts.count > 1 ? parts[1] : ""
            switch op {
            case "wait":
                Thread.sleep(forTimeInterval: Double(arg) ?? 1)
            case "tap":
                let v = arg.split(separator: ",").compactMap { Double($0) }
                guard v.count == 2 else { XCTFail("bad tap \(arg)"); continue }
                app.coordinate(withNormalizedOffset: CGVector(dx: v[0], dy: v[1])).tap()
            case "taps":
                let v = arg.split(separator: ",").compactMap { Double($0) }
                guard v.count == 3 else { XCTFail("bad taps \(arg)"); continue }
                for _ in 0..<Int(v[2]) {
                    app.coordinate(withNormalizedOffset: CGVector(dx: v[0], dy: v[1])).tap()
                    Thread.sleep(forTimeInterval: 0.3)
                }
            case "shot":
                let shot = XCTAttachment(screenshot: XCUIScreen.main.screenshot())
                shot.name = arg.isEmpty ? "shot" : arg
                shot.lifetime = .keepAlways
                add(shot)
            case "relaunch":
                app.terminate()
                app.launch()
                _ = app.wait(for: .runningForeground, timeout: 60)
            default:
                XCTFail("unknown step \(step)")
            }
        }
    }
}
