using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using FlexAutomator.Blocks.Triggers;

namespace FlexAutomator.Services;

public class GlobalHotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int LLKHF_INJECTED = 0x00000010; 

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookID = IntPtr.Zero;

    private readonly object _lock = new();

    private readonly Dictionary<(Key Key, string Modifiers), List<Guid>> _registry = new();

    public event Action<Guid>? OnScenarioTriggered;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public GlobalHotkeyService()
    {
        _proc = HookCallback;
        _hookID = SetHook(_proc);
    }

    public void Register(Guid scenarioId, Key key, string modifiers)
    {
        var modKey = SortModifiers(modifiers);
        var dictKey = (key, modKey);

        lock (_lock)
        {
            if (!_registry.ContainsKey(dictKey))
            {
                _registry[dictKey] = new List<Guid>();
            }

            if (!_registry[dictKey].Contains(scenarioId))
            {
                _registry[dictKey].Add(scenarioId);
            }
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _registry.Clear();
        }
    }

    private string SortModifiers(string modifiers)
    {
        if (string.IsNullOrWhiteSpace(modifiers)) return string.Empty;
        var parts = modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries)
                             .Select(m => m.Trim().ToLower())
                             .OrderBy(x => x);
        return string.Join("+", parts);
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        if (curModule?.ModuleName == null)
        {
            return IntPtr.Zero;
        }

        return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            GetModuleHandle(curModule.ModuleName), 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if ((kbStruct.flags & LLKHF_INJECTED) != 0)
            {
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }

            Key key = KeyInterop.KeyFromVirtualKey((int)kbStruct.vkCode);

            var currentMods = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) currentMods.Add("ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) currentMods.Add("shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) currentMods.Add("alt");

            var modKey = string.Join("+", currentMods.OrderBy(x => x));

            List<Guid>? scenariosToRun = null;

            lock (_lock)
            {
                if (_registry.TryGetValue((key, modKey), out var scenarioIds) && scenarioIds.Count > 0)
                {
                    scenariosToRun = new List<Guid>(scenarioIds);
                }
            }

            if (scenariosToRun != null)
            {
                foreach (var id in scenariosToRun)
                {
                    OnScenarioTriggered?.Invoke(id);
                }

                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    public void Dispose()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
    }
}