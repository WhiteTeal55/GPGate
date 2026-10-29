# GPGate

把 `gpg-agent` / `keyboxd` 的启停压成一条命令。

## 用法

```
GPGate.exe /s   启动
GPGate.exe /k   终止
GPGate.exe /r   重启
GPGate.exe /g   打开图形界面
GPGate.exe      自动切换（在跑就停，没跑就启动）
```

大小写不敏感；多余参数报错并返回 `2`。双击进界面，从终端调用才附着控制台，所以 shell 不等它结束——取退出码用 `$p = Start-Process GPGate.exe /s -PassThru; $p.WaitForExit(); $p.ExitCode`（别用 `-Wait`，它会连 `gpg-agent` 一起等，看着像卡死）。退出码 `0` 成功、`1` 失败、`2` 参数错误。

只支持 Windows 11 x64；`gpgconf.exe`、`gpg-connect-agent.exe` 需在 PATH 里。

## 图形界面

无边框圆角卡片，跟随应用深浅色、按 DPI 缩放。顶部可拖动；Esc 关闭，F1 打开关于。– 收进托盘（单击重开），× 退出（执行中会等动作完成，期间锁定界面；失败则留在界面提示）。只显示可用操作，每 2 秒刷新；状态行右侧是「日志」和版本号（Tab 聚焦，空格打开关于）。窗口与任务栏图标右上角带状态圆点（蓝=运行中、灰=未运行）。

<img src="assets/shot-dark-off.png" width="260" alt="深色 · 未运行"> <img src="assets/shot-light-on.png" width="260" alt="浅色 · 运行中">

托盘右键是系统原生菜单（跟随深浅色），含「打开日志」「关于」等项；「关于」显示版本、版权与图标署名。重复启动或 `/g` 只唤醒已有实例，3 秒内唤不出会弹提示。

## 构建

需要 .NET 10 SDK。两个版本可以一次出齐。

框架依赖（约 680 KB，需 .NET 10 桌面运行时）：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

自包含（约 50 MB，免运行时；压缩只对它有效，首启慢半秒）：

```
dotnet publish src/GPGate/GPGate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish-standalone
```

## 图标与许可

GnuPG 官方 artwork 衍生（CC BY-SA 4.0，见 `src/GPGate/Assets/README.md`）：运行中原色，未运行随主题取白/黑；48px 以上带外环 logo，16–32px 只用挂锁。状态圆点运行时画到窗口图标上，不写进 ico；改设计后跑 `python tools/make-icon.py`。

代码 MIT（`LICENSE`）；图标 CC BY-SA 4.0。与 GnuPG 项目无隶属关系。

## 说明

- 启动：`gpg-connect-agent /bye` → 等 agent socket（最多 5 秒，路径取自 `gpgconf --list-dirs agent-socket`）→ `--keyboxd /bye`；终止：`gpgconf --kill all`；单条命令 10 秒超时。
- 命令行与 GUI 共用 `Local\GPGate.Action` 锁，抢不到立即返回；只在 PATH 的绝对路径项里找 exe，避免当前目录同名文件劫持。
- 失败附子进程 stderr；日志 `%TEMP%\GPGate\gpgate.log`（512 KB 轮转）。
- 错误提示统一用 TaskDialog；窗口隐藏时先唤出主窗口；无控制台时参数错误也弹窗，完整用法收在展开器里。
- 开机自启：托盘切换；不勾「自启时启动托盘」则只以 `/s` 后台启动。启动、操作、开菜单时同步，exe 移动或改名后重写路径；写入失败时菜单提示需要管理员权限。
