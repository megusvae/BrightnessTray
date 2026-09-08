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

namespace BrightnessTray
{
    /// <summary>
    /// Unified brightness control abstraction that handles both WMI (laptop) and DDC-CI (external) monitors.
    /// </summary>
    internal static class BrightnessController
    {
        private static List<MonitorInfo> monitors = new List<MonitorInfo>();

        /// <summary>
        /// Initializes the brightness controller by detecting all connected monitors.
        /// </summary>
        internal static void Initialize()
        {
            RefreshMonitors();
        }

        /// <summary>
        /// Refreshes the list of connected monitors.
        /// </summary>
        internal static void RefreshMonitors()
        {
            monitors = MonitorManager.GetConnectedMonitors();
        }

        /// <summary>
        /// Gets all detected monitors.
        /// </summary>
        /// <returns>List of MonitorInfo objects.</returns>
        internal static List<MonitorInfo> GetMonitors()
        {
            return new List<MonitorInfo>(monitors);
        }

        /// <summary>
        /// Gets the brightness of a specific monitor.
        /// </summary>
        /// <param name="monitor">The monitor to query.</param>
        /// <returns>Brightness value (0-100) or -1 if unavailable.</returns>
        internal static int GetBrightness(MonitorInfo monitor)
        {
            if (monitor == null)
                return -1;

            try
            {
                if (monitor.IsLaptopDisplay)
                {
                    return WmiFunctions.GetBrightnessLevel();
                }
                else if (monitor.SupportsDdcCi)
                {
                    return DdcCiFunctions.GetBrightnessViaDdcCi(monitor.Handle);
                }
            }
            catch { }

            return -1;
        }

        /// <summary>
        /// Sets the brightness of a specific monitor.
        /// </summary>
        /// <param name="monitor">The monitor to control.</param>
        /// <param name="brightness">Brightness value (0-100).</param>
        /// <returns>True if successful, false otherwise.</returns>
        internal static bool SetBrightness(MonitorInfo monitor, int brightness)
        {
            if (monitor == null || brightness < 0 || brightness > 100)
                return false;

            try
            {
                if (monitor.IsLaptopDisplay)
                {
                    WmiFunctions.SetBrightnessLevel(brightness);
                    monitor.CurrentBrightness = brightness;
                    return true;
                }
                else if (monitor.SupportsDdcCi)
                {
                    if (DdcCiFunctions.SetBrightnessViaDdcCi(monitor.Handle, brightness))
                    {
                        monitor.CurrentBrightness = brightness;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Gets the count of controllable monitors.
        /// </summary>
        /// <returns>Number of monitors with brightness control support.</returns>
        internal static int GetControllableMonitorCount()
        {
            return monitors.Count;
        }

        /// <summary>
        /// Checks if any monitors support DDC-CI.
        /// </summary>
        /// <returns>True if at least one monitor supports DDC-CI.</returns>
        internal static bool HasDdcCiSupport()
        {
            foreach (var monitor in monitors)
            {
                if (monitor.SupportsDdcCi)
                    return true;
            }
            return false;
        }
    }
}
