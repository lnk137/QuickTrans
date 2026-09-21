# QuickTrans 项目交接与开发日志

最后更新：2026-09-21  
当前版本：0.1.8  
项目根目录：`E:\Dev\Projects\QuickTrans`
GitHub：`https://github.com/lnk137/QuickTrans`（私有仓库）

## 1. 项目定位

QuickTrans 是一款 Windows 托盘常驻的极简划词翻译工具。用户在其他应用中选中文字后按全局快捷键（默认 `Alt+Q`），程序立即在鼠标附近创建一个不主动抢焦点的翻译卡片，并调用选定的翻译服务。

当前目标用户是个人或小范围分发，不包含账户、商业化、同步、翻译历史、OCR、截图翻译和离线模型。

### 当前技术栈

| 项目 | 当前选择 |
| --- | --- |
| UI | .NET 8、WPF，少量 Windows Forms（托盘图标） |
| 目标框架 | `net8.0-windows10.0.19041.0` |
| 架构 | Windows x64 |
| 发布方式 | 自包含、单文件 EXE |
| 安装器 | Inno Setup 6，per-user 安装 |
| 测试 | xUnit 2.9.2 |
| 配置 | `%LocalAppData%\QuickTrans\config.json` |

当前发布的 `QuickTrans.exe` 约 179 MB，主要原因是自包含单文件中嵌入了 .NET 8 与 Windows Desktop/WPF 运行库。Inno Setup 压缩后的 0.1.8 安装包约 53 MB。这是部署策略的结果，不是业务代码本身达到该体积。

## 2. 用户使用流程

### 首次启动

1. 程序读取本地配置；不存在或损坏时使用默认设置。
2. 注册全局快捷键，默认是 `Alt+Q`。
3. `SetupCompleted` 为 `false` 时打开设置页。
4. 用户选择默认引擎并填写该引擎所需凭据。
5. 首次使用必须对默认引擎执行“测试连接”并成功，才能保存。
6. 保存后设置窗口关闭，程序继续在托盘常驻。
7. 首次设置窗口未保存就关闭时，应用会退出。

### 日常翻译

1. 用户在任意兼容应用中选中文字。
2. 按下全局快捷键。
3. 旧翻译卡片立即取消请求并销毁。
4. 程序在当前光标附近创建一张全新的加载卡片。
5. 先通过 UI Automation 读取选区；失败后等待快捷键释放，再模拟 `Ctrl+C`。
6. 剪贴板兜底完成后尽量恢复原剪贴板内容。
7. 规范化文本、检查空选区与 2,000 字符上限。
8. 自动判断中译英或英译中并发起翻译。
9. AI 结果以流式形式展示；机器翻译一次性展示。
10. 点击卡片外部会立即取消请求、关闭并销毁卡片。卡片获得焦点后按 `Esc` 也可关闭。

卡片内仅提供引擎切换、复制译文和 AI“解释”。切换引擎会取消当前请求并重新翻译；失败时不会自动转发给其他提供方。

## 3. 已实现功能细节

### 3.1 取词

实现位置：`src/QuickTrans/Services/TextSelectionService.cs`

- 优先读取 `AutomationElement.FocusedElement` 的 `TextPattern` 选区。
- UI Automation 最多读取 2,001 个字符，用于后续判断是否超限。
- UI Automation 不可用、元素失效、无权限或未返回文本时，回退到剪贴板方案。
- 剪贴板方案最多等待修饰键释放约 450 ms，避免 `Alt+Q` 尚未抬起时模拟出 `Alt+Ctrl+C`。
- 模拟复制后以 30 ms 间隔检查剪贴板序列号，最多 10 次，最长约 300 ms。
- 原剪贴板按格式快照并尽力恢复，包括文本、可复制流、WPF 位图和 GDI 位图。
- 延迟渲染、所有者渲染或无法物化的剪贴板格式会跳过，因此“恢复原剪贴板”是尽力而为，不是所有第三方格式的绝对保证。
- 当前未读取选区周边上下文，也没有 OCR。

### 3.2 文本限制与语言方向

实现位置：

- `src/QuickTrans/Services/TranslationRequestValidator.cs`
- `src/QuickTrans/Services/LanguageService.cs`

规则：

