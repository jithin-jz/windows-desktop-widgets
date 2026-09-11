using System;
using System.Runtime.InteropServices;

namespace KiroWidgets
{
    /// <summary>
    /// Win32 entry points. Everything the widgets need from the OS is read
    /// through P/Invoke rather than WMI, which keeps the process small and
    /// avoids waking the WMI provider host every few seconds.
    /// </summary>
    internal static class Native
    {
        // ---- window styles and z-order -----------------------------------
        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                                int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        internal const byte VK_MEDIA_NEXT = 0xB0;
        internal const byte VK_MEDIA_PREV = 0xB1;
        internal const byte VK_MEDIA_PLAY = 0xB3;

        /// <summary>Non-interactive widget: never takes focus, never in Alt+Tab.</summary>
        internal static void MakeDesktopWidget(IntPtr hwnd)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        /// <summary>Interactive widget (Notes): can take focus, still hidden from Alt+Tab.</summary>
        internal static void MakeToolWindowOnly(IntPtr hwnd)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// Park the widget at the bottom of the z-order so every app window draws
        /// over it. The widget then lives on the desktop the way macOS widgets do
        /// instead of floating above everything like the taskbar.
        /// </summary>
        internal static void PinToBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        internal static void SendMediaKey(byte vk)
        {
            keybd_event(vk, 0, 0x0001, UIntPtr.Zero);
            keybd_event(vk, 0, 0x0001 | 0x0002, UIntPtr.Zero);
        }

        // ---- system stats -------------------------------------------------
        [DllImport("kernel32.dll")]
        internal static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORYSTATUSEX
        {
            internal uint dwLength;
            internal uint dwMemoryLoad;
            internal ulong ullTotalPhys;
            internal ulong ullAvailPhys;
            internal ulong ullTotalPageFile;
            internal ulong ullAvailPageFile;
            internal ulong ullTotalVirtual;
            internal ulong ullAvailVirtual;
            internal ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GetDiskFreeSpaceEx(string directoryName,
                                                       out ulong freeBytesAvailable,
                                                       out ulong totalNumberOfBytes,
                                                       out ulong totalNumberOfFreeBytes);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_POWER_STATUS
        {
            internal byte ACLineStatus;
            internal byte BatteryFlag;
            internal byte BatteryLifePercent;
            internal byte SystemStatusFlag;
            internal uint BatteryLifeTime;
            internal uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        /// <summary>BatteryFlag bit 7 means "no system battery".</summary>
        internal static bool HasBattery()
        {
            SYSTEM_POWER_STATUS s;
            if (!GetSystemPowerStatus(out s)) return false;
            return (s.BatteryFlag & 128) == 0;
        }
    }
}
