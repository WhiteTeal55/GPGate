# GPGate

GPGate 是一个 Windows 托盘工具，用于启动、终止和重启 GnuPG 后台进程。

- 面向 Windows 11 x64
- 需要将 `gpgconf.exe` 和 `gpg-connect-agent.exe` 加入 `PATH`
- 使用 .NET 10 构建

## 托盘使用

启动程序后，GPGate 常驻系统托盘，不显示主窗口。默认会显示 Windows 通知提示程序已启动；在托盘菜单中取消勾选「启动时提示」即可关闭后续通知。单击或右键托盘图标可打开菜单。

托盘菜单提供：

- **启动 / 终止 / 重启** GnuPG 后台进程
- **开机自启**：将当前程序路径写入当前用户的 Windows 启动项
- **启动时提示**：控制程序启动通知，默认开启
- **打开日志**
- **关于**
- **退出**

托盘图标每 5 秒刷新一次运行状态，打开菜单时也会立即刷新。菜单和提示窗口跟随 Windows 应用配色。

如果程序已在运行，再次启动 GPGate 或执行 `/g` 会立即提示程序已在运行，然后退出新进程。请使用已有的托盘图标打开菜单。正在执行 GPG 操作时，新的操作不会排队；此时请求退出会等操作结束，操作失败时保留托盘实例并显示错误。

## 命令行

```text
GPGate.exe /s   启动 GnuPG 后台进程
GPGate.exe /k   终止 GnuPG 后台进程
GPGate.exe /r   重启 GnuPG 后台进程
GPGate.exe /g   启动托盘程序
GPGate.exe      自动切换：运行中则终止，否则启动
```

参数不区分大小写。命令行调用会等待 GPG 操作完成，并返回退出码：

| 退出码 | 含义 |
| --- | --- |
| `0` | 成功 |
| `1` | 操作失败 |
| `2` | 参数错误 |

参数错误时会输出用法。无控制台时，错误通过提示窗口显示。

在 PowerShell 中获取退出码：

```powershell
$p = Start-Process .\GPGate.exe -ArgumentList '/s' -PassThru
$p.WaitForExit()
$p.ExitCode
```

不要给 `Start-Process` 加 `-Wait`：GPG 子进程可能继续运行，导致 PowerShell 一直等待。

## GnuPG 操作说明

启动会依次启动 `gpg-agent`、`keyboxd` 和 `scdaemon`。若 GnuPG 配置中禁用了 `scdaemon`，则跳过它。终止操作使用 `gpgconf --kill all`。

运行状态根据 `gpg-agent`、`keyboxd` 和 `scdaemon` 进程判断；任一进程运行即视为 GnuPG 正在运行。该判断不区分 Windows 用户或登录会话。

单个 GnuPG 命令最多等待 10 秒。操作失败时会显示错误提示；无控制台的命令行调用也会显示提示窗口。

## 日志与设置

- 日志文件：`%TEMP%\GPGate\gpgate.log`
- 日志超过 512 KB 后轮转为 `gpgate.log.old`
- 开机自启与启动通知偏好保存在当前用户注册表中
- 注册表设置写入失败时会显示提示

## 构建与发布

需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。仓库的 VS Code 任务也提供框架依赖和自包含两种发布方式。

### 框架依赖

需要安装 .NET 10 Desktop Runtime：

```powershell
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

### 自包含

无需另外安装 .NET 运行时：

```powershell
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish-standalone
```

## 项目结构

```text
GPGate/
├─ src/
│  └─ GPGate/
│     ├─ Program.cs                      程序入口、参数解析、托盘进程单实例管理
│     ├─ GPGate.csproj                   .NET 10 Windows Forms 项目与嵌入资源配置
│     ├─ Core/
│     │  ├─ AppInfo.cs                   程序名称、版本、元数据与应用图标加载
│     │  ├─ Log.cs                       日志记录、并发保护与日志轮转
│     │  └─ Startup.cs                   开机自启注册表项与启动通知偏好管理
│     ├─ Gpg/
│     │  ├─ ActionGate.cs                跨进程 GPG 操作互斥与命令分发
│     │  └─ GpgController.cs             GnuPG 进程状态查询及启停控制
│     ├─ Ui/
│     │  ├─ MainForm.cs                  隐藏的托盘宿主、状态刷新与菜单命令处理
│     │  ├─ TrayMenu.cs                  原生托盘菜单及主题适配
│     │  ├─ AboutForm.cs                 关于窗口
│     │  ├─ Dialog.cs                    提示窗口显示入口与窗口实现
│     │  ├─ Controls.cs                  详细信息文本框与链接按钮
│     │  ├─ Interop.cs                   图标、声音/任务栏提示与窗口焦点互操作
│     │  └─ Theme.cs                     深浅色调色板与 Windows 配色刷新
```

## 许可与图标署名

程序代码采用 MIT 许可，详见 [LICENSE](LICENSE)。

图标基于 GnuPG 官方 artwork 派生，按 CC BY-SA 4.0 使用。署名 GnuPG Project / g10 Code GmbH。来源、改动说明和许可信息见 [图标素材说明](src/GPGate/Assets/README.md)。GPGate 与 GnuPG 项目无隶属关系。
