# QuickTrans

QuickTrans 是一个面向 Windows 10 22H2 和 Windows 11 的轻量划词翻译工具。选中文字后按 `Alt+Q`，译文会显示在光标附近。

完整的架构说明、功能细节、构建发布流程、测试现状、已知问题与开发日志见 [HANDOFF.md](HANDOFF.md)。

## 首版能力

- OpenAI 兼容 `/v1/chat/completions` 流式翻译
- 百度翻译与阿里云机器翻译
- 跟随系统、浅色、深色与 12 项可实时预览的自定义配色
- UI Automation 取词，剪贴板复制兜底并恢复原内容
- 中英自动方向、2,000 字符上限、内存会话缓存
- 托盘常驻、可配置快捷键、跟随系统明暗主题
- 不保存翻译历史，不采集周边上下文，不包含遥测

> API 凭据按产品约定以明文写入 `%LocalAppData%\QuickTrans\config.json`。不要分享该文件，也不要在多人共用的 Windows 账户中保存生产密钥。

## 构建

需要 Windows 与 .NET 8 SDK：

```powershell
dotnet restore QuickTrans.sln
dotnet build QuickTrans.sln -c Release
dotnet test tests\QuickTrans.Tests\QuickTrans.Tests.csproj -c Release
dotnet publish src\QuickTrans\QuickTrans.csproj -c Release -r win-x64 --self-contained true
```

若需要启用轻量版本检查，在发布时传入清单地址：

```powershell
dotnet publish src\QuickTrans\QuickTrans.csproj -c Release -r win-x64 --self-contained true -p:QuickTransUpdateManifestUrl=https://example.com/update.json
```

随后使用 Inno Setup 6 编译 `installer\QuickTrans.iss`。安装器采用当前用户安装，不需要管理员权限。

## 使用

首次启动时配置至少一个引擎，测试默认引擎连接并保存。之后在 Edge、Chrome、Word、记事本、Edge PDF 或 Adobe Acrobat Reader 中选中文字并按 `Alt+Q`。

每次触发都会在当前光标附近创建一张全新的翻译卡片；点击卡片外部会关闭并销毁它。卡片默认不抢焦点，点击卡片后可复制结果、切换引擎，AI 句段翻译还可按需请求简短解释。
