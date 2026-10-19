import AppKit
import SwiftUI
import Carbon.HIToolbox
import ApplicationServices
import Combine
import ServiceManagement

struct Settings: Codable {
    var baseURL = "https://api.openai.com/v1"
    var apiKey = ""
    var model = "gpt-4o-mini"
    var hotkey = "⌥Q"
    var customPrompt = ""
    var runAtStartup = false
    enum CodingKeys: String, CodingKey { case baseURL, apiKey, model, hotkey, customPrompt, runAtStartup }
    init() {}
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        baseURL = try c.decodeIfPresent(String.self, forKey: .baseURL) ?? "https://api.openai.com/v1"
        apiKey = try c.decodeIfPresent(String.self, forKey: .apiKey) ?? ""
        model = try c.decodeIfPresent(String.self, forKey: .model) ?? "gpt-4o-mini"
        hotkey = try c.decodeIfPresent(String.self, forKey: .hotkey) ?? "⌥Q"
        customPrompt = try c.decodeIfPresent(String.self, forKey: .customPrompt) ?? ""
        runAtStartup = try c.decodeIfPresent(Bool.self, forKey: .runAtStartup) ?? false
    }
}

final class Store: ObservableObject {
    @Published var settings: Settings
    private let url: URL
    init() {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("QuickTrans", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        url = dir.appendingPathComponent("config.json")
        if let data = try? Data(contentsOf: url), let value = try? JSONDecoder().decode(Settings.self, from: data) { settings = value } else { settings = Settings() }
    }
    func save() { if let data = try? JSONEncoder().encode(settings) { try? data.write(to: url, options: .atomic) } }
}

final class Translator: ObservableObject {
    @Published var source = ""
    @Published var result = ""
    @Published var busy = false
    @Published var error: String?
    @Published var testing = false
    @Published var testMessage: String?
    @Published var isTerm = false
    @Published var actionMessage: String?
    private func direction(for text: String) -> (source: String, target: String) {
        let chinese = text.unicodeScalars.filter { (0x3400...0x4DBF).contains($0.value) || (0x4E00...0x9FFF).contains($0.value) }.count
        let words = text.split { !$0.isLetter && !$0.isNumber }.filter { part in part.unicodeScalars.contains(where: { $0.isASCII && $0.properties.isAlphabetic }) }.count
        return chinese > 0 && chinese >= max(1, words) ? ("zh", "en") : ("en", "zh")
    }
    private func isTermLike(_ text: String, source: String) -> Bool {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if value.isEmpty || value.range(of: "[。！？!?;；.\\r\\n]", options: .regularExpression) != nil { return false }
        if source == "zh" { return value.count <= 12 }
        let words = value.split { !$0.isLetter && !$0.isNumber }
        return words.count > 0 && words.count <= 5
    }
    private func prompt(for text: String, settings: Settings, explain: Bool = false) -> String {
        let d = direction(for: text); let target = d.target == "zh" ? "简体中文" : "英文"
        if explain { return "你是简洁的双语语言助手。用\(target)解释给定文本的语义、关键表达和可能的语境。只输出解释，不复述任务，不使用标题，控制在三句话内。" }
        if !settings.customPrompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return settings.customPrompt.trimmingCharacters(in: .whitespacesAndNewlines) }
        if isTermLike(text, source: d.source) { return "你是精确的词语翻译器。把输入翻译为\(target)，只给出1到3个最常用释义，用中文分号分隔。不要音标、词性、例句、编号、引号或说明。" }
        return "你是精确、自然的翻译器。把输入翻译为\(target)，保留原有换行和语气。只输出译文，不要解释、标题、引号或其他内容。"
    }
    private func requestBody(settings: Settings, messages: [[String: String]], maxTokens: Int? = nil) -> [String: Any] {
        var body: [String: Any] = ["model": settings.model, "temperature": 0.1, "stream": false, "messages": messages]
        if let maxTokens { body["max_tokens"] = maxTokens }
        if settings.baseURL.localizedCaseInsensitiveContains("deepseek.com") {
            body["thinking"] = ["type": "disabled"]
        }
        return body
    }
    func translate(_ text: String, settings: Settings, explain: Bool = false) {
        source = text; result = ""; error = nil; actionMessage = nil; busy = true
        if !explain { isTerm = isTermLike(text, source: direction(for: text).source) }
        guard let url = URL(string: settings.baseURL.trimmingCharacters(in: CharacterSet(charactersIn: "/")) + "/chat/completions") else { error = "API 地址无效"; busy = false; return }
        var request = URLRequest(url: url); request.httpMethod = "POST"; request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Content-Type"); request.setValue("Bearer \(settings.apiKey)", forHTTPHeaderField: "Authorization")
        let body = requestBody(settings: settings, messages: [["role": "system", "content": prompt(for: text, settings: settings, explain: explain)], ["role": "user", "content": text]], maxTokens: explain ? 500 : 1000)
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)
        URLSession.shared.dataTask(with: request) { [weak self] data, response, _ in
            DispatchQueue.main.async {
                guard let self else { return }; self.busy = false
                guard let data, let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { self.error = "无法连接翻译服务"; return }
                if let message = ((json["choices"] as? [[String: Any]])?.first?["message"] as? [String: Any])?["content"] as? String { self.result = message.trimmingCharacters(in: .whitespacesAndNewlines) } else { self.error = (json["error"] as? [String: Any])?["message"] as? String ?? "接口未返回译文" }
            }
        }.resume()
    }

    func explain(settings: Settings) { guard !source.isEmpty, !isTerm else { return }; translate(source, settings: settings, explain: true) }
    func copyResult() { guard !result.isEmpty else { return }; NSPasteboard.general.clearContents(); NSPasteboard.general.setString(result, forType: .string); actionMessage = "已复制" }

    func testConnection(settings: Settings) {
        testing = true; testMessage = nil
        guard let url = URL(string: settings.baseURL.trimmingCharacters(in: CharacterSet(charactersIn: "/")) + "/chat/completions") else {
            testing = false; testMessage = "API 地址无效"; return
        }
        var request = URLRequest(url: url); request.httpMethod = "POST"; request.timeoutInterval = 15
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(settings.apiKey)", forHTTPHeaderField: "Authorization")
        let body = requestBody(settings: settings, messages: [["role": "user", "content": "Reply with OK"]], maxTokens: 8)
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)
        URLSession.shared.dataTask(with: request) { [weak self] data, response, _ in
            DispatchQueue.main.async {
                guard let self else { return }
                self.testing = false
                let status = (response as? HTTPURLResponse)?.statusCode ?? 0
                guard let data, let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                    self.testMessage = "连接失败（HTTP \(status)）"; return
                }
                if let content = ((json["choices"] as? [[String: Any]])?.first?["message"] as? [String: Any])?["content"] as? String,
                   !content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    self.testMessage = "连接成功"
                } else {
                    let message = (json["error"] as? [String: Any])?["message"] as? String
                    let hasReasoning = (((json["choices"] as? [[String: Any]])?.first?["message"] as? [String: Any])?["reasoning_content"] as? String)?.isEmpty == false
                    self.testMessage = message ?? (hasReasoning ? "接口返回了思考内容但没有正文，请检查模型或关闭思考模式" : "接口未返回有效内容（HTTP \(status)）")
                }
            }
        }.resume()
    }
}

