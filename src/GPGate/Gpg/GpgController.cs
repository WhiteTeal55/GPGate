using System.ComponentModel;
using System.Diagnostics;

namespace GPGate;

// GPG 启停控制，命令行与 GUI 共用。
internal static class GpgController
{
    private static readonly string[] GpgProcessNames = ["gpg-agent", "keyboxd", "scdaemon"];
    private const int ExecTimeoutMs = 10_000;
    private const int OutputTimeoutMs = 3_000;
    private const int MaxDetailChars = 300;

    private static bool _queryFailed;

    // 失败返回 null（只记一次日志），调用方保持原状态
    public static bool? IsRunning()
    {
        try
        {
            bool running = IsAnyProcessRunning(GpgProcessNames);
            _queryFailed = false;
            return running;
        }
        catch (Exception ex)
        {
            if (!_queryFailed)
            {
                _queryFailed = true;
                _ = Log.Warn($"查询 GPG 进程失败: {ex.Message}");
            }

            return null;
        }
    }

    // 只查目标进程，避免枚举全部进程
    private static bool IsAnyProcessRunning(params string[] names)
    {
        foreach (string name in names)
        {
            Process[] processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Length > 0)
                    return true;
            }
            finally
            {
                foreach (Process process in processes)
                    process.Dispose();
            }
        }

        return false;
    }

    public static string? Toggle() => IsRunning() is true ? Kill() : Start();

    public static string? Kill() => Exec("gpgconf", "--kill all", "终止 GPG 进程");

    public static string? Restart() => Kill() ?? Start();

    public static string? Start()
    {
        // keyboxd 与 scdaemon 都依赖 agent，agent 起不来就不必继续
        if (Exec("gpg-connect-agent", "/bye", "启动 gpg-agent") is { } agentFailed)
            return agentFailed;

        // 两者互不影响，都尝试并报第一个错误
        string? keyboxdFailed = Exec("gpg-connect-agent", "--keyboxd /bye", "启动 keyboxd");
        return StartScdaemon() ?? keyboxdFailed;
    }

    // 返回 null 表示成功或用户已禁用
    private static string? StartScdaemon()
    {
        if (IsScdaemonDisabled())
        {
            _ = Log.Info("scdaemon 已禁用，跳过");
            return null;
        }

        // 只能由 agent 拉起，GETINFO 不碰读卡器
        if (Exec("gpg-connect-agent", "\"SCD GETINFO version\" /bye", "启动 scdaemon") is { } scdaemonFailed)
            return scdaemonFailed;

        // 协议层出错时退出码仍是 0，改看进程
        return IsAnyProcessRunning("scdaemon") ? null : Log.Warn("scdaemon 未能启动");
    }

    // 行格式 name:flags:…:argdef:value，disable-scdaemon 的 value 为 1 即禁用
    private static bool IsScdaemonDisabled() =>
        Exec("gpgconf", "--list-options gpg-agent", "读取 gpg-agent 选项", out string options) is null
        && options.Split('\n').Any(line =>
            line.StartsWith("disable-scdaemon:", StringComparison.Ordinal) && line.TrimEnd().EndsWith(":1", StringComparison.Ordinal));

    private static string? Exec(string appName, string arguments, string description) =>
        Exec(appName, arguments, description, out _);

    private static string? Exec(string appName, string arguments, string description, out string output)
    {
        output = string.Empty;

        string? path = FindTool(appName);
        if (path is null)
            return Log.Error($"未找到 {appName}.exe，请检查 GPG 是否已安装并在 PATH 中");

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                // 应答走 stdout，重定向防漏到控制台
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null)
                return Log.Error($"无法启动: {description}");

            // 先异步读，防管道写满
            Task<string> stdout = ReadTextAsync(process.StandardOutput);
            Task<string> stderr = ReadTextAsync(process.StandardError);

            // 卡住时不能一直占着全局锁
            if (!process.WaitForExit(ExecTimeoutMs))
            {
                KillTree(process);
                return Log.Warn($"{description} 超时，已强制结束");
            }

            // 成功不等管道关闭，失败最多等 1 秒
            bool collected = process.ExitCode != 0 ? stderr.Wait(1000) : stderr.IsCompletedSuccessfully;
            string detail = collected ? stderr.Result.Trim() : "";
            if (detail.Length > MaxDetailChars)
                detail = detail[..MaxDetailChars] + "…";

            if (process.ExitCode == 0)
            {
                // GnuPG 的提示也走 stderr
                if (detail.Length > 0)
                    _ = Log.Info($"{description} 输出: {detail}");

                output = stdout.Wait(OutputTimeoutMs) ? stdout.Result : "";
                return null;
            }

            // 带上 stderr，否则只剩一个退出码
            return Log.Warn($"{description} 返回非零退出码: {process.ExitCode}{(detail.Length == 0 ? "" : "，" + detail)}");
        }
        catch (Win32Exception ex)
        {
            return Log.Error($"无法启动: {description} (错误码: {ex.NativeErrorCode})");
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(1000);
        }
        catch (Exception)
        {
            // 已退出或无权结束
        }
    }

    private static async Task<string> ReadTextAsync(StreamReader reader)
    {
        try
        {
            return await reader.ReadToEndAsync();
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
