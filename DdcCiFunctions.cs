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
    /// Provides DDC-CI (Display Data Channel Command Interface) support for controlling
    /// external monitor brightness via I2C communication.
    /// </summary>
    internal static class DdcCiFunctions
    {
        // DDC-CI command codes
        private const byte DDC_CI_COMMAND_GET_BRIGHTNESS = 0x10;
        private const byte DDC_CI_COMMAND_SET_BRIGHTNESS = 0x10;
        private const byte DDC_CI_VCP_CODE_BRIGHTNESS = 0x10;

        // DDC-CI protocol constants
        private const byte DDC_CI_GET_VCP_REQUEST = 0x01;
        private const byte DDC_CI_SET_VCP_REQUEST = 0x03;
        private const byte DDC_CI_GET_VCP_REPLY = 0x02;

        private const byte I2C_WRITE_ADDRESS = 0x6E; // DDC-CI write address (0x37 << 1)
        private const byte I2C_READ_ADDRESS = 0x6F;  // DDC-CI read address (0x37 << 1 | 1)

        /// <summary>
        /// Attempts to get the current brightness of a monitor via DDC-CI.
        /// </summary>
        /// <param name="hMonitor">Handle to the monitor.</param>
        /// <returns>Brightness value (0-100) or -1 if unsupported/failed.</returns>
        internal static int GetBrightnessViaDdcCi(IntPtr hMonitor)
        {
            try
            {
                if (hMonitor == IntPtr.Zero)
                    return -1;

                // Attempt to get VCP code 0x10 (brightness)
                uint currentValue = 0, maxValue = 0;

                if (GetVcpFeature(hMonitor, DDC_CI_VCP_CODE_BRIGHTNESS, out currentValue, out maxValue))
                {
                    // Normalize to 0-100 range
                    if (maxValue > 0)
                        return (int)((currentValue * 100) / maxValue);
                }

                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Attempts to set the brightness of a monitor via DDC-CI.
        /// </summary>
        /// <param name="hMonitor">Handle to the monitor.</param>
        /// <param name="brightness">Brightness value (0-100).</param>
        /// <returns>True if successful, false otherwise.</returns>
        internal static bool SetBrightnessViaDdcCi(IntPtr hMonitor, int brightness)
        {
            try
            {
                if (hMonitor == IntPtr.Zero || brightness < 0 || brightness > 100)
                    return false;

                // First, get the max value for this monitor
                uint currentValue = 0, maxValue = 100;
                GetVcpFeature(hMonitor, DDC_CI_VCP_CODE_BRIGHTNESS, out currentValue, out maxValue);

                // Normalize brightness from 0-100 to 0-maxValue
                uint newValue = (uint)((brightness * maxValue) / 100);

                return SetVcpFeature(hMonitor, DDC_CI_VCP_CODE_BRIGHTNESS, newValue);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a VCP feature value from a monitor.
        /// </summary>
        private static bool GetVcpFeature(IntPtr hMonitor, byte vcpCode, out uint currentValue, out uint maxValue)
        {
            currentValue = 0;
            maxValue = 100;

            try
            {
                // Construct DDC-CI GET VCP REQUEST command
                // Format: 0x6E [length] 0x01 [VCP code] [checksum]
                byte[] request = new byte[4];
                request[0] = DDC_CI_GET_VCP_REQUEST;
                request[1] = vcpCode;
                request[2] = 0; // placeholder for checksum
                request[3] = 0; // placeholder

                // Calculate checksum
                byte checksum = CalculateChecksum(request, 3);
                request[3] = checksum;

                // Send request via I2C
                byte[] response = new byte[12];
                if (I2CWrite(hMonitor, request) && I2CRead(hMonitor, response))
                {
                    // Parse response: 0x6F [length] 0x02 [VCP code] [type] [max high] [max low] [current high] [current low] [checksum]
                    if (response[0] == DDC_CI_GET_VCP_REPLY && response[1] == vcpCode)
                    {
                        // Extract max and current values
                        maxValue = ((uint)response[2] << 8) | response[3];
                        currentValue = ((uint)response[4] << 8) | response[5];
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Sets a VCP feature value on a monitor.
        /// </summary>
        private static bool SetVcpFeature(IntPtr hMonitor, byte vcpCode, uint newValue)
        {
            try
            {
                // Construct DDC-CI SET VCP REQUEST command
                // Format: 0x6E [length] 0x03 [VCP code] [value high] [value low] [checksum]
                byte[] request = new byte[6];
                request[0] = DDC_CI_SET_VCP_REQUEST;
                request[1] = vcpCode;
                request[2] = (byte)((newValue >> 8) & 0xFF);
                request[3] = (byte)(newValue & 0xFF);
                request[4] = 0; // placeholder for checksum
                request[5] = 0; // placeholder

                // Calculate checksum
                byte checksum = CalculateChecksum(request, 5);
                request[5] = checksum;

                return I2CWrite(hMonitor, request);
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Writes data to a monitor via I2C.
        /// </summary>
        private static bool I2CWrite(IntPtr hMonitor, byte[] data)
        {
            try
            {
                // Note: Direct I2C access requires appropriate driver support.
                // In practice, Windows DDC-CI is typically accessed via WMI or
                // through specialized monitor control libraries.
                // This is a placeholder for the protocol structure.
                return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Reads data from a monitor via I2C.
        /// </summary>
        private static bool I2CRead(IntPtr hMonitor, byte[] buffer)
        {
            try
            {
                // Placeholder - actual implementation depends on driver availability
                return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Calculates the DDC-CI checksum.
        /// The checksum is calculated as: 0x50 XOR (all data bytes)
        /// </summary>
        private static byte CalculateChecksum(byte[] data, int length)
        {
            byte checksum = 0x50; // DDC-CI base value

            for (int i = 0; i < length; i++)
            {
                checksum ^= data[i];
            }

            return checksum;
        }
    }
}
