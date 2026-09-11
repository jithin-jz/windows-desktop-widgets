using System;

namespace KiroWidgets
{
    internal class StatsSnapshot
    {
        internal int CpuPercent = -1;      // -1 means not available yet
        internal int RamPercent = -1;
        internal double DiskFreeGB = -1;
        internal int BatteryPercent = -1;
        internal bool Charging;
    }

    /// <summary>
    /// System stats straight from kernel32. The PowerShell version queried WMI
    /// every three seconds; these calls are a few microseconds each and never
    /// touch the WMI provider host.
    /// </summary>
    internal class Stats
    {
        private long prevIdle;
        private long prevKernel;
        private long prevUser;
        private bool hasPrevious;

        internal StatsSnapshot Read()
        {
            StatsSnapshot s = new StatsSnapshot();

            // ---- CPU: compare the idle share of total kernel+user time between
            // ticks. GetSystemTimes reports kernel time inclusive of idle.
            long idle, kernel, user;
            if (Native.GetSystemTimes(out idle, out kernel, out user))
            {
                if (hasPrevious)
                {
                    long idleDelta = idle - prevIdle;
                    long busyDelta = (kernel - prevKernel) + (user - prevUser);
                    if (busyDelta > 0)
                    {
                        double used = 100.0 * (busyDelta - idleDelta) / busyDelta;
                        s.CpuPercent = (int)Math.Round(Clamp(used, 0, 100));
                    }
                    else
                    {
                        s.CpuPercent = 0;
                    }
                }
                prevIdle = idle; prevKernel = kernel; prevUser = user;
                hasPrevious = true;
            }

            // ---- RAM
            Native.MEMORYSTATUSEX mem = new Native.MEMORYSTATUSEX();
            mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
            if (Native.GlobalMemoryStatusEx(ref mem) && mem.ullTotalPhys > 0)
            {
                double usedFraction = (double)(mem.ullTotalPhys - mem.ullAvailPhys) / mem.ullTotalPhys;
                s.RamPercent = (int)Math.Round(Clamp(usedFraction * 100.0, 0, 100));
            }

            // ---- disk
            ulong freeAvail, total, totalFree;
            if (Native.GetDiskFreeSpaceEx("C:\\", out freeAvail, out total, out totalFree))
                s.DiskFreeGB = totalFree / 1024.0 / 1024.0 / 1024.0;

            // ---- battery
            Native.SYSTEM_POWER_STATUS power;
            if (Native.GetSystemPowerStatus(out power) && (power.BatteryFlag & 128) == 0)
            {
                if (power.BatteryLifePercent <= 100) s.BatteryPercent = power.BatteryLifePercent;
                s.Charging = power.ACLineStatus == 1;
            }

            return s;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