struct SettingsView: View {
    @ObservedObject var store: Store
    @ObservedObject var translator: Translator
    let onSave: () -> Void
    var body: some View {
        Form {
            Section("OpenAI 兼容接口") { TextField("Base URL", text: $store.settings.baseURL); SecureField("API Key", text: $store.settings.apiKey); TextField("Model", text: $store.settings.model); TextField("自定义翻译提示词（可选）", text: $store.settings.customPrompt, axis: .vertical).lineLimit(2...5) }
            Section("快捷键") {
                Text("⌥Q（Option + Q，全局快捷键）")
                Text("首次取词请在“系统设置 → 隐私与安全性 → 辅助功能”中允许 QuickTrans 控制其他 App。").font(.caption).foregroundStyle(.secondary)
            }
            Toggle("登录 macOS 时自动启动", isOn: $store.settings.runAtStartup)
            HStack {
                Button(translator.testing ? "测试中…" : "测试连接") { translator.testConnection(settings: store.settings) }.disabled(translator.testing)
                if let message = translator.testMessage { Text(message).foregroundStyle(message == "连接成功" ? .green : .red) }
            }
            Button("保存") { onSave() }.keyboardShortcut(.defaultAction)
        }.padding(20).frame(width: 430)
    }
}

struct PopoverView: View {
    @ObservedObject var translator: Translator
    let store: Store
    var body: some View { VStack(alignment: .leading, spacing: 10) { if !translator.source.isEmpty { Text(translator.source).font(.subheadline).foregroundStyle(.secondary).lineLimit(4) }; if translator.busy { ProgressView() } else if let error = translator.error { Text(error).foregroundStyle(.red).fixedSize(horizontal: false, vertical: true) } else { Text(translator.result).textSelection(.enabled).fixedSize(horizontal: false, vertical: true); if !translator.result.isEmpty { HStack { Button("复制译文") { translator.copyResult() }; if !translator.isTerm { Button("解释") { translator.explain(settings: store.settings) } }; if let message = translator.actionMessage { Text(message).font(.caption).foregroundStyle(.secondary) } } } } }.padding(16).frame(width: 388, alignment: .leading).background(.regularMaterial).clipShape(RoundedRectangle(cornerRadius: 12)) }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    let store = Store(); let translator = Translator(); var status: NSStatusItem!; var settingsWindow: NSWindow?; var popover: NSPanel?; var globalKeyMonitor: Any?; var localKeyMonitor: Any?; var outsideMonitor: Any?; var outsideLocalMonitor: Any?; var popoverCancellable: AnyCancellable?; var popoverAnchor = NSEvent.mouseLocation
    var hotKey: EventHotKeyRef?; var eventHandler: EventHandlerRef?
    var lastHotkey = Date.distantPast
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Capture the launch event before dispatching work; it is only current during launch.
        let launchEvent = NSAppleEventManager.shared().currentAppleEvent
        let launchedAtLogin = launchEvent?.eventID == kAEOpenApplication &&
            launchEvent?.paramDescriptor(forKeyword: keyAEPropData)?.enumCodeValue == keyAELaunchedAsLogInItem
        installEditMenu()
        status = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let image = NSImage(named: "QuickTrans") { image.size = NSSize(width: 18, height: 18); image.isTemplate = false; status.button?.image = image } else { status.button?.title = "QT" }
        let menu = NSMenu(); menu.addItem(NSMenuItem(title: "设置", action: #selector(openSettings), keyEquivalent: ",")); menu.addItem(NSMenuItem(title: "测试快捷键 ⌥Q", action: #selector(testHotkey), keyEquivalent: "")); menu.addItem(NSMenuItem(title: "退出", action: #selector(quit), keyEquivalent: "q")); status.menu = menu
        registerHotkey()
        configureStartup()
        let handler: (NSEvent) -> Void = { [weak self] event in
            guard event.keyCode == UInt16(kVK_ANSI_Q), event.modifierFlags.contains(.option) else { return }
            self?.triggerHotkey()
        }
        globalKeyMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown, handler: handler)
        localKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in handler(event); return event }
        if !launchedAtLogin {
            DispatchQueue.main.async { self.openSettings() }
        }
    }
    func installEditMenu() {
        let main = NSMenu()
        let appItem = NSMenuItem(); let appMenu = NSMenu(); appItem.submenu = appMenu; main.addItem(appItem)
        appMenu.addItem(withTitle: "退出 QuickTrans", action: #selector(quit), keyEquivalent: "q")
        let editItem = NSMenuItem(); let edit = NSMenu(title: "编辑"); editItem.submenu = edit; main.addItem(editItem)
        edit.addItem(withTitle: "撤销", action: #selector(UndoManager.undo), keyEquivalent: "z")
        edit.addItem(withTitle: "重做", action: #selector(UndoManager.redo), keyEquivalent: "Z")
        edit.addItem(NSMenuItem.separator())
        edit.addItem(withTitle: "剪切", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        edit.addItem(withTitle: "复制", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        edit.addItem(withTitle: "粘贴", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        edit.addItem(withTitle: "全选", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        NSApp.mainMenu = main
    }
    func registerHotkey() {
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let callback: EventHandlerProcPtr = { _, _, userData in
            guard let userData else { return noErr }
            Unmanaged<AppDelegate>.fromOpaque(userData).takeUnretainedValue().triggerHotkey()
            return noErr
        }
        InstallEventHandler(GetApplicationEventTarget(), callback, 1, &spec, Unmanaged.passUnretained(self).toOpaque(), &eventHandler)
        let id = EventHotKeyID(signature: OSType(0x5154), id: 1)
        RegisterEventHotKey(UInt32(kVK_ANSI_Q), UInt32(optionKey), id, GetApplicationEventTarget(), 0, &hotKey)
    }
    func triggerHotkey() { guard Date().timeIntervalSince(lastHotkey) > 0.25 else { return }; lastHotkey = Date(); translateSelection() }
    @objc func testHotkey() { translator.source = "快捷键已触发"; translator.result = "现在可以使用 ⌥Q"; translator.error = nil; translator.busy = false; showPopover() }
    func translateSelection() {
        guard AXIsProcessTrusted() else { translator.busy = false; translator.error = "请在系统设置 → 隐私与安全性 → 辅助功能中重新添加 QuickTrans.app"; showPopover(); return }
        translator.source = "正在读取选中文本…"; translator.result = ""; translator.error = nil; translator.busy = true; showPopover()
        let pasteboard = NSPasteboard.general; let old = pasteboard.string(forType: .string); let before = pasteboard.changeCount
        let source = CGEventSource(stateID: .combinedSessionState); let down = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_ANSI_C), keyDown: true); let up = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(kVK_ANSI_C), keyDown: false); down?.flags = .maskCommand; up?.flags = .maskCommand; down?.post(tap: .cghidEventTap); up?.post(tap: .cghidEventTap)
        var attempts = 0
        func poll() {
            attempts += 1
            let text = pasteboard.string(forType: .string)
            if pasteboard.changeCount != before, let text, !text.isEmpty {
                translator.translate(String(text.prefix(2000)), settings: store.settings)
                if let old { pasteboard.clearContents(); pasteboard.setString(old, forType: .string) }
            } else if attempts < 30 {
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.05, execute: poll)
            } else {
                translator.busy = false; translator.error = "没有读取到选中文本，请确认目标 App 已允许辅助功能"
            }
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12, execute: poll)
    }
    func showPopover() {
        if popover == nil { popoverAnchor = NSEvent.mouseLocation }
        popover?.close(); removePopoverMonitors()
        let controller = NSHostingController(rootView: PopoverView(translator: translator, store: store)); let p = NSPanel(contentViewController: controller)
        p.styleMask = [.borderless, .nonactivatingPanel]; p.isFloatingPanel = true; p.level = .floating; p.hidesOnDeactivate = false; p.backgroundColor = .clear; p.isOpaque = false; p.hasShadow = true; p.contentView?.wantsLayer = true; p.setContentSize(NSSize(width: 380, height: 140))
        popover = p; positionPopover(); p.orderFrontRegardless()
        popoverCancellable = translator.objectWillChange.sink { [weak self] in DispatchQueue.main.async { self?.resizePopover() } }
        outsideMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown, .otherMouseDown]) { [weak self] _ in self?.dismissPopoverIfOutside() }
        outsideLocalMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown, .otherMouseDown, .keyDown]) { [weak self] event in if event.type == .keyDown && event.keyCode == 53 { self?.closePopover(); return nil }; self?.dismissPopoverIfOutside(); return event }
    }
    func positionPopover() { guard let p = popover else { return }; let screen = NSScreen.screens.first(where: { $0.frame.contains(popoverAnchor) }) ?? NSScreen.main; let visible = screen?.visibleFrame ?? NSRect(x: 0, y: 0, width: 800, height: 600); let size = p.frame.size; var origin = NSPoint(x: popoverAnchor.x + 14, y: popoverAnchor.y - size.height - 14); if origin.x + size.width > visible.maxX { origin.x = visible.maxX - size.width - 6 }; if origin.y < visible.minY { origin.y = min(popoverAnchor.y + 18, visible.maxY - size.height - 6) }; p.setFrameOrigin(origin) }
    func resizePopover() { guard let p = popover, let view = p.contentViewController?.view else { return }; let fitting = view.fittingSize; let screen = NSScreen.screens.first(where: { $0.frame.contains(popoverAnchor) }) ?? NSScreen.main; let maxHeight = (screen?.visibleFrame.height ?? 600) * 0.4; p.setContentSize(NSSize(width: 420, height: min(max(92, fitting.height), maxHeight))); positionPopover() }
    func dismissPopoverIfOutside() { guard let p = popover, !p.frame.contains(NSEvent.mouseLocation) else { return }; closePopover() }
    func removePopoverMonitors() { if let outsideMonitor { NSEvent.removeMonitor(outsideMonitor) }; if let outsideLocalMonitor { NSEvent.removeMonitor(outsideLocalMonitor) }; outsideMonitor = nil; outsideLocalMonitor = nil; popoverCancellable = nil }
    func closePopover() { removePopoverMonitors(); popover?.close(); popover = nil }
    @objc func openSettings() { let c = NSHostingController(rootView: SettingsView(store: store, translator: translator, onSave: { [weak self] in self?.saveSettings() })); let w = NSWindow(contentViewController: c); w.title = "QuickTrans 设置"; w.styleMask = [.titled, .closable]; w.center(); w.makeKeyAndOrderFront(nil); settingsWindow = w; NSApp.activate(ignoringOtherApps: true) }
    func saveSettings() { store.save(); configureStartup() }
    func configureStartup() { if store.settings.runAtStartup { try? SMAppService.mainApp.register() } else { try? SMAppService.mainApp.unregister() } }
    @objc func quit() { NSApp.terminate(nil) }
    func applicationWillTerminate(_ notification: Notification) { if let globalKeyMonitor { NSEvent.removeMonitor(globalKeyMonitor) }; if let localKeyMonitor { NSEvent.removeMonitor(localKeyMonitor) }; removePopoverMonitors(); if let hotKey { UnregisterEventHotKey(hotKey) }; if let eventHandler { RemoveEventHandler(eventHandler) } }
}

let app = NSApplication.shared; let delegate = AppDelegate(); app.delegate = delegate; app.setActivationPolicy(.accessory); app.run()
