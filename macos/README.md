# QuickTrans macOS

这是 QuickTrans 的原生 macOS 菜单栏客户端，支持 macOS 13+。

## 运行

使用 Xcode 打开 `QuickTransMac` 目录，或执行：

```bash
cd QuickTransMac
swift run -c release
```

打包为可双击运行的 `QuickTrans.app`：

```bash
cd ..
./build-app.sh
open dist/QuickTrans.app
```

脚本需要 Xcode 15+，会把应用放到 `macos/dist/QuickTrans.app`。

首次使用请在“系统设置 → 隐私与安全性 → 辅助功能”中授权当前终端或应用控制其他 App。按 `⌥Q` 读取当前选中文本并显示译文；点击菜单栏 `QT` 可配置 OpenAI 兼容接口。

在设置中勾选“登录 macOS 时自动启动”并保存后，登录时应用会静默驻留菜单栏，不弹出设置窗口。手动启动应用仍会打开设置；后台运行时可从菜单栏选择“设置”。

打包脚本会为完整应用进行本地 ad-hoc 签名并验证。当前版本没有 Developer ID 签名和 Apple 公证，经微信或浏览器下载后仍可能被 macOS 拦截。将应用拖到“应用程序”后，可在“系统设置 → 隐私与安全性”中选择“仍要打开”。如果系统仅提示“已损坏”且没有该选项，确认使用的是自己信任的安装包后，在终端执行以下命令再打开：

```bash
xattr -dr com.apple.quarantine /Applications/QuickTrans.app
open /Applications/QuickTrans.app
```
