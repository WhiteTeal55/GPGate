using System.Runtime.InteropServices;

namespace GPGate;

// /s 启动、/k 终止、/r 重启、/g 界面；无参数时自动切换，无父控制台则进界面。
internal static class Program
{
    private const int AttachParentProcess = -1;
    private static readonly string GuiMutexName = $@"Local\{AppInfo.Name}.Gui";

    // 退出码：0 成功、1 执行失败、2 参数错误
    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error($"未处理异常: {e.ExceptionObject}");

        // 启动即同步自启项
        _ = Startup.Sync();

        bool guiArg = args.Length == 1 && args[0].Equals("/g", StringComparison.OrdinalIgnoreCase);

        // GUI 子系统双击不建控制台，从终端调用才附着父控制台
        bool console = !guiArg && AttachConsole(AttachParentProcess);

        if (guiArg || (args.Length == 0 && !console))
            return StartGui();

        // 多参数按错误处理
        string? command = args.Length switch
        {
            0 => "toggle",
            1 => ToCommand(args[0]),
            _ => null,
        };

        if (command is null)
        {
            string shown = string.Join(' ', args);
            if (console)
                Console.Error.WriteLine(Usage(shown));
            else
                Dialog.Show(null, TaskDialogIcon.Warning, $"无法识别的参数：{shown}", "参数错误", Usage(shown));
            return 2;
        }

        // 别的实例正在跑就立刻报错
        var (_, error) = ActionGate.TryRun(command);
        if (error is null)
            return 0;

        if (console)
            Console.Error.WriteLine(error);
        else
            Dialog.Show(null, TaskDialogIcon.Warning, error); // 没有控制台时错误没地方写

        return 1;
    }

    private static string? ToCommand(string arg) => arg.ToLowerInvariant() switch
    {
        "/s" => "start",
        "/k" => "kill",
        "/r" => "restart",
        _ => null,
    };

    private static string Usage(string arg) => $"""
        参数错误: {arg}
        用法: {Environment.ProcessPath} [选项]
          /s  启动 GPG 代理
          /k  终止 GPG 进程
          /r  重启 GPG 代理
          /g  打开图形界面
          (无参数) 自动切换
        """;

    private static int StartGui()
    {
        using var mutex = new Mutex(true, GuiMutexName, out bool createdNew);
        if (!createdNew)
        {
            // 已有实例：等窗口建好再唤醒，3 秒等不到就提示退出
            long deadline = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < deadline)
            {
                if (SingleInstance.NotifyExisting())
                    return 0;

                Thread.Sleep(100);
            }

            _ = Log.Warn("已有实例但未找到主窗口");
            Dialog.Show(null, TaskDialogIcon.Information, "没能唤出已运行实例的窗口，请在任务栏或托盘里找一下，必要时结束进程后重试。", "已有一个 GPGate 在运行");
            return 1;
        }

        // 界面线程异常：记日志并提示
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportGuiError(e.Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.System);
            Application.Run(new MainForm());
            return 0;
        }
        catch (Exception ex)
        {
            // 启动期异常走不到 ThreadException，这里补一次提示
            ReportGuiError(ex);
            return 1;
        }
    }

    private static void ReportGuiError(Exception ex)
    {
        _ = Log.Error($"界面异常: {ex}");
        Dialog.Show(null, TaskDialogIcon.Error, ex.Message, "程序出错", $"日志：{Log.FilePath}");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);
}