- 将 Windows 换行 `\r\n` 规范化为 `\n`，并去除首尾空白。
- 空文本直接报错。
- 单次最多 2,000 个 .NET 字符；超限报错，不截断、不拆分。
- 文本含中文字符且中文字符数不少于英文单词数时，方向为 `zh -> en`。
- 其他情况为 `en -> zh`。
- 首版只支持中文与英文两个目标方向。

短词模式：

- 包含 `。！？!?;；.` 或换行时，不视为短词。
- 中文不超过 12 个 Unicode rune 时视为短词。
- 英文匹配到 1–5 个单词时视为短词。
- 短词模式只影响 AI 提示词。百度和阿里通用文本翻译仍按各自接口返回普通译文，无法保证 1–3 个候选释义。

### 3.3 AI 翻译

实现位置：`src/QuickTrans/Services/OpenAiTranslationProvider.cs`

配置字段：

- `BaseUrl`：应填写 OpenAI 兼容 API 根地址，例如 `https://api.openai.com/v1`。
- `ApiKey`：Bearer Token。
- `Model`：服务端可用的模型名称。
- `CustomPrompt`：高级自定义翻译提示词。

协议行为：

- 请求地址是 `BaseUrl.TrimEnd('/') + "/chat/completions"`。
- 使用 `POST /chat/completions` 与 OpenAI Chat Completions 兼容 JSON。
- 翻译请求开启 `stream=true`，解析 SSE 的 `data:` 帧和 `choices[0].delta.content`。
- `temperature=0.1`。
- 普通翻译 `max_tokens=1000`；解释 `max_tokens=500`；测试连接 `max_tokens=32`。
- DeepSeek 官方端点 `api.deepseek.com` 会额外发送 `thinking: { type: "disabled" }`，减少翻译延迟并避免测试连接只有推理内容、没有正文。
- 其他 OpenAI 兼容端点不会收到 DeepSeek 专用字段。
- 连接超时为 8 秒；首个有效内容等待按每次读行 15 秒；后续流读取按每次读行 30 秒。
- SSE 格式错误的保活帧会忽略；全部无有效内容时返回安全错误。

内置默认提示词位于 `src/QuickTrans/Services/PromptBuilder.cs`：

- 短词：要求只给 1–3 个最常用释义，以中文分号分隔，不输出音标、词性、例句、编号或说明。
- 普通文本：只输出自然译文，保留换行和语气。
- 解释：用目标语言解释语义、关键表达和语境，不超过三句话。
- 自定义提示词只覆盖 `Translate` 模式；`Explain` 始终使用内置解释提示词。
- 提示词版本常量当前为 `v1`。修改内置提示词语义时必须同步提升此版本，防止命中旧缓存。

测试连接的“连接成功”表示接口确实返回了非空 `choices[0].message.content`。如果显示“接口连接成功，但没有返回可用内容”，通常是模型名错误、接口兼容性不足，或模型只返回了非标准字段；这种情况不算配置验证成功。

### 3.4 百度翻译

实现位置：`src/QuickTrans/Services/BaiduTranslationProvider.cs`

- 端点固定为 `https://fanyi-api.baidu.com/api/trans/vip/translate`。
- 使用 App ID、原文、随机 salt 和 Secret Key 生成 MD5 签名。
- 使用表单 POST。
- 从 `trans_result[*].dst` 读取结果，多段结果以换行连接。
- 配置字段是 `AppId` 与 `SecretKey`。
- 已映射请求超时、系统错误、鉴权失败、频率限制、额度不足和语言方向错误。
- 百度通用文本翻译 API 每个源片段只承诺一个 `dst`，不提供结构化的 1–3 个常用释义。

### 3.5 阿里云机器翻译

实现位置：`src/QuickTrans/Services/AliyunTranslationProvider.cs`

- 端点固定为 `https://mt.cn-hangzhou.aliyuncs.com/`。
- 调用 `TranslateGeneral`，版本 `2018-10-12`，场景 `general`。
- 使用排序参数、RFC 3986 风格编码和 HMAC-SHA1 生成 RPC 签名。
- 从 `Data.Translated` 读取结果。
- 配置字段是 `AccessKeyId` 与 `AccessKeySecret`。
- 已映射鉴权失败、限流、RAM 权限不足和额度不足。

### 3.6 请求取消、流式刷新与缓存

实现位置：

