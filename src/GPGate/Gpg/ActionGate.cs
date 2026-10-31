namespace GPGate;

// 同一时刻只允许一个进程执行 GPG 操作（命令行与 GUI 共用）。
internal static class ActionGate
{
    private static readonly string LockName = $@"Local\{AppInfo.Name}.Action";

    // 拿不到锁立刻返回，不排队
    public static (bool Ran, string? Error) TryRun(string command)
    {
        Mutex? gate = null;
        bool owned = false;
        try
        {
            gate = new Mutex(false, LockName);
            owned = gate.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            owned = true; // 上一个进程异常退出，锁归我们
        }
        catch (Exception ex)
        {
            // 例如权限受限，按执行失败处理
            gate?.Dispose();
            _ = Log.Error($"获取命令锁失败: {ex.Message}");
            return (true, $"[错误] 获取命令锁失败: {ex.Message}");
        }

        if (!owned)
        {
            gate?.Dispose();
            return (false, $"[错误] 另一个 {AppInfo.Name} 正在执行命令");
        }

        try
        {
            _ = Startup.Sync();
            _ = Log.Info($"执行 {command}");
            return (true, Dispatch(command));
        }
        catch (Exception ex)
        {
            // 别把界面/终端带崩
            _ = Log.Error($"执行 {command} 出错: {ex}");
            return (true, $"[错误] {ex.Message}");
        }
        finally
        {
            gate?.ReleaseMutex();
            gate?.Dispose();
        }
    }

    private static string? Dispatch(string command) => command switch
    {
        "start" => GpgController.Start(),
        "kill" => GpgController.Kill(),
        "restart" => GpgController.Restart(),
        "toggle" => GpgController.Toggle(),
        _ => $"[错误] 未知命令: {command}",
    };
}
