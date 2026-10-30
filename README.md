# GPGate

用于 `gpg-agent`、`keyboxd` 与 `scdaemon` 的启停管理，封装为单条命令。

## 用法

```
GPGate.exe /s   启动
GPGate.exe /k   终止
GPGate.exe /r   重启
GPGate.exe /g   打开图形界面
GPGate.exe      自动切换（正在运行就停止，未在运行就启动）
```

参数不区分大小写。参数多余时程序报错退出，返回码 `2`。

双击运行时进入图形界面。

在终端中调用不会阻塞 shell，获取退出码请使用：

```
$p = Start-Process GPGate.exe /s -PassThru; $p.WaitForExit(); $p.ExitCode
```

不要加 `-Wait`：它会连带等待 `gpg-agent`，看起来像卡死。

退出码含义：`0` 成功，`1` 失败，`2` 参数错误。

仅支持 Windows 11 x64。`gpgconf.exe` 与 `gpg-connect-agent.exe` 必须位于 PATH 中。

## 图形界面

界面为无边框圆角卡片，跟随系统主题配色，按 DPI 缩放。

顶部区域可拖动。Esc 关闭窗口，F1 打开关于。

`–` 最小化至托盘，单击托盘图标即可恢复。`×` 为退出，默认需二次确认（可勾选「不再提示」）。若当前有操作正在执行，程序会等待该操作完成后再退出。等待期间界面锁定，不响应点击。操作失败时程序不退出，在界面上提示错误。

界面只呈现当前可用的操作。程序启动时读取一次 GPG 状态，之后通过 Windows 进程启停事件更新界面，不进行定时轮询。

窗口图标与任务栏图标右上角显示状态圆点。蓝色为运行中，灰色为未运行。

<img src="assets/shot-dark-off.png" width="260" alt="深色 · 未运行"> <img src="assets/shot-light-on.png" width="260" alt="浅色 · 运行中">

托盘右键菜单为系统原生菜单，跟随主题配色。窗口收起后进程启停事件仍会更新托盘图标，打开菜单时也会主动刷新一次状态。

菜单包含显示窗口、启动/终止/重启、开机自启、退出时二次确认、打开日志与关于。关于页显示版本、版权与图标署名。

程序已在运行时，再次启动或执行 `/g` 均不会创建新实例。已有实例将被唤至前台。3 秒内未能唤出时会弹出提示。

## 构建

需要 .NET 10 SDK，两种发布方式可一次生成。

框架依赖版本约 680 KB，运行时需安装 .NET 10 桌面运行时：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

自包含版本约 50 MB，无需安装运行时。代价是首次启动延迟约半秒：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish-standalone
```

## 图标与许可

图标基于 GnuPG 官方 artwork 派生，采用 CC BY-SA 4.0。改动说明见 `src/GPGate/Assets/README.md`。

代码采用 MIT 许可，见 `LICENSE`；图标为 CC BY-SA 4.0。本项目与 GnuPG 项目无隶属关系。

## 说明

启动会拉起 `gpg-agent`、`keyboxd` 与 `scdaemon`，终止则一并结束。启用了 `disable-scdaemon` 时跳过 `scdaemon`。单个子进程超过 10 秒未退出即判定为失败。

同时只允许一个操作，已有操作在跑时新的调用立即返回失败，不排队。

任一环节失败时，对话框会一并给出子进程的错误输出。日志位于 `%TEMP%\GPGate\gpgate.log`，按行记录，超过 512 KB 轮转为 `.old`。

在无控制台环境下，参数错误和执行失败同样以对话框提示。完整用法置于对话框的展开区域中。

开机自启由托盘菜单控制。未勾选「自启时启动托盘」时，开机只静默启动，不显示托盘图标。设置写入失败时会提示需要管理员权限。

退出确认默认开启。可在确认框中勾选「不再提示」，也可在托盘菜单的「退出时二次确认」里开关。