- `src/QuickTrans/ViewModels/PopoverViewModel.cs`
- `src/QuickTrans/Services/SessionTranslationCache.cs`

- 再次触发快捷键、关闭卡片或切换引擎时都会取消旧请求。
- AI SSE 小分片先在后台累积，最多每 50 ms 更新一次 WPF 属性，避免每个 token 都触发 UI 布局导致鼠标和窗口掉帧。
- 最终结果在会话内保存到 `ConcurrentDictionary`，应用退出后清空。
- 当前缓存键包含：提供方 ID、模型、源语言、目标语言、模式、短词标志、提示词版本和原文。
- 机器翻译的模型字段为空字符串。
- 缓存只保存成功且非空的最终结果。

### 3.7 翻译卡片生命周期与定位

实现位置：`src/QuickTrans/Views/PopoverWindow.xaml(.cs)`

- 卡片宽度固定为 420 DIP，高度按内容自适应。
- 窗口为无边框、透明、置顶、不显示在任务栏、默认不激活。
- 每次快捷键都创建新的 `PopoverWindow`；旧窗口会取消请求并 `Close()`，不会复用或平移旧窗口。
- 显示时先将透明度设为 0，在目标显示器上定位一次以触发正确 DPI，再计算高度和二次定位，最后恢复透明度，用于避免跨帧滑动。
- 最大窗口高度按鼠标所在屏幕工作区高度的 40% 计算，结果区域内部可滚动。
- 默认放在光标右下方；空间不足时移到光标上方，并限制在工作区边缘内。
- 通过 10 ms 定时器检测鼠标按钮由“未按下”到“按下”；点击落在窗口矩形外时立即关闭。
- 窗口失活时也会关闭；聚焦窗口后按 `Esc` 可关闭。
- 关闭操作使用原子标志保证幂等，避免重复关闭竞态。

### 3.8 设置、主题与托盘

实现位置：

- `src/QuickTrans/Views/SettingsWindow.xaml(.cs)`
- `src/QuickTrans/ViewModels/SettingsViewModel.cs`
- `src/QuickTrans/Services/ThemeService.cs`
- `src/QuickTrans/Services/TrayService.cs`

设置支持：

- 默认引擎与全局快捷键。
- 当前用户登录后启动。
- AI、百度和阿里云凭据及连接测试。
- 跟随 Windows、浅色、深色与自定义主题。
- 自定义主题包含 12 项颜色：窗口、内容、次级内容、输入框、主要文字、次要文字、边框、强调色、悬停强调色、弱强调背景、错误色和成功色。
- 自定义颜色必须是 `#RRGGBB`。
- 主题修改可实时预览；取消/关闭设置窗口时恢复已保存主题。
- 原生标题栏通过 DWM 属性同步背景、文字、边框和明暗模式。
- API 密钥输入框在界面上以密码形式遮罩。

托盘行为：

- 提示文字仅为 `QuickTrans`，不附带快捷键。
- 双击托盘图标打开设置。
- 菜单包含“设置”“暂停取词”“退出”。
- 暂停状态目前只存在于本次进程，不写入配置。

### 3.9 快捷键、单实例与开机启动

实现位置：

- `src/QuickTrans/Services/HotkeyService.cs`
- `src/QuickTrans/Services/StartupService.cs`
- `src/QuickTrans/App.xaml.cs`

- 使用 `RegisterHotKey` 注册系统级快捷键，并启用 `MOD_NOREPEAT`。
- 支持 `Ctrl`、`Alt`、`Shift`、`Win` 与普通键组合。
- 快捷键被占用时显示错误；启动时若保存的快捷键注册失败，会尝试回退到 `Alt+Q`。
- 通过本地命名互斥体 `Local\QuickTrans.App` 保证单实例。
- 开机启动写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名为 `QuickTrans`，无需管理员权限。
- 启动项写入的是当前进程 EXE 路径；开发调试版本不应随意开启，否则会把 `bin` 目录写入启动项。

### 3.10 配置、安全与隐私

实现位置：

- `src/QuickTrans/Models/AppSettings.cs`
- `src/QuickTrans/Services/ConfigStore.cs`
- `src/QuickTrans/Services/ProviderHttp.cs`

