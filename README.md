# GPGate

用于 `gpg-agent` 与 `keyboxd` 的启停管理，封装为单条命令。

## 用法

```
GPGate.exe /s   启动
GPGate.exe /k   终止
GPGate.exe /r   重启
GPGate.exe /g   打开图形界面
GPGate.exe      自动切换（正在运行就停止，未在运行就启动）
```

参数不区分大小写。参数多余时程序报错退出，返回码 `2`。

双击运行时进入图形界面。在终端中调用会附着控制台，shell 不等待其结束。因此获取退出码需自行等待：

```
$p = Start-Process GPGate.exe /s -PassThru; $p.WaitForExit(); $p.ExitCode
```

不建议使用 `-Wait`。它会连带等待 `gpg-agent`，而后者由 GPGate 派生。父进程等待子进程时界面无响应，容易被误判为卡死。改用 `-PassThru` 取得进程对象后再调用 `WaitForExit()`，等待范围限于 GPGate 自身。

退出码含义：`0` 成功，`1` 失败，`2` 参数错误。

仅支持 Windows 11 x64。`gpgconf.exe` 与 `gpg-connect-agent.exe` 必须位于 PATH 中。

## 图形界面

界面为无边框圆角卡片，跟随系统主题配色，按 DPI 缩放。

顶部区域可拖动。Esc 关闭窗口，F1 打开关于。

`–` 最小化至托盘，单击托盘图标即可恢复。`×` 为退出。若当前有操作正在执行，程序会等待该操作完成后再退出。等待期间界面锁定，不响应点击。操作失败时程序不退出，在界面上提示错误。

界面只呈现当前可用的操作，状态每 2 秒刷新。状态行右侧为「日志」与版本号。可用 Tab 聚焦，空格打开关于。

窗口图标与任务栏图标右上角显示状态圆点。蓝色为运行中，灰色为未运行。

<img src="assets/shot-dark-off.png" width="260" alt="深色 · 未运行"> <img src="assets/shot-light-on.png" width="260" alt="浅色 · 运行中">

托盘右键菜单为系统原生菜单，跟随主题配色。菜单项包含「打开日志」与「关于」。关于页显示版本、版权与图标署名。

程序已在运行时，再次启动或执行 `/g` 均不会创建新实例。已有实例将被唤至前台。3 秒内未能唤出时会弹出提示。

## 构建

需要 .NET 10 SDK，两种发布方式可一次生成。

框架依赖版本约 680 KB，运行时需安装 .NET 10 桌面运行时：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

自包含版本约 50 MB，无需安装运行时。压缩仅对自包含版本有效，代价是首次启动延迟约半秒：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish-standalone
```

## 图标与许可

图标基于 GnuPG 官方 artwork 派生，采用 CC BY-SA 4.0。改动说明见 `src/GPGate/Assets/README.md`。

运行中使用原色，未运行时按主题取白或黑。48px 以上尺寸带外环 logo，16–32px 仅保留挂锁。

状态圆点在运行时绘制到窗口图标上，不写入 ico 文件。修改图标设计后需重新执行 `python tools/make-icon.py`。

代码采用 MIT 许可，见 `LICENSE`；图标为 CC BY-SA 4.0。本项目与 GnuPG 项目无隶属关系。

## 说明

启动流程分为三步：

1. 执行 `gpg-connect-agent /bye`，启动 agent。
2. 等待 agent socket 就绪。等待上限为 5 秒。socket 路径取自 `gpgconf --list-dirs agent-socket`，未采用硬编码。
3. 执行 `--keyboxd /bye`。

终止流程仅执行单条 `gpgconf --kill all`。每条命令均设有 10 秒超时。超时即判定为失败。

命令行与图形界面共用互斥锁 `Local\GPGate.Action`。未能获取该锁时立即返回，且不进入等待队列。

可执行文件的查找范围限定于 PATH 中的绝对路径项。当前目录不参与检索。此举用于避免同名文件劫持。

任一环节失败时，一并输出子进程的 stderr。日志文件位于 `%TEMP%\GPGate\gpgate.log`。达到 512 KB 后轮转。

错误信息统一通过 TaskDialog 呈现。主窗口处于隐藏状态时，先将其恢复显示。在无控制台环境下，参数错误同样以对话框提示。完整用法置于对话框的展开区域中。

开机自启由托盘菜单控制。未勾选「自启时启动托盘」时，开机仅以 `/s` 启动。启动过程在后台进行。

该配置在程序启动、执行操作及打开菜单时进行同步。可执行文件可能被移动或改名。此时其路径将被重写。写入失败时，托盘菜单提示需要管理员权限。
