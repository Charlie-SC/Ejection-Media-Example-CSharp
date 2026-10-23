# HardwareEject

## Overview

HardwareEject is a diagnostic utility written in C# that ejects a Windows drive by opening a handle to the volume through `CreateFile` and issuing a sequence of `DeviceIoControl` calls, both exported by `kernel32.dll`. The utility locks the volume, dismounts it and then sends the eject command to the media, following the sequence of operations described in the Microsoft documentation for these control codes. The result of every step is checked, and the utility stops at the first step that fails, reporting the error returned by Windows.

## Warning

**Dismounting a volume that is in use can cause data loss.**

The program does not request confirmation and does not verify that the selected drive is the intended one. The lock step fails when files are open on the volume, and in that case the utility stops before dismounting, but this protection must not be treated as a substitute for closing every program that uses the drive beforehand. The value of `Drive` must be verified before every execution. The authors assume no responsibility for data loss resulting from the use of this software.

## Requirements

| Requirement      | Detail                                                  |
|------------------|---------------------------------------------------------|
| Operating system | Windows                                                 |
| Shell            | Windows PowerShell 5.1                                  |
| Privileges       | PowerShell must be started with administrator rights    |

No compiler or SDK installation is required. PowerShell compiles the source file in memory at execution time.

## Usage

### 1. Configure the operation

Open `HardwareEject.cs` and edit the value at the beginning of the `Main` method:

```csharp
string Drive = @"D:\";     // Target Drive Letter
```

### 2. Execute

Start PowerShell as administrator and run the following commands:

```powershell
cd C:\path\to\HardwareEject
Add-Type -Path .\HardwareEject.cs
[Program]::Main()
```

Each PowerShell session can load the class only once. To run the utility again after modifying the configuration, close the current window and open a new one.

## Configuration Reference

| Parameter | Type     | Description                                                                                                                              |
|-----------|----------|------------------------------------------------------------------------------------------------------------------------------------------|
| `Drive`   | `string` | Drive to be ejected. Accepted forms are a drive letter (`D`), a drive letter with colon (`D:`) or a root path (`D:\`). The letter is case-insensitive. |

## Output Reference

Messages are written to the console. The utility does not print event codes. Errors returned by Windows include the system message and the numeric error code.

| Situation                          | Output                                                                              |
|------------------------------------|-------------------------------------------------------------------------------------|
| `Drive` is empty                   | `Ejection failed: No drive was specified.`                                          |
| `Drive` has an invalid format      | `Ejection failed: Invalid drive: <Drive>`                                           |
| Volume handle could not be opened  | `Ejection failed: Could not open volume <Letter>: <message> (error <code>)`         |
| A control operation failed         | `Ejection failed: <Operation> failed: <message> (error <code>)`                     |
| Sequence completed                 | `<Letter>: successfully ejected. Safe to remove.`                                   |

`<Operation>` is `Lock volume`, `Dismount volume` or `Eject media`. The success message is printed only after the three operations have completed without error.

## Implementation

| Component              | Purpose                                                                                                                                  |
|------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| `HardwareEjectorDiag`  | Class containing the native imports and the eject logic.                                                                                 |
| `CreateFile`           | Native function imported from `kernel32.dll` through P/Invoke. Opens the volume and returns a `SafeFileHandle`, which releases the handle deterministically. |
| `DeviceIoControl`      | Native function imported from `kernel32.dll`. Sends a control code directly to the volume driver. The `BOOL` return value is marshalled as `UnmanagedType.Bool`. |
| `Eject`                | Validates the drive, builds the volume path, opens the handle, and runs the lock, dismount and eject operations in order. After each operation it checks the result and, on failure, closes the handle and reports the Windows error message and code. |
| `Program.Main`         | Contains the configuration value, calls `Eject` and reports any exception.                                                               |

### Volume path

The drive letter is converted to upper case and used to build a path in the form `\\.\D:`, which addresses the volume itself instead of the files stored on it.

### Operation sequence

| Step | Operation                   | Control code | Purpose                                                                                      |
|------|-----------------------------|--------------|----------------------------------------------------------------------------------------------|
| 1    | `FSCTL_LOCK_VOLUME`         | `0x90018`    | Requests exclusive access to the volume. Fails if files are open on the volume.              |
| 2    | `FSCTL_DISMOUNT_VOLUME`     | `0x90020`    | Dismounts the file system from the volume.                                                   |
| 3    | `IOCTL_STORAGE_EJECT_MEDIA` | `0x2D4808`   | Instructs the device to eject its media.                                                     |

The lock is held by the handle and is released when the handle is closed. The handle is closed after the last step, or immediately after any step that fails. If the lock fails, the dismount and eject steps are not attempted.

### Constants passed to `CreateFile`

| Argument              | Windows constant                          | Value        | Meaning                                  |
|-----------------------|-------------------------------------------|--------------|------------------------------------------|
| `desiredAccess`       | `GENERIC_READ \| GENERIC_WRITE`           | `0xC0000000` | Read and write access to the volume      |
| `shareMode`           | `FILE_SHARE_READ \| FILE_SHARE_WRITE`     | `3`          | Required by the lock and dismount codes  |
| `creationDisposition` | `OPEN_EXISTING`                           | `3`          | Opens the volume only if it exists       |
| `flagsAndAttributes`  | None                                      | `0`          | No additional attributes                 |

## Troubleshooting

| Symptom                                                  | Probable cause and resolution                                                                                           |
|----------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------|
| `Could not open volume ...`                              | PowerShell is not elevated or the drive letter does not exist. Run as administrator and verify `Drive`.                 |
| `Lock volume failed: ...`                                | Files are open on the volume, or the volume is a system volume or contains a page file. Close every program using the drive and run the utility again. |
| `Dismount volume failed: ...`                            | Windows refused to dismount the volume. Read the error message and code, close any program using the drive and try again. |
| `Eject media failed: ...`                                | The device may not support the eject command. The documentation states that `IOCTL_STORAGE_EJECT_MEDIA` may or may not be supported on devices with removable media. |
| `Invalid drive: ...` or `No drive was specified.`        | `Drive` is empty or does not start with a letter followed by an optional colon. Set a value such as `D:\`.              |
| Type already exists when running again                   | The class is already loaded in the current session. Open a new PowerShell window.                                       |

## Limitations

- The utility does not ask for confirmation and does not verify that the selected drive is the intended one.
- If the dismount succeeds but the eject step fails, the volume remains dismounted. Windows may mount it again the next time a process accesses it.
- The eject step depends on device support, so it may fail on some devices even when the lock and dismount steps succeed.
- The utility ejects one drive per execution and does not accept command-line arguments; the value is edited in the source file.
- The utility is intended for diagnostic and educational purposes.

## Sources

- [Microsoft Platform Invoke (P/Invoke) Documentation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
- [Microsoft Native Type Marshalling](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/type-marshalling)
- [Microsoft FSCTL_LOCK_VOLUME](https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_lock_volume)
- [Microsoft FSCTL_DISMOUNT_VOLUME](https://learn.microsoft.com/en-us/windows-hardware/drivers/ifs/fsctl-dismount-volume)
- [Microsoft IOCTL_STORAGE_EJECT_MEDIA](https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-ioctl_storage_eject_media)
- [Microsoft SafeFileHandle Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.safehandles.safefilehandle?view=net-8.0)
- [Microsoft Win32 New Data Types](https://learn.microsoft.com/en-us/windows/win32/winprog64/the-new-data-types)