- 默认配置目录是 `%LocalAppData%\QuickTrans`。
- 可用环境变量 `QUICKTRANS_CONFIG_DIR` 覆盖配置目录，主要用于测试或隔离运行。
- 配置以缩进 JSON 明文保存，密钥不加密，这是产品已确认的取舍。
- 保存流程先写 `config.json.tmp`，再替换正式文件，降低中途写坏的概率。
- 配置 JSON 损坏或读取失败时回退到默认配置。
- 旧配置缺少主题字段时会自动补默认主题。
- HTTP 错误响应正文会被消费以复用连接，但不会显示给用户，防止服务端回显 API Key 或原文。
- 当前没有日志文件、遥测、翻译历史和持久化缓存。
- 不应在日志、错误报告、截图或提交中放入真实凭据和翻译正文。

配置结构示例（仅示意，不能填入真实密钥后提交）：

```json
{
  "DefaultProvider": "openai",
  "Hotkey": "Alt+Q",
  "RunAtStartup": false,
  "SetupCompleted": true,
  "Theme": {
    "Mode": "system",
    "CustomPalette": {
      "Window": "#10151B",
      "Surface": "#171E27",
      "SurfaceAlt": "#202A35",
      "Input": "#121820",
      "Text": "#EEF3F8",
      "MutedText": "#9BA8B7",
      "Border": "#2B3745",
      "Accent": "#69A7F5",
      "AccentHover": "#87BAF8",
      "AccentSubtle": "#1D3551",
      "Error": "#FF8A8A",
      "Success": "#67C394"
    }
  },
  "OpenAi": {
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "REDACTED",
    "Model": "MODEL_NAME",
    "CustomPrompt": ""
  },
  "Baidu": {
    "AppId": "",
    "SecretKey": ""
  },
  "Aliyun": {
    "AccessKeyId": "",
    "AccessKeySecret": ""
  }
}
```

### 3.11 版本检查

实现位置：`src/QuickTrans/Services/VersionCheckService.cs`

- 更新清单 URL 在构建时通过 `QuickTransUpdateManifestUrl` 写入程序集元数据。
- 未配置有效 URL 时静默禁用。
- 已完成首次设置的启动流程会等待约 1.5 秒，再在后台检查一次。
- 清单格式见 `update-manifest.example.json`，字段为 `version` 与 `downloadUrl`。
- 发现更高版本时仅询问是否打开下载页面，不静默下载、不自动安装。
- 网络或清单错误全部静默忽略，不得影响翻译。

## 4. 架构与代码导航

```text
QuickTrans/
├─ src/QuickTrans/
│  ├─ App.xaml(.cs)             应用资源、单实例和入口
│  ├─ Assets/                   Logo、PNG、ICO
│  ├─ Interop/NativeMethods.cs  Win32 热键、窗口、键盘和鼠标互操作
│  ├─ Models/                   配置和翻译领域模型
│  ├─ Services/                 取词、提供方、主题、托盘、启动项等
│  ├─ ViewModels/               设置页和翻译卡片状态/命令
│  └─ Views/                    WPF 设置窗口与翻译卡片
├─ tests/QuickTrans.Tests/      xUnit 单元及协议测试
├─ installer/QuickTrans.iss     Inno Setup 安装脚本
├─ tools/generate_icon_assets.py Logo 资源生成工具
├─ outputs/                     已生成安装包、源码包和校验值
├─ work/                        本机 SDK、Inno 与临时验证工具
├─ QuickTrans.sln
└─ README.md
```

核心调用链：

```text
WM_HOTKEY
  -> HotkeyService.Pressed
  -> AppController.OnHotkeyPressed
  -> 创建 PopoverWindow + PopoverViewModel
  -> 先显示加载卡片
  -> TextSelectionService 读取选区
  -> TranslationRequestValidator 校验
  -> PopoverViewModel 判断方向/短词
  -> ITranslationProvider.TranslateAsync
  -> 流式节流更新 UI
  -> SessionTranslationCache 保存最终结果
```

提供方统一接口：

```csharp
public interface ITranslationProvider
{
    ProviderDescriptor Descriptor { get; }
    bool IsConfigured { get; }
    Task ValidateConfigurationAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<TranslationChunk> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken);
}
```

增加新翻译引擎时，至少需要：

