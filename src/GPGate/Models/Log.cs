namespace GPGate;

// %TEMP%\GPGate\gpgate.log，超过 512 KB 轮转为 .old。
internal static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly string MutexName = $@"Local\{AppInfo.Name}.Log";

    internal static string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "GPGate");

    internal static string FilePath { get; } = Path.Combine(DirectoryPath, "gpgate.log");

    internal static string Error(string message) => Write("错误", message);

    internal static string Warn(string message) => Write("警告", message);

    internal static string Info(string message) => Write("信息", message);

    // 写日志并返回可用于终端/弹窗的单行文本；任何异常都不向外抛
    private static string Write(string level, string message)
    {
        string line = $"[{level}] {string.Join(" / ", message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))}";
        bool owned = false;
        Mutex? gate = null;
        try
        {
            // GUI 与 CLI 可能同时写，轮转和追加要串行
            gate = new Mutex(false, MutexName);
            try
            {
                owned = gate.WaitOne(500);
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            if (!owned)
                return line;

            _ = Directory.CreateDirectory(DirectoryPath);

            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                File.Move(FilePath, FilePath + ".old", true);

            File.AppendAllText(FilePath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch
        {
            // 日志不能反过来影响主流程
        }
        finally
        {
            if (owned)
                gate?.ReleaseMutex();
            gate?.Dispose();
        }

        return line;
    }
}
