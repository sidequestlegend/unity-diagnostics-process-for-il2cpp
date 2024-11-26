using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoodAI.Core.Text;

namespace GoodAI.Core.Diagnostics
{
    struct ProcessUtility
    {
        // public methods

        public static void StopTask(ref Task task, ref CancellationTokenSource cancellationTokenSource)
        {
            if (cancellationTokenSource == null)
                return;

            try
            {
                cancellationTokenSource.Cancel();

                if (task != null)
                {   // Give it a short time to complete
                    _ = Task.WhenAny(task, Task.Delay(100))
                            .GetAwaiter()
                            .GetResult();
                }
            }
            catch { }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                task                    = null;
            }
        }

        public static string GetProcessName(IntPtr handle)
        {
            var sb   = StringBuilderPool.Get();
            var size = sb.Capacity;
            var name = Kernel32.QueryFullProcessImageName(handle, 0, sb, ref size) && size > 0
                     ? Path.GetFileNameWithoutExtension(sb.ToString())
                     : "";

            StringBuilderPool.Return(sb);

            return name;
        }

        public static IntPtr FindMainWindow(int processId)
        {
            var result     = IntPtr.Zero;
            var dataHandle = GCHandle.Alloc(new Kernel32.ProcessWindowInfo { ProcessId = (uint)processId });

            try
            {
                var handler = new User32.EnumWindowsProc(HandleEnumWindows);

                _ = User32.EnumWindows(handler, GCHandle.ToIntPtr(dataHandle));
            }
            finally
            {
                dataHandle.Free();
            }

            return result;
        }

        public static Kernel32.SECURITY_ATTRIBUTES GetSecurityAttributesForPipe()
        {
            var securityAttributes                  = new Kernel32.SECURITY_ATTRIBUTES();
            securityAttributes.nLength              = Marshal.SizeOf(securityAttributes);
            securityAttributes.bInheritHandle       = true;
            securityAttributes.lpSecurityDescriptor = IntPtr.Zero;

            return securityAttributes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Encoding GetEncoding(Encoding encoding) => encoding ?? Console.OutputEncoding ?? Encoding.Default;

        // handlers

        [AOT.MonoPInvokeCallback(typeof(User32.EnumWindowsProc))]
        private static bool HandleEnumWindows(IntPtr handle, IntPtr lParam)
        {
            var info = (Kernel32.ProcessWindowInfo)GCHandle.FromIntPtr(lParam).Target;
            _        = User32.GetWindowThreadProcessId(handle, out var processId);

            if (processId == info.ProcessId && User32.IsWindowVisible(handle))
            {
                info.MainWindowHandle = handle;
                return false;
            }

            return true;
        }
    }
}