1. 在 `ProviderIds` 增加稳定 ID。
2. 在 `AppSettings` 增加配置模型。
3. 实现 `ITranslationProvider`。
4. 在 `AppController.CreateProviders()` 注册运行时提供方。
5. 在 `SettingsViewModel` 增加描述、配置绑定、测试命令和完整性判断。
6. 在 `SettingsWindow.xaml` 增加配置区域。
7. 增加协议解析、签名、错误脱敏和取消测试。
8. 明确是否支持解释与流式输出。

## 5. 构建、测试与打包

### 5.1 当前机器上的本地工具

项目已迁移到 `E:\Dev\Projects\QuickTrans`，所需便携工具也在项目内：

- .NET SDK：`work\.dotnet\dotnet.exe`
- 当前便携 SDK：8.0.424
- 当前便携运行时：8.0.30
- Inno Setup 编译器：`work\innosetup\ISCC.exe`
- Inno Setup 原安装包：`work\innosetup.exe`

当前系统级 `C:\Program Files\dotnet` 只有 .NET 7.0.20 x64 Runtime 与 Windows Desktop Runtime，没有 SDK。因此在这台电脑上应显式使用项目内的 `work\.dotnet\dotnet.exe`。

`work` 是本机工作目录，不应作为最终产品内容打包，也不建议放入未来的源码发布包。若以后建立 Git 仓库，应将 SDK、Inno 解压目录、构建输出、渲染截图和烟雾测试产物加入忽略规则，只保留必要的可复现脚本。

### 5.2 常用命令

在 PowerShell 中进入项目根目录：

```powershell
Set-Location -LiteralPath 'E:\Dev\Projects\QuickTrans'
$quickTransDotnet = '.\work\.dotnet\dotnet.exe'
```

首次或依赖变化后恢复：

```powershell
& $quickTransDotnet restore .\QuickTrans.sln
```

如果恢复或任何下载连续约 1 分钟没有明显进度，应停止命令，检查网络、NuGet 源或代理，并及时通知项目所有者，不要无限等待或反复重试。

构建和测试：

```powershell
& $quickTransDotnet build .\QuickTrans.sln -c Release --no-restore
& $quickTransDotnet test .\QuickTrans.sln -c Release --no-restore
```

发布自包含单文件：

```powershell
& $quickTransDotnet publish .\src\QuickTrans\QuickTrans.csproj `
  -c Release -r win-x64 --self-contained true --no-restore
```

带版本检查清单发布：

```powershell
& $quickTransDotnet publish .\src\QuickTrans\QuickTrans.csproj `
  -c Release -r win-x64 --self-contained true --no-restore `
  -p:QuickTransUpdateManifestUrl=https://example.com/update.json
```

编译 Inno 安装器：

```powershell
& '.\work\innosetup\ISCC.exe' '.\installer\QuickTrans.iss'
```

Inno 脚本读取：

`src\QuickTrans\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\QuickTrans.exe`

并输出到：

`outputs\QuickTrans-{版本}-win-x64-setup.exe`

### 5.3 安装行为

- 当前 Inno 默认安装目录：`%LocalAppData%\Programs\QuickTrans`。
- 安装范围是当前用户，`PrivilegesRequired=lowest`，不要求管理员权限。
- 可选创建桌面快捷方式。
- 安装完成后可直接启动应用。
- 卸载时询问是否保留 `%LocalAppData%\QuickTrans` 配置；选择“不保留”会删除凭据和设置。
- 旧截图或本机现有安装可能位于 `E:\Apps\Installed\Common\QuickTrans`，那不是当前 Inno 脚本的默认目录，可能来自手动安装路径或旧安装选择。

### 5.4 版本发布检查清单

1. 更新 `src/QuickTrans/QuickTrans.csproj` 的 `<Version>`。
2. 更新 `installer/QuickTrans.iss` 的 `MyAppVersion`。
3. 若修改内置提示词语义，更新 `PromptBuilder.PromptVersion`。
4. 运行全部自动化测试。
5. 执行 50 次窗口创建/销毁烟雾测试。
6. 发布自包含 `win-x64` 单文件。
7. 编译 Inno 安装器。
8. 在干净用户环境完成安装、升级、卸载和启动项验证。
9. 生成源码包时排除 `bin`、`obj`、`work`、真实配置和密钥。
10. 为实际发布文件重新生成 SHA-256；不要继续保留已删除文件的陈旧校验项。
11. 如启用更新检查，更新静态清单中的版本和下载地址。

### 5.5 Logo 资源

源图与生成物：

- `src/QuickTrans/Assets/QuickTrans-source.png`
- `src/QuickTrans/Assets/QuickTrans.png`
- `src/QuickTrans/Assets/QuickTrans.ico`

当前 Logo 保留白色底板，外轮廓是圆角矩形，圆角外区域透明，并带浅灰细边框以适配白色桌面。

生成脚本依赖 Pillow 和 NumPy：

```powershell
conda run -n base python .\tools\generate_icon_assets.py `
  .\src\QuickTrans\Assets\QuickTrans-source.png `
  .\src\QuickTrans\Assets
