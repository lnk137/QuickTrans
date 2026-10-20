<div align="center">
  <img src="assets/QuickTrans.png" width="112" height="112" alt="QuickTrans Logo" />

  <h1>QuickTrans</h1>
  <p><strong>选中文字，按下快捷键，继续阅读。</strong></p>
  <p>面向 Windows 与 macOS 的轻量划词翻译工具，让译文出现在光标附近。</p>

  <p>
    <img src="https://img.shields.io/badge/Windows-10%2022H2%2B-0078D4?style=flat-square" alt="Windows 10 22H2 and later" />
    <img src="https://img.shields.io/badge/macOS-13%2B-111111?style=flat-square" alt="macOS 13 and later" />
    <img src="https://img.shields.io/badge/.NET-8-512BD4?style=flat-square" alt=".NET 8" />
    <img src="https://img.shields.io/badge/Swift-5.9%2B-F05138?style=flat-square" alt="Swift 5.9 and later" />
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT license" /></a>
    <a href="https://github.com/lnk137/QuickTrans/actions/workflows/windows.yml"><img src="https://github.com/lnk137/QuickTrans/actions/workflows/windows.yml/badge.svg" alt="Windows build and tests" /></a>
    <a href="https://github.com/lnk137/QuickTrans/actions/workflows/macos.yml"><img src="https://github.com/lnk137/QuickTrans/actions/workflows/macos.yml/badge.svg" alt="macOS build" /></a>
    <a href="https://github.com/lnk137/QuickTrans/stargazers"><img src="https://img.shields.io/github/stars/lnk137/QuickTrans?style=flat-square" alt="GitHub stars" /></a>
  </p>

  <p>
    <a href="#快速开始">快速开始</a> ·
    <a href="#平台与功能">平台与功能</a> ·
    <a href="#从源码构建">从源码构建</a> ·
    <a href="#隐私与配置">隐私与配置</a> ·
    <a href="https://github.com/lnk137/QuickTrans/issues">反馈问题</a>
  </p>
</div>

---

阅读外文资料时，不必来回复制、切换窗口。QuickTrans 常驻托盘或菜单栏，通过全局快捷键读取当前选区，在附近显示翻译卡片，用完点击外部即可关闭。

- **随手翻译** — Windows 使用 `Alt+Q`，macOS 使用 `⌥Q`。
- **连接自己的服务** — 支持 OpenAI 兼容接口；Windows 还支持百度翻译和阿里云机器翻译。
- **中英双向** — 自动判断翻译方向，单次最多处理 2,000 个字符。
- **按需解释** — AI 句段翻译支持进一步解释，也可复制译文。
- **轻量常驻** — 支持登录启动；macOS 自启动时静默运行，不弹出设置窗口。

## 快速开始

### 1. 安装或构建

