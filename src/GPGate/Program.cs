using System.Runtime.InteropServices;
using System.Security.Principal;

namespace GPGate;

// /s 启动、/k 终止、/r 重启、/t 托盘、/u 清理；无参数时自动切换，无父控制台则进托盘。
internal static class Program
{
    private const int AttachParentProcess = -1;
    private static readonly string GuiMutexName = $@"Local\{AppInfo.Name}.Gui";

    // 退出码：0 成功、1 执行失败、2 参数错误
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error($"未处理异常: {e.ExceptionObject}");

        string argText = string.Join(' ', args);
        _ = Log.Info($"启动 {AppInfo.Name} {AppInfo.Version}，参数: {(argText.Length == 0 ? "无" : argText)}，pid: {Environment.ProcessId}");

        bool trayArg = args.Length == 1 && args[0].Equals("/t", StringComparison.OrdinalIgnoreCase);
        bool uninstallArg = args.Length == 1 && args[0].Equals("/u", StringComparison.OrdinalIgnoreCase);

        // 从终端调用才附着父控制台
        bool console = !trayArg && AttachConsole(AttachParentProcess);

        // 提权会拉起同权限的 gpg 进程，并可能读写其他账户的设置
        if (IsElevated())
        {
            const string message = "GPGate 不需要管理员权限，请以当前用户身份运行。";
            _ = Log.Warn(message);
            if (console)
                Console.Error.WriteLine(message);
            else
                Dialog.Show(null, TaskDialogIcon.Warning, message);
            return 1;
        }

        if (uninstallArg)
            return Uninstall(console);

        if (trayArg || (args.Length == 0 && !console))
            return StartTray();

        string? command = args.Length switch
        {
            0 => "toggle",
            1 => ToCommand(args[0]),
            _ => null,
        };

        if (command is null)
        {
            if (console)
                Console.Error.WriteLine(Usage(argText));
            else
                Dialog.Show(null, TaskDialogIcon.Warning, $"无法识别的参数：{argText}", "参数错误", Usage(argText));
            return 2;
        }

        var (_, error) = ActionGate.TryRun(command);
        if (error is null)
            return 0;

        if (console)
            Console.Error.WriteLine(error);
        else
            Dialog.ShowResult(error);

        return 1;
    }

    private static string? ToCommand(string arg) => arg.ToLowerInvariant() switch
    {
        "/s" => "start",
        "/k" => "kill",
        "/r" => "restart",
        _ => null,
    };

    private static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string Usage(string arg) => $"""
        参数错误: {arg}
        用法: {Environment.ProcessPath} [选项]
          /s  启动 GPG 代理
          /k  终止 GPG 进程
          /r  重启 GPG 代理
          /t  启动托盘程序
          /u  清理开机自启与设置
          (无参数) 自动切换
        """;

    private static int Uninstall(bool console)
    {
        if (IsTrayRunning())
        {
            const string message = "请先退出 GPGate 托盘，再执行清理。";
            if (console)
                Console.Error.WriteLine(message);
            else
                Dialog.Show(null, TaskDialogIcon.Warning, message);
            return 1;
        }

        bool cleaned = Startup.Disable() & CloseConfirmation.DeleteSettings();
        string result = cleaned ? "已清理开机自启与退出确认设置。" : "清理失败，请检查注册表权限。";

        if (console)
            Console.WriteLine(result);
        else
            Dialog.Show(null, cleaned ? TaskDialogIcon.Information : TaskDialogIcon.Warning, result);

        return cleaned ? 0 : 1;
    }

    private static bool IsTrayRunning()
    {
        if (!Mutex.TryOpenExisting(GuiMutexName, out Mutex? mutex))
            return false;

        mutex?.Dispose();
        return true;
    }

    private static int StartTray()
    {
        using var mutex = new Mutex(true, GuiMutexName, out bool createdNew);
        if (!createdNew)
        {
            Dialog.Show(null, TaskDialogIcon.Information, "GPGate 已在系统托盘运行，请使用托盘菜单。", "程序已在运行");
            return 0;
        }

        _ = Startup.Sync();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportGuiError(e.Exception);

        try
        {
            Application.SetColorMode(SystemColorMode.System);
            using TrayContext context = new();
            Application.Run(context);
            return 0;
        }
        catch (Exception ex)
        {
            // 启动期异常走不到 ThreadException
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