```

本项目约定需要 Python 时使用 conda base 环境。

## 6. 测试现状

### 自动化测试

截至 2026-09-15，`QuickTrans.Tests` 共 22 个测试用例，覆盖：

- 中文/英文方向判断。
- 中英文短词模式边界。
- 换行规范化与 2,000 字符限制。
- 短词默认提示词。
- 会话缓存按提供方和模式隔离。
- 12 项自定义颜色格式检查。
- 配置读写与旧配置主题兼容。
- OpenAI SSE 分片消费。
- DeepSeek 禁用思考字段。
- 通用 OpenAI 端点不携带厂商专用字段。
- 百度请求签名流程与响应解析。
- 阿里响应解析。
- HTTP 错误正文不暴露服务端回显内容。
- 500 个流式小分片合并为少量 UI 更新，防止 token 级刷新卡顿。

所有协议测试使用本地 `HttpMessageHandler` 桩，不使用真实密钥，也不会请求真实翻译服务。

### 辅助烟雾工具

`work\window-lifecycle-smoke` 会连续创建并销毁 50 个翻译窗口，期望结果：

```text
created=50 closed=50 remaining=0
```

运行方式：

```powershell
& '.\work\.dotnet\dotnet.exe' run `
  --project '.\work\window-lifecycle-smoke\window-lifecycle-smoke.csproj' `
  -c Release --no-restore
