# GPGate

用一条命令启停 `gpg-agent`、`keyboxd` 与 `scdaemon`，也可常驻托盘管理。

## 用法

```
GPGate.exe /s   启动
GPGate.exe /k   终止
GPGate.exe /r   重启
GPGate.exe /t   启动托盘
GPGate.exe /u   清理开机自启与设置
GPGate.exe      切换（运行中则停止，否则启动）
```

参数不区分大小写；多余参数会报错退出，返回码 `2`。双击运行进入托盘模式，不显示窗口。

终端调用不会阻塞 shell，获取退出码：

```
$p = Start-Process GPGate.exe /s -PassThru; $p.WaitForExit(); $p.ExitCode
```

不要加 `-Wait`，否则会连带等待 `gpg-agent`。

退出码：`0` 成功，`1` 失败，`2` 参数错误。以管理员身份运行会被拒绝并返回 `1`，GPG 进程与设置始终按当前用户处理。仅支持 Windows 11 x64，`gpgconf.exe` 与 `gpg-connect-agent.exe` 需位于 PATH 中。

## 托盘

- 常驻托盘，无主窗口；每 5 秒刷新一次状态，打开菜单前也会查询。
- 有操作执行时，托盘提示和菜单显示“处理中”，完成后自动恢复运行状态。
- 左右键单击托盘图标都会打开菜单：启动/终止/重启、开机自启、自启时启动托盘、退出时二次确认、打开日志、关于、退出。
- 关于页显示版本、版权与图标署名。
- 菜单与提示窗口使用系统原生控件，跟随深浅色；提示窗口出现时会播放系统提示音。
- 退出默认需二次确认，可在确认框勾选「不再提示」，也可在菜单中关闭。有操作执行时会等其结束；操作失败则不退出，改为弹窗提示。
- 已在运行时，再次双击或执行 `/t` 只弹提示，不会创建新实例。

## 构建

需要 .NET 10 SDK。VS Code 任务：`构建：两个版本`（一次构建两个产物），也可单独执行 `构建：框架依赖版本`、`构建：自包含版本`。

框架依赖版本约 549 KB，需安装 .NET 10 桌面运行时：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

自包含版本约 50 MB，无需运行时，首次启动稍慢：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish-standalone
```

## 项目结构

```
├─ src/                 源码
│  ├─ GPGate.sln        解决方案
│  └─ GPGate/
│     ├─ Program.cs     入口、参数与退出码
│     ├─ Core/          应用信息、日志、开机自启
│     ├─ Gpg/           GPG 进程控制与动作锁
│     ├─ Ui/
│     │  ├─ Common/     控件、互操作、主题
│     │  ├─ Dialogs/    对话框、关于、退出确认
│     │  └─ Tray/       托盘宿主与菜单
│     └─ Assets/        图标与改动说明
├─ tools/               图标生成脚本
├─ .github/             CI：手动或打 tag 触发构建，打 tag 时发布 Release
└─ .vscode/             构建任务
```

## 说明

- 启动会拉起三个组件；启用 `disable-scdaemon` 时跳过 `scdaemon`。单个子进程超过 10 秒未退出即判定失败。
- 运行状态只按进程名判断：任一组件在跑即显示运行中，不区分用户与登录会话。
- 同一时刻只执行一个操作；已有操作在跑时，新调用立即失败，不排队。
- 卸载前先退出托盘，执行 `GPGate.exe /u` 可清理开机自启与退出确认设置；日志文件保留。
- 开机自启写入 HKCU Run。未勾选「自启时启动托盘」时，开机只静默启动，不显示托盘图标；写入失败会提示检查注册表权限。
- 错误会连同子进程输出一起显示；无控制台时同样以对话框提示，完整用法位于展开区域。
- 日志位于 `%TEMP%\GPGate\gpgate.log`，按行记录，超过 512 KB 轮转为 `.old`。
- 菜单与弹窗只有深色、浅色两套配色，不跟随系统高对比度主题。

## 图标与许可

图标派生自 GnuPG 官方 artwork，采用 CC BY-SA 4.0；改动说明见 `src/GPGate/Assets/README.md`。

代码采用 MIT 许可，见 `LICENSE`。本项目与 GnuPG 项目无隶属关系。