目前仓库尚未发布 GitHub Release 安装包，请按下方[源码构建指南](#从源码构建)生成应用。已获得 macOS DMG 的用户，可将其中的 `QuickTrans.app` 拖入“应用程序”，推出磁盘映像后再启动。

| 平台 | 系统要求 | 客户端 |
| --- | --- | --- |
| Windows | Windows 10 22H2 / Windows 11，x64 | .NET 8 / WPF |
| macOS | macOS 13+ | Swift / SwiftUI / AppKit |

macOS 预编译包目前面向 Apple 芯片；其他架构请使用对应设备从源码构建。

### 2. 配置翻译服务

打开设置，填写服务商的 Base URL、API Key 和模型名，点击“测试连接”，成功后保存。Windows 也可选择百度或阿里云并填写对应凭据。

Base URL 应包含服务商要求的 API 前缀；客户端会追加 `/chat/completions`。例如：

| 服务 | Base URL |
| --- | --- |
| OpenAI | `https://api.openai.com/v1` |
| DeepSeek | `https://api.deepseek.com` |
| 其他兼容服务 | 使用服务商提供的地址与模型名 |

需要自行准备 API 凭据，服务费用和模型可用性由对应服务商决定。

### 3. 选中并翻译

1. 在浏览器、文档或其他可复制文本的应用中选中内容。
2. 按 `Alt+Q`（Windows）或 `⌥Q`（macOS）。
3. 在光标附近查看译文；需要时复制或请求解释，点击外部关闭卡片。

macOS 首次使用需要在“系统设置 → 隐私与安全性 → 辅助功能”中授权 QuickTrans。从终端运行时，应授权对应终端。设置入口位于托盘或菜单栏菜单中。

## 平台与功能

两个客户端分别使用平台原生技术实现，当前功能范围有所不同。

| 功能 | Windows | macOS |
| --- | --- | --- |
| 全局快捷键划词翻译 | `Alt+Q`，可配置 | `⌥Q` |
| OpenAI 兼容接口 | 支持流式输出 | 完整响应后显示 |
| 百度 / 阿里云翻译 | 支持 | 暂不支持 |
| 中英自动方向 / 自定义提示词 | 支持 | 支持 |
| 复制译文 / AI 句段解释 | 支持 | 支持 |
| 登录启动 | 支持 | 支持，静默驻留菜单栏 |
| 主题 | 跟随系统、浅色、深色、自定义配色 | 使用系统原生样式 |
| 取词方式 | UI Automation，剪贴板兜底 | 模拟复制选区，需要辅助功能权限 |
| 内存翻译缓存 | 支持 | 暂不支持 |

当前没有 OCR、截图翻译、文档翻译、离线模型或翻译历史。受保护页面、禁用复制的内容和部分自绘控件可能无法取词。

## 从源码构建

```bash
git clone https://github.com/lnk137/QuickTrans.git
cd QuickTrans
```

### macOS

安装完整的 Xcode 15+，并确保 `xcode-select` 指向 Xcode 的开发目录。

```bash
# 编译并运行
cd macos/QuickTransMac
swift run -c release
```

在仓库根目录执行以下命令，生成可安装应用：

```bash
./macos/build-app.sh
open macos/dist/QuickTrans.app
```

产物位于 `macos/dist/QuickTrans.app`。打包脚本会为完整应用进行本地 ad-hoc 签名并验证。

<details>
<summary><strong>macOS 提示无法验证开发者或“已损坏”怎么办？</strong></summary>

当前构建没有 Developer ID 签名和 Apple 公证，通过微信或浏览器下载后可能被系统拦截。

先将应用安装到“应用程序”，再尝试从“系统设置 → 隐私与安全性”中选择“仍要打开”。若仍提示“已损坏”，确认安装包来源可信后，可仅移除该应用的下载隔离标记：

```bash
xattr -dr com.apple.quarantine /Applications/QuickTrans.app
open /Applications/QuickTrans.app
```

无需关闭系统全局安全检查。安装与权限说明见 [macOS 文档](macos/README.md)。

</details>

### Windows

需要 Windows 与 .NET 8 SDK。运行：

```powershell
cd windows
dotnet restore QuickTrans.sln
dotnet build QuickTrans.sln -c Release
dotnet test tests\QuickTrans.Tests\QuickTrans.Tests.csproj -c Release
dotnet publish src\QuickTrans\QuickTrans.csproj -c Release -r win-x64 --self-contained true
```

以上命令在 `windows/` 中运行。生成的程序位于仓库的 `windows/src/QuickTrans/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`。如需安装器，使用 Inno Setup 6+ 编译 `windows/installer/QuickTrans.iss`，产物输出到 `windows/outputs/`；默认按当前用户安装，无需管理员权限。详见 [Windows 文档](windows/README.md)。

<details>
<summary><strong>配置可选的版本检查</strong></summary>

发布时传入版本清单地址：

```powershell
dotnet publish src\QuickTrans\QuickTrans.csproj -c Release -r win-x64 --self-contained true -p:QuickTransUpdateManifestUrl=https://example.com/update.json
```

此命令同样在 `windows/` 中运行。清单格式参考 [update-manifest.example.json](windows/update-manifest.example.json)。版本检查不会自动安装更新。

</details>

## 隐私与配置

- 翻译会将选中文本发送到你配置的服务商；“解释”和“测试连接”也会请求对应 API。
- 不保存翻译历史，不采集周边上下文，不包含遥测。
- API 凭据以**明文**保存在本地配置文件中，不要分享该文件，也不要将其上传到 Issue 或 Git。
- 配置修改后需要在设置中保存。

| 平台 | 默认配置位置 |
| --- | --- |
| Windows | `%LocalAppData%\QuickTrans\config.json` |
| macOS | `~/Library/Application Support/QuickTrans/config.json` |

Windows 可通过 `QUICKTRANS_CONFIG_DIR` 环境变量覆盖配置目录，用于测试或隔离运行。

## 项目结构

```text
QuickTrans/
├── windows/                  # Windows 独立工程与说明
│   ├── src/QuickTrans/        # C# / WPF 客户端
│   ├── tests/QuickTrans.Tests/ # Windows 自动化测试
│   ├── installer/            # Inno Setup 安装脚本
│   └── QuickTrans.sln        # Windows 解决方案
├── macos/                    # macOS 独立工程与说明
│   ├── QuickTransMac/        # Swift / SwiftUI 客户端
│   └── build-app.sh          # 打包与签名校验
├── assets/                   # 两端共用的图标资源
├── tools/                    # 资源生成工具
├── AGENTS.md                 # Agent 工作约束与提交规范
└── HANDOFF.md                # Windows 架构、历史记录与已知限制
```

两个客户端位于同一仓库、同一主分支中，各自构建与测试；目前共享图标资源，业务逻辑分别实现，不依赖额外的公共运行时。

GitHub Actions 根据路径触发平台检查：Windows 执行构建、自动化测试与自包含发布验证，macOS 执行应用打包与签名校验；共享图标变更会触发两端检查。

## 参与贡献

欢迎通过 [Issues](https://github.com/lnk137/QuickTrans/issues) 反馈问题，或提交聚焦单一改动的 Pull Request。反馈时请附上系统版本、客户端版本、复现步骤和脱敏后的错误信息。

提交前请阅读 [AGENTS.md](AGENTS.md)，运行与改动相关的检查，并更新受影响的文档。Commit 标题使用 Emoji 和类型前缀，**标题、正文、尾注全部使用英文**：

```text
🐛 fix(macos): keep settings hidden when launched at login
📚 docs: improve the installation guide
```

可启用仓库提供的提交模板：

```bash
git config --local commit.template .gitmessage
```

架构与开发历史见 [HANDOFF.md](HANDOFF.md)，平台细节见 [Windows 文档](windows/README.md)及 [macOS 文档](macos/README.md)。

## 许可证

本项目采用 [MIT License](LICENSE)。
