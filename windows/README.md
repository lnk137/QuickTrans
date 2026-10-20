# QuickTrans Windows

原生 C# / WPF 客户端，支持 Windows 10 22H2 / Windows 11 x64。功能概览与隐私说明见[项目主页](../README.md)。

## 构建与测试

在 Windows 上安装 .NET 8 SDK，然后在当前 `windows/` 目录执行：

```powershell
dotnet restore QuickTrans.sln
dotnet build QuickTrans.sln -c Release
dotnet test tests\QuickTrans.Tests\QuickTrans.Tests.csproj -c Release
dotnet publish src\QuickTrans\QuickTrans.csproj -c Release -r win-x64 --self-contained true
```

可执行文件位于 `src/QuickTrans/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/QuickTrans.exe`。

## 安装器

完成 publish 后，使用 Inno Setup 6+ 编译：

```powershell
ISCC.exe installer\QuickTrans.iss
```

安装包输出到当前目录的 `outputs/`，默认按当前用户安装，无需管理员权限。

## 目录说明

- `QuickTrans.sln`：Windows 解决方案，应用和测试的相对引用保持在本目录内。
- `src/QuickTrans/`：客户端源码。
- `tests/QuickTrans.Tests/`：自动化测试。
- `installer/`：Inno Setup 安装脚本。
- `update-manifest.example.json`：可选版本检查清单示例。
- `../assets/`：两端共用图标；WPF 工程通过链接资源保留原有 `Assets/` 资源 URI。

仓库重整前位于根目录的解决方案、`src/`、`tests/` 和 `installer/` 均已迁入本目录。旧的 Windows 交接文档见 [HANDOFF.md](../HANDOFF.md)，其中历史命令需以当前目录执行。
