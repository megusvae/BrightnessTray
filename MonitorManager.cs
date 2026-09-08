/*
	This file is part of BrightnessTray.

    BrightnessTray is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    BrightnessTray is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with BrightnessTray.  If not, see <http://www.gnu.org/licenses/>.

*/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BrightnessTray
{
    /// <summary>
    /// Represents a single monitor with its properties and brightness control capabilities.
    /// </summary>
    public class MonitorInfo
    {
        public IntPtr Handle { get; set; }
        public string Name { get; set; }
        public bool IsLaptopDisplay { get; set; }
        public bool SupportsDdcCi { get; set; }
        public int CurrentBrightness { get; set; }
        public int MaxBrightness { get; set; }

        public override string ToString()
        {
            return $"{Name} ({(IsLaptopDisplay ? "Laptop" : "External")})";
        }
    }

    /// <summary>
    /// Manages detection and enumeration of connected monitors.
    /// </summary>
    internal static class MonitorManager
    {
        private static List<MonitorInfo> cachedMonitors = new List<MonitorInfo>();

        /// <summary>
        /// Callback for EnumDisplayMonitors.
        /// </summary>
        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData);

        /// <summary>
        /// Enumerates all connected monitors and returns their information.
        /// </summary>
        /// <returns>List of detected monitors.</returns>
        internal static List<MonitorInfo> GetConnectedMonitors()
        {
            cachedMonitors.Clear();
            EnumDisplayMonitorsWrapper();
            return new List<MonitorInfo>(cachedMonitors);
        }

        /// <summary>
        /// Wrapper for EnumDisplayMonitors that collects monitor information.
        /// </summary>
        private static void EnumDisplayMonitorsWrapper()
        {
            try
            {
                MonitorEnumDelegate del = MonitorEnumProc;
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, del, IntPtr.Zero);
            }
            catch { }
        }

        /// <summary>
        /// Callback function for monitor enumeration.
        /// </summary>
        private static bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
        {
            try
            {
                var monitorInfo = new MonitorInfo
                {
                    Handle = hMonitor,
                    Name = GetMonitorName(hMonitor),
                    IsLaptopDisplay = IsLaptopDisplay(hMonitor),
                    SupportsDdcCi = false, // Will be determined when attempting to use DDC-CI
                    MaxBrightness = 100
                };

                // Try to get initial brightness
                if (monitorInfo.IsLaptopDisplay)
                {
                    monitorInfo.CurrentBrightness = WmiFunctions.GetBrightnessLevel();
                }
                else
                {
                    // Attempt DDC-CI for external monitors
                    int ddcBrightness = DdcCiFunctions.GetBrightnessViaDdcCi(hMonitor);
                    if (ddcBrightness >= 0)
                    {
                        monitorInfo.CurrentBrightness = ddcBrightness;
                        monitorInfo.SupportsDdcCi = true;
                    }
                    else
                    {
                        monitorInfo.CurrentBrightness = 50; // Default fallback
                    }
                }

                cachedMonitors.Add(monitorInfo);
            }
            catch { }

            return true; // Continue enumeration
        }

        /// <summary>
        /// Gets the user-friendly name of a monitor.
        /// </summary>
        private static string GetMonitorName(IntPtr hMonitor)
        {
            try
            {
                NativeMethods.MONITORINFO mi = new NativeMethods.MONITORINFO();
                mi.cbSize = (uint)Marshal.SizeOf(mi);

                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    // Check if primary monitor
                    if ((mi.dwFlags & 1) != 0) // MONITORINFOF_PRIMARY
                    {
                        return "Primary Monitor";
                    }
                    else
                    {
                        return $"Monitor {cachedMonitors.Count + 1}";
                    }
                }
            }
            catch { }

            return $"Unknown Monitor";
        }

        /// <summary>
        /// Determines if a monitor is a laptop internal display.
        /// This is a heuristic check - laptop displays are typically the primary monitor
        /// and may have specific properties.
        /// </summary>
        private static bool IsLaptopDisplay(IntPtr hMonitor)
        {
            try
            {
                NativeMethods.MONITORINFO mi = new NativeMethods.MONITORINFO();
                mi.cbSize = (uint)Marshal.SizeOf(mi);

                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    // Primary monitor is likely the laptop display
                    // (This is a heuristic; true detection would require checking EDID or WMI)
                    return (mi.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// P/Invoke for EnumDisplayMonitors.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    }
}
