using System.ComponentModel;
using System.Diagnostics;

namespace GPGate;

// GPG 启停控制，命令行与 GUI 共用。
internal static class GpgController
{
    private static readonly string[] GpgProcessNames = ["gpg-agent", "keyboxd", "scdaemon"];
    private const int ExecTimeoutMs = 10_000;
    private const int MaxDetailChars = 300;

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

        // gpg-connect-agent 会自己等 agent 起来
        return Exec("gpg-connect-agent", "--keyboxd /bye", "启动 keyboxd");
    }

    public static string? Restart() => Kill() ?? Start();

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

            // 先异步读，防管道写满
            Task<string> stderr = ReadTextAsync(process.StandardError);

            // 卡住时不能一直占着全局锁
            if (!process.WaitForExit(ExecTimeoutMs))
            {
                Kill(process);
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

    // 结束可能已经退出的进程，失败不抛
    private static void Kill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (Exception)
        {
            // 已退出或无权结束
        }
    }

    // 读取异常（进程已释放）按空处理
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