```

`work\render-ui` 是设置页离屏渲染辅助项目，可生成主题预览 PNG，但它不是正式自动化 UI 测试框架。

### 尚未形成自动化保证的项目

- UI Automation 成功路径与剪贴板回退的端到端测试。
- 富文本、图片及第三方私有剪贴板格式的完整恢复矩阵。
- 外部点击关闭、失活关闭和 `Esc` 关闭的系统级 UI 自动化。
- 多显示器、不同缩放比例与负坐标显示器定位。
- 明暗主题切换时所有原生控件的视觉回归。
- Edge、Chrome、Word、记事本、Edge PDF 和 Adobe Reader 的完整兼容矩阵。
- 热态卡片出现不超过 150 ms 的可重复性能测量。
- 空闲内存低于 120 MB 的自动化验收。
- 长时间运行的句柄、计时器、HttpClient 和窗口泄漏测试。
- 安装、覆盖升级、卸载与开机启动的自动化测试。
- 当前仓库没有 CI 配置。

## 7. 已知限制与接手优先事项

### P0：维护版本控制和建立可复现发布

项目已于 2026-09-21 初始化 Git，并以当前 0.1.8 源码建立 `main` 分支基线。此前开发阶段没有 Git，因此 0.1.0–0.1.7 仍然不存在可追溯的逐提交历史。后续应：

1. 持续检查并维护 `.gitignore`。
2. 确保配置、密钥、`bin`、`obj`、`work`、`outputs` 和本机截图不进入仓库。
3. 为正式发布版本建立签名标签和可审计的变更记录。
4. 将构建、测试、源码打包、安装器编译和校验文件生成脚本化。

### P0：修正缓存键的配置隔离

当前缓存键包含提供方、模型和提示词版本，但不包含：

- OpenAI `BaseUrl`。
- 自定义提示词内容或其哈希。

同一进程内如果用户更换兼容接口但沿用相同模型名，或修改自定义提示词后再次翻译同一原文，可能命中旧结果。建议把规范化后的 Base URL 和实际 system prompt 的哈希加入缓存键，或在成功保存提供方配置后清空会话缓存。

### P1：评估 UI Automation 的同步阻塞

`TryReadAutomationSelection()` 在 WPF UI 线程上同步调用。某些受限、繁忙或实现不佳的目标应用可能让 UI Automation 暂时阻塞，表现为翻译时鼠标或界面卡顿。建议：

- 对 UIA 调用引入独立 STA 线程与明确超时。
- 不要直接把 WPF `Clipboard` 调用移到 MTA；剪贴板回退仍需 STA。
- 加入慢 UIA 提供者测试和可取消边界测试。

### P1：降低外部点击监控频率或改为事件机制

卡片显示期间每 10 ms 轮询一次五个鼠标按键。窗口生命周期很短，当前开销通常可接受，但仍应通过 CPU 与鼠标流畅度数据验证。可考虑低级鼠标钩子、WinEvent、较低轮询频率或消息筛选，同时注意全局钩子的稳定性和安全软件兼容性。

### P1：严格化 AI 超时

当前“首包 15 秒”和“流中断 30 秒”实际按每次 `ReadLineAsync` 计时。持续收到空行或保活事件可能反复重置等待，不是严格的首个有效 token 总时限。可改为独立总计时器：

- 从响应头收到后到第一个有效 `delta.content` 总计 15 秒。
- 两个有效内容块之间总计 30 秒。

### P1：补齐选区和窗口端到端测试

目前单元/协议测试较好，但真实风险主要集中在 Windows UIA、剪贴板、DPI、窗口激活和第三方应用兼容性。接手后应优先把这些测试变成可重复脚本，并记录 Windows 版本、应用版本和缩放比例。

### P2：发布体积策略

当前自包含单文件约 179 MB，优势是用户无需安装 .NET。可选方案：

- 保持自包含并尝试 `EnableCompressionInSingleFile=true`，测量启动时间与最终体积。
- 改为 framework-dependent 后，客户端只需安装 Microsoft .NET 8 Desktop Runtime x64；但单个用户的总磁盘占用并不会显著下降。
- 不建议降级到已经停止支持的 .NET 7。
- .NET 8 支持期临近结束，后续应评估迁移到 .NET 10 LTS，并重新验证 Windows 10 22H2、WPF、单文件和安装器行为。

### P2：其他限制

- 自定义提示词完全替代翻译默认提示词，用户可能意外丢失目标语言、短词格式和“只输出译文”等约束；可改成“附加要求”或提供模板变量。
- OpenAI 流解析只处理 Chat Completions SSE 的 `choices[0].delta.content`，不兼容 Responses API 或厂商自定义事件结构。
- 百度与阿里端点目前硬编码；若服务区域、版本或签名协议变化需要发版。
- 设置页非首次使用时允许在未重新测试连接的情况下保存已修改的凭据，这是当前行为，不是强制验证。
- 暂停取词状态不持久化。
- 启动项状态只按保存动作写注册表，没有在设置页打开时读取注册表并校正漂移。
- `outputs\SHA256SUMS.txt` 仍列出 0.1.0–0.1.6 的源码包和部分当前目录中已不存在的文件。发布前应只对实际交付文件重新生成清单。
- 版本检查示例清单仍是占位地址，当前私有构建默认静默禁用更新检查。

## 8. 开发日志

由于当前项目没有 Git 历史，以下日志依据现有源码、构建产物、文件时间和开发过程记录整理。0.1.0–0.1.5 的精确逐版本差异无法可靠恢复，不应据此伪造详细 changelog。

### 2026-09-08：首版工程与核心能力

- 从空白目录建立 .NET 8 WPF x64 工程和 xUnit 测试工程。
- 完成托盘生命周期、全局快捷键、首次设置、当前用户启动项和单实例。
- 建立 `ITranslationProvider` 统一接口。
- 接入 OpenAI 兼容 Chat Completions SSE、百度通用文本翻译和阿里云机器翻译。
- 完成中英方向判断、短词模式、2,000 字符限制、默认提示词和会话缓存。
- 完成 UI Automation 取词及模拟复制兜底，加入剪贴板快照恢复。
- 实现无焦点翻译卡片、鼠标附近定位、多显示器工作区约束、原文折叠、复制和解释。
- 加入连接测试、用户可读错误映射以及错误正文脱敏。
- 建立自包含单文件发布和 Inno Setup per-user 安装脚本。

### 2026-09-08：人工反馈后的稳定性与体验迭代

- 修正早期设置页深色背景与系统默认控件颜色不协调的问题，统一输入框、下拉框、按钮、复选框、滚动区域和标题栏视觉。
- 增加跟随系统、浅色、深色和 12 项高自由度自定义配色，支持实时预览与取消回滚。
- 改善取词兜底时机：等待 `Alt+Q` 修饰键释放后再模拟复制，解决部分应用读取不到划词的问题。
- 将翻译卡片改为每次触发都新建窗口、关闭后彻底销毁；重复触发会取消旧请求。
- 增加点击卡片外部立即关闭、失活关闭和 `Esc` 关闭。
- 调整跨 DPI 定位流程：透明显示、目标屏幕预定位、重新测量、最终定位，降低可见平移和抖动。
- 对 AI 流式 token 进行 50 ms 合并刷新，缓解高频 UI 更新造成的鼠标掉帧。
- 为 DeepSeek 端点禁用思考模式，改善连接测试和翻译首包速度。
- 替换应用 Logo；按要求保留白底并使用圆角外形。
- 托盘名称简化为 `QuickTrans`，移除提示文字中的 `Alt+Q`。

### 2026-09-08：0.1.6–0.1.8 构建产物

当前 `outputs` 目录保留：

- `QuickTrans-0.1.6-win-x64-setup.exe`
- `QuickTrans-0.1.7-win-x64-setup.exe`
- `QuickTrans-0.1.7-source.zip`
- `QuickTrans-0.1.8-win-x64-setup.exe`
- `QuickTrans-0.1.8-source.zip`
- `SHA256SUMS.txt`

0.1.7 到 0.1.8 的源码包差异涉及主题模型与服务、设置页、托盘、翻译卡片窗口代码、配置兼容、测试、README、项目版本和安装器版本。0.1.8 是当前源目录对应的最新版本。

### 2026-09-09：项目迁移

- 将项目全部内容从 `C:\Users\Lenovo\Documents\Codex\2026-09-08\new-chat` 迁移到 `E:\Dev\Projects\QuickTrans`。
- 迁移内容包括源码、测试、安装脚本、构建产物、便携 .NET SDK、Inno Setup 和辅助工作目录。
- 检查源码与脚本，未发现旧项目绝对路径引用。
- 使用项目内 .NET 8 SDK执行 Release 测试，22 项全部通过。
- 原位置只留下空目录；当时因其仍是 Codex 会话工作区根目录，未删除。

### 2026-09-15：交接文档

- 审计当前 0.1.8 源码、测试、安装器和保留构建产物。
- 补充本交接文档，明确实际实现、测试空白、风险和接手优先级。

### 2026-09-21：建立 Git 基线

- 扩充 `.gitignore`，排除本机 SDK、Inno 本体、构建产物、配置、凭据和日志。
- 扫描高置信度密钥模式，未发现待提交的真实凭据或私钥。
- 初始化 `main` 分支并以 QuickTrans 0.1.8 建立首个基线提交。
- 创建 GitHub 私有仓库 `lnk137/QuickTrans`，并通过 SSH 推送 `main` 分支。

## 9. 当前产品边界

明确不在当前版本范围内：

- 翻译历史与持久化缓存。
- OCR、截图翻译和文档翻译。
- 朗读、收藏、术语库 UI 和离线模型。
- 账户、同步、计费和商业化系统。
- 自动引擎回退。
- 多套 AI 配置。
- 自动静默更新。
- 中文/英文之外的目标语言选择。
- 周边上下文读取。

“全系统划词”是通用机制下的尽力兼容，不等于所有应用和权限边界下绝对可用。管理员权限应用、受保护页面、自绘控件、远程桌面、游戏和禁用复制的内容可能无法读取。

## 10. 接手时的建议顺序

1. 先运行 22 项自动化测试和 50 次窗口生命周期烟雾测试。
2. 确认 GitHub 远端、分支保护和发布标签策略，持续规范化 `outputs` 与 `work`。
3. 修复缓存键未包含 Base URL/实际提示词的问题。
4. 将 UI Automation 移出 UI 线程并加超时。
5. 建立真实应用取词与多 DPI 的回归矩阵。
6. 评估 .NET 10 LTS 迁移和单文件内部压缩。
7. 脚本化版本提升、发布、Inno 编译、源码归档和 SHA-256 生成。
8. 最后再扩展新语言、新翻译引擎或高级词典能力。
