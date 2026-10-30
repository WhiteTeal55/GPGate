using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GPGate;

// GPG 启停控制，命令行与 GUI 共用。
internal static class GpgController
{
    private static readonly HashSet<string> GpgProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "gpg-agent.exe",
        "keyboxd.exe",
        "scdaemon.exe",
    };

    private const uint SnapshotProcesses = 0x00000002;
    private const int ErrorNoMoreFiles = 18;
    private const int ExecTimeoutMs = 10_000;
    private const int MaxDetailChars = 300;

    private static bool _queryFailed;

    public static bool IsRunning()
    {
        try
        {
            bool running = IsAnyProcessRunning(GpgProcessNames);
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

    public static IDisposable WatchProcessChanges(Action<bool> statusChanged) =>
        new GpgProcessMonitor(statusChanged);

    private sealed class GpgProcessMonitor : IDisposable
    {
        private readonly object _sync = new();
        private readonly Action<bool> _statusChanged;
        private readonly ManagementEventWatcher _started;
        private readonly ManagementEventWatcher _stopped;
        private bool _running;
        private bool _disposed;

        public GpgProcessMonitor(Action<bool> statusChanged)
        {
            ArgumentNullException.ThrowIfNull(statusChanged);
            _statusChanged = statusChanged;
            _running = IsRunning();

            string filter = string.Join(" OR ", GpgProcessNames.Select(name => $"ProcessName = '{name}'"));
            ManagementScope scope = new(@"\\.\root\CIMV2");
            _started = new ManagementEventWatcher(scope,
                new WqlEventQuery($"SELECT * FROM Win32_ProcessStartTrace WHERE {filter}"));
            _stopped = new ManagementEventWatcher(scope,
                new WqlEventQuery($"SELECT * FROM Win32_ProcessStopTrace WHERE {filter}"));
            _started.EventArrived += OnProcessChanged;
            _stopped.EventArrived += OnProcessChanged;

            try
            {
                _started.Start();
                _stopped.Start();
                RefreshStatus(notifyEvenIfUnchanged: true);
            }
            catch
            {
                _started.Dispose();
                _stopped.Dispose();
                throw;
            }
        }

        private void OnProcessChanged(object sender, EventArrivedEventArgs e) =>
            RefreshStatus(notifyEvenIfUnchanged: false);

        private void RefreshStatus(bool notifyEvenIfUnchanged)
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                bool running = IsRunning();
                if (!notifyEvenIfUnchanged && running == _running)
                    return;

                _running = running;
                _statusChanged(running);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            _started.EventArrived -= OnProcessChanged;
            _stopped.EventArrived -= OnProcessChanged;
            _started.Dispose();
            _stopped.Dispose();
        }
    }

    private static bool IsProcessRunning(string name)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase) { name + ".exe" };
        return IsAnyProcessRunning(names);
    }

    private static bool IsAnyProcessRunning(HashSet<string> names)
    {
        using ProcessSnapshotHandle snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        ProcessEntry32 entry = new() { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
        if (!Process32First(snapshot, ref entry))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNoMoreFiles)
                return false;

            throw new Win32Exception(error);
        }

        do
        {
            if (names.Contains(entry.szExeFile))
                return true;
        }
        while (Process32Next(snapshot, ref entry));

        int nextError = Marshal.GetLastWin32Error();
        if (nextError != ErrorNoMoreFiles)
            throw new Win32Exception(nextError);

        return false;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    private sealed class ProcessSnapshotHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
    {
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ProcessSnapshotHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(ProcessSnapshotHandle snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(ProcessSnapshotHandle snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    public static string? Toggle() => IsRunning() ? Kill() : Start();

    public static string? Kill() => Exec("gpgconf", "--kill all", "终止 GPG 进程");

    public static string? Start()
    {
        // keyboxd 与 scdaemon 都依赖 agent，agent 起不来就不必继续
        if (Exec("gpg-connect-agent", "/bye", "启动 gpg-agent") is { } agentFailed)
            return agentFailed;

        // 两者互不影响：都尝试，报第一个错误
        // gpg-connect-agent 会自己等 agent 起来
        string? keyboxdFailed = Exec("gpg-connect-agent", "--keyboxd /bye", "启动 keyboxd");
        return StartScdaemon() ?? keyboxdFailed;
    }

    // 返回 null 表示成功或用户已禁用
    private static string? StartScdaemon()
    {
        // 用户禁用时跳过
        if (IsScdaemonDisabled())
        {
            _ = Log.Info("scdaemon 已禁用，跳过");
            return null;
        }

        // 只能由 agent 拉起，GETINFO 不碰读卡器
        if (Exec("gpg-connect-agent", "\"SCD GETINFO version\" /bye", "启动 scdaemon") is { } scdaemonFailed)
            return scdaemonFailed;

        // 协议层出错时退出码仍是 0，改看进程
        return IsProcessRunning("scdaemon") ? null : Log.Warn("scdaemon 未能启动");
    }

    // 行格式 name:flags:…:argdef:value，disable-scdaemon 的 value 为 1 即禁用
    private static bool IsScdaemonDisabled() =>
        Exec("gpgconf", "--list-options gpg-agent", "读取 gpg-agent 选项", out string options) is null
        && options.Split('\n').Any(line =>
            line.StartsWith("disable-scdaemon:", StringComparison.Ordinal) && line.TrimEnd().EndsWith(":1", StringComparison.Ordinal));

    public static string? Restart() => Kill() ?? Start();

    // 成功返回 null
    private static string? Exec(string appName, string arguments, string description) =>
        Exec(appName, arguments, description, out _);

    // 成功返回 null，output 带出 stdout
    private static string? Exec(string appName, string arguments, string description, out string output)
    {
        output = string.Empty;

        string? path = FindTool(appName);
        if (path is null)
            return Log.Error($"未找到 {appName}.exe，请检查 GPG 是否已安装并在 PATH 中");

        try
        {
            using Process process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                // 应答走 stdout，重定向防漏到控制台
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;

            // 先异步读，防管道写满
            Task<string> stdout = ReadTextAsync(process.StandardOutput);
            Task<string> stderr = ReadTextAsync(process.StandardError);

            // 卡住时不能一直占着全局锁
            if (!process.WaitForExit(ExecTimeoutMs))
            {
                Kill(process);
                return Log.Warn($"{description} 超时，已强制结束");
            }

            // stderr：成功不等管道关闭，失败最多等 1 秒
            bool collected = process.ExitCode != 0 ? stderr.Wait(1000) : stderr.IsCompletedSuccessfully;
            string detail = collected ? stderr.Result.Trim() : "";
            if (detail.Length > MaxDetailChars)
                detail = detail[..MaxDetailChars] + "…";

            if (process.ExitCode == 0)
            {
                // GnuPG 的提示也走 stderr
                if (detail.Length > 0)
                    _ = Log.Info($"{description} 输出: {detail}");

                output = stdout.Wait(1000) ? stdout.Result : "";
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
