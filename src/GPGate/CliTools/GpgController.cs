using System.ComponentModel;
using System.Diagnostics;

namespace GPGate;

// GPG 启停控制，命令行与 GUI 共用。
internal static class GpgController
{
    private static readonly string[] GpgProcessNames = ["gpg-agent", "keyboxd", "scdaemon"];
    private const int ExecTimeoutMs = 10_000;
    private const int AgentReadyTimeoutMs = 5_000;
    private const int AgentReadyPollMs = 100;

    private static bool _queryFailed;

    public static bool IsRunning()
    {
        try
        {
            bool running = GpgProcessNames.Any(IsProcessRunning);
            _queryFailed = false;
            return running;
        }
        catch (Exception ex)
        {
            // 受限环境下查询可能被拒，只记一次
            if (!_queryFailed)
            {
                _queryFailed = true;
                _ = Log.Warn($"查询 GPG 进程失败: {ex.Message}");
            }

            return false;
        }
    }

    private static bool IsProcessRunning(string name)
    {
        Process[] found = Process.GetProcessesByName(name);
        foreach (Process process in found)
            process.Dispose();

        return found.Length > 0;
    }

    public static string? Toggle() => IsRunning() ? Kill() : Start();

    public static string? Kill() => Exec("gpgconf", "--kill all", "终止 GPG 进程");

    public static string? Start()
    {
        if (Exec("gpg-connect-agent", "/bye", "启动 gpg-agent") is { } failed)
            return failed;

        // 等 agent socket 就绪再拉 keyboxd
        if (!WaitForAgentReady())
            _ = Log.Warn("等待 gpg-agent 就绪超时，仍尝试启动 keyboxd");

        return Exec("gpg-connect-agent", "--keyboxd /bye", "启动 keyboxd");
    }

    public static string? Restart() => Kill() ?? Start();

    private static bool WaitForAgentReady()
    {
        // gpgconf 可能输出带引号的路径；命名管道没有可等的文件
        string socket = AgentSocket().Trim().Trim('"');
        if (socket.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
            return true;

        Stopwatch watch = Stopwatch.StartNew();
        while (!File.Exists(socket) && watch.ElapsedMilliseconds < AgentReadyTimeoutMs)
            Thread.Sleep(AgentReadyPollMs);

        return File.Exists(socket);
    }

    // 路径由 gpgconf 给出，自定义 socketdir 也能正确等待
    private static string AgentSocket() =>
        QueryTool("gpgconf", "--list-dirs agent-socket")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gnupg", "S.gpg-agent");

    private static string? QueryTool(string appName, string arguments)
    {
        string? path = FindTool(appName);
        if (path is null)
            return null;

        try
        {
            using Process process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            })!;

            if (!process.WaitForExit(ExecTimeoutMs))
            {
                process.Kill();
                return null;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    // 成功返回 null
    private static string? Exec(string appName, string arguments, string description)
    {
        string? path = FindTool(appName);
        if (path is null)
            return Log.Error($"未找到 {appName}.exe，请检查 GPG 是否已安装并在 PATH 中");

        try
        {
            using Process process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            })!;

            // 提前异步读 stderr，防管道写满；守护进程会继承管道不放，只等 1 秒
            Task<string> stderr = ReadStderrAsync(process);

            // 卡住时不能一直占着全局锁
            if (!process.WaitForExit(ExecTimeoutMs))
            {
                process.Kill();
                return Log.Warn($"{description} 超时，已强制结束");
            }

            if (process.ExitCode == 0)
                return null;

            // 带上 stderr，否则只剩一个退出码
            string detail = stderr.Wait(1000) ? stderr.Result.Trim() : "";
            return Log.Warn($"{description} 返回非零退出码: {process.ExitCode}{(detail.Length == 0 ? "" : "，" + detail)}");
        }
        catch (Win32Exception ex)
        {
            return Log.Error($"无法启动: {description} (错误码: {ex.NativeErrorCode})");
        }
    }

    // 读取异常（进程已释放）按空处理
    private static async Task<string> ReadStderrAsync(Process process)
    {
        try
        {
            return await process.StandardError.ReadToEndAsync();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    // 只认 PATH 里的绝对路径项，防当前目录同名 exe 劫持
    private static string? FindTool(string appName) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(dir => dir.Trim('"'))
            .Where(Path.IsPathFullyQualified)
            .Select(dir => Path.Combine(dir, appName + ".exe"))
            .FirstOrDefault(File.Exists);
}
