using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassIsland;

public class RegistryNotifier : IDisposable
{
    [DllImport("advapi32.dll", EntryPoint = "RegNotifyChangeKeyValue")]
    private static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool bWatchSubtree, int dwNotifyFilter, int hEvent, bool fAsynchronous);

    [DllImport("advapi32.dll", EntryPoint = "RegOpenKey")]
    private static extern int RegOpenKey(uint hKey, string lpSubKey, ref IntPtr phkResult);

    [DllImport("advapi32.dll", EntryPoint = "RegCloseKey")]
    private static extern int RegCloseKey(IntPtr hKey);

    public static uint HKEY_CLASSES_ROOT = 0x80000000;
    public static uint HKEY_CURRENT_USER = 0x80000001;
    public static uint HKEY_LOCAL_MACHINE = 0x80000002;
    public static uint HKEY_USERS = 0x80000003;
    public static uint HKEY_PERFORMANCE_DATA = 0x80000004;
    public static uint HKEY_CURRENT_CONFIG = 0x80000005;
   // private static uint HKEY_DYN_DATA = 0x80000006;

    private const int REG_NOTIFY_CHANGE_NAME = 0x1;
    private const int REG_NOTIFY_CHANGE_ATTRIBUTES = 0x2;
    private const int REG_NOTIFY_CHANGE_LAST_SET = 0x4;
    private const int REG_NOTIFY_CHANGE_SECURITY = 0x8;

    private IntPtr _openIntPtr = IntPtr.Zero;

    public delegate void RegistryKeyUpdatedHandler();
    public event RegistryKeyUpdatedHandler? RegistryKeyUpdated;

    private bool _isWorking;
    private Task? _updatingTask;

    public RegistryNotifier(uint root, string path)
    {
        int openResult = RegOpenKey(root, path, ref _openIntPtr);
        if (openResult != 0)
        {
            throw new InvalidOperationException("Failed to open registry key.");
        }
    }

    public void Start()
    {
        _isWorking = true;
        _updatingTask = Task.Run(async () => await UpdateMain());
    }

    public void Stop()
    {
        _isWorking = false;
        _updatingTask?.Wait();
        RegCloseKey(_openIntPtr);
        _openIntPtr = IntPtr.Zero;
    }

    private async Task UpdateMain()
    {
        while (_isWorking)
        {
            int notifyResult = RegNotifyChangeKeyValue(
                _openIntPtr, true,
                REG_NOTIFY_CHANGE_ATTRIBUTES | REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_SECURITY,
                0, false);

            if (notifyResult == 0)
            {
                RegistryKeyUpdated?.Invoke();
                Debug.WriteLine("Registry key updated.");
            }

            await Task.Yield(); // Ensure the loop doesn't block the thread
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
