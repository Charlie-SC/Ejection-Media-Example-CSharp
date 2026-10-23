/*
 * HardwareEject
 * Copyright (C) 2026 Charlie-SC
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public class HardwareEjectorDiag {

    // ------------------------------------------------------------------
    // Native imports
    // ------------------------------------------------------------------

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Auto,
        SetLastError = true)]

    public static extern SafeFileHandle CreateFile(
        string fileName,
        uint   desiredAccess,
        uint   shareMode,
        IntPtr securityAttributes,
        uint   creationDisposition,
        uint   flagsAndAttributes,
        IntPtr templateFile);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]

    [return: MarshalAs(UnmanagedType.Bool)]

    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint           dwIoControlCode,
        IntPtr         lpInBuffer,
        uint           nInBufferSize,
        IntPtr         lpOutBuffer,
        uint           nOutBufferSize,
        out uint       lpBytesReturned,
        IntPtr         lpOverlapped);

    // ------------------------------------------------------------------
    // Eject logic
    // ------------------------------------------------------------------

    public static void Eject(string driveLetter) {

        // Validate the drive (accepted forms: D, D: or D:\)
        if (string.IsNullOrEmpty(driveLetter)) {
            throw new Exception("No drive was specified.");
        }

        char letter = char.ToUpperInvariant(driveLetter[0]);

        if (letter < 'A' || letter > 'Z' || (driveLetter.Length > 1 && driveLetter[1] != ':')) {
            throw new Exception("Invalid drive: " + driveLetter);
        }

        // Ensure proper format of the drive letter path (e.g., \\.\D:)
        string formattedPath = @"\\.\" + letter + ":";

        // Open a handle to the volume
        SafeFileHandle handle = CreateFile(
            formattedPath,
            0xC0000000, // GENERIC_READ | GENERIC_WRITE
            3,          // FILE_SHARE_READ | FILE_SHARE_WRITE
            IntPtr.Zero,
            3,          // OPEN_EXISTING
            0,          // No additional attributes
            IntPtr.Zero
        );

        int error;

        if (handle.IsInvalid) {
            error = Marshal.GetLastWin32Error();
            throw new Exception("Could not open volume " + letter + ": " + new Win32Exception(error).Message + " (error " + error + ")");
        }

        uint bytesReturned;
        bool success;

        // Lock volume (FSCTL_LOCK_VOLUME)
        // Fails if any file is open on the volume. Nothing else is attempted in that case.
        success = DeviceIoControl(
            handle,
            0x90018,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            out bytesReturned,
            IntPtr.Zero);

        if (!success) {
            error = Marshal.GetLastWin32Error();
            handle.Close();
            throw new Exception("Lock volume failed: " + new Win32Exception(error).Message + " (error " + error + ")");
        }

        // Dismount volume (FSCTL_DISMOUNT_VOLUME)
        success = DeviceIoControl(
            handle,
            0x90020,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            out bytesReturned,
            IntPtr.Zero);

        if (!success) {
            error = Marshal.GetLastWin32Error();
            handle.Close();
            throw new Exception("Dismount volume failed: " + new Win32Exception(error).Message + " (error " + error + ")");
        }

        // Eject media (IOCTL_STORAGE_EJECT_MEDIA)
        success = DeviceIoControl(
            handle,
            0x2D4808,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            out bytesReturned,
            IntPtr.Zero);

        if (!success) {
            error = Marshal.GetLastWin32Error();
            handle.Close();
            throw new Exception("Eject media failed: " + new Win32Exception(error).Message + " (error " + error + ")");
        }

        handle.Close();
        Console.WriteLine(letter + ": successfully ejected. Safe to remove.");
    }
}

public static class Program {

    public static void Main() {

        // ========== VALUES: change the values and execute ===============================================
        string Drive = @"D:\";     // Target Drive Letter
        // ================================================================================================

        try {
            HardwareEjectorDiag.Eject(Drive);
        } catch (Exception ex) {
            Console.WriteLine("Ejection failed: " + ex.Message);
        }
    }
}