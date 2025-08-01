using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GoodAI.Core.Diagnostics
{
    public struct Kernel32
    {
        public const uint STARTF_USESTDHANDLES  = 0x00000100;
        public const uint CREATE_NO_WINDOW      = 0x08000000;
        public const uint DUPLICATE_SAME_ACCESS = 0x00000002;
        public const uint WM_CLOSE              = 0x0010;
        public const int  ERROR_HANDLE_EOF      = 38;
        public const int  ERROR_BROKEN_PIPE     = 109;
        public const int  ERROR_NO_DATA         = 232;

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int    cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint   dwX;
            public uint   dwY;
            public uint   dwXSize;
            public uint   dwYSize;
            public uint   dwXCountChars;
            public uint   dwYCountChars;
            public uint   dwFillAttribute;
            public uint   dwFlags;
            public short  wShowWindow;
            public short  cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint   dwProcessId;
            public uint   dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int    nLength;
            public IntPtr lpSecurityDescriptor;
            public bool   bInheritHandle;
        }

        public struct ProcessWindowInfo
        {
            public uint   ProcessId;
            public IntPtr MainWindowHandle;
        }

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool CreateProcess(
            string                  lpApplicationName,
            string                  lpCommandLine,
            IntPtr                  lpProcessAttributes,
            IntPtr                  lpThreadAttributes,
            bool                    bInheritHandles,
            uint                    dwCreationFlags,
            IntPtr                  lpEnvironment,
            string                  lpCurrentDirectory,
            ref STARTUPINFO         lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern int GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool QueryFullProcessImageName(
            IntPtr        hProcess,
            uint          dwFlags,
            StringBuilder lpExeName,
            ref int       lpdwSize);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool CreatePipe(
            out SafeFileHandle hReadPipe,
            out SafeFileHandle hWritePipe,
            IntPtr             lpPipeAttributes,
            uint               nSize);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool ReadFile(
            IntPtr   hFile,
            byte[]   lpBuffer,
            uint     nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr   lpOverlapped);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool DuplicateHandle(
            IntPtr     hSourceProcessHandle,
            IntPtr     hSourceHandle,
            IntPtr     hTargetProcessHandle,
            out IntPtr lpTargetHandle,
            uint       dwDesiredAccess,
            bool       bInheritHandle,
            uint       dwOptions);


        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
