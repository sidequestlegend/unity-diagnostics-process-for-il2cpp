// https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs
// https://github.com/mono/mono/blob/main/mcs/class/referencesource/System/services/monitoring/system/diagnosticts/Process.cs

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace GoodAI.Core.Diagnostics
{
    public class DataReceivedEventArgs : EventArgs
    {
        public readonly string Data;

        public DataReceivedEventArgs(string data) => Data = data;
    }

    public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);

    public sealed class Process : IDisposable
    {
        const int BUFFER_SIZE = 4096;

        // public members

        public string                         ProcessName => _processName;
        public int                            Id          => _processID;
        public IntPtr                         Handle      => _processHandle;
        public bool                           HasExited   => _hasExited;

        public ProcessStartInfo               StartInfo {
            get => _startInfo;
            set => _startInfo = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool                           EnableRaisingEvents {
            get => _enableRaisingEvents;
            set {
                if (_enableRaisingEvents == value)
                    return;
                _enableRaisingEvents = value;

                if (value && _hasExited == false && _disposed == false)
                {
                    StartExitMonitor();
                }
                else
                if (value == false)
                {
                    StopExitMonitor();
                }
            }
        }

        public StreamReader                   StandardOutput {
            get {
                if (_startInfo.RedirectStandardOutput == false)
                    throw new InvalidOperationException("StandardOutput has not been redirected");
            
                if (_outputStream == null && _outputPipeHandle != null)
                {
                    var encoding = ProcessUtility.GetEncoding(_startInfo.StandardOutputEncoding);
                    _outputStream = new StreamReader(new FileStream(_outputPipeHandle, FileAccess.Read, BUFFER_SIZE, false), encoding, true, BUFFER_SIZE);
                }
                
                return _outputStream;
            }
        }

        public StreamReader                   StandardError {
            get {
                if (_startInfo.RedirectStandardError == false)
                    throw new InvalidOperationException("StandardError has not been redirected");
            
                if (_errorStream == null && _errorPipeHandle != null)
                {
                    var encoding = ProcessUtility.GetEncoding(_startInfo.StandardErrorEncoding);
                    _errorStream = new StreamReader(new FileStream(_errorPipeHandle, FileAccess.Read, BUFFER_SIZE, false), encoding, true, BUFFER_SIZE);
                }
                
                return _errorStream;
            }
        }

        // events

        public event EventHandler             Disposed;
        public event EventHandler             Exited;
        public event DataReceivedEventHandler OutputDataReceived;
        public event DataReceivedEventHandler ErrorDataReceived;

        // private members

        private IntPtr                        _processHandle;
        private int                           _processID;
        private string                        _processName;
        private ProcessStartInfo              _startInfo;
        private volatile bool                 _disposed;
        private volatile bool                 _hasExited;
        private bool                          _enableRaisingEvents;
        private CancellationTokenSource       _exitMonitorCancellationTokenSource;
        private CancellationTokenSource       _outputCancellationTokenSource;
        private CancellationTokenSource       _errorCancellationTokenSource;
        private Task                          _exitMonitorTask;
        private Task                          _outputTask;
        private Task                          _errorTask;
        private SafeFileHandle                _outputPipeHandle;
        private SafeFileHandle                _errorPipeHandle;
        private StreamReader                  _outputStream;
        private StreamReader                  _errorStream;

        // c-tor / d-tor

        public Process()
        {
            _startInfo = new ProcessStartInfo();
        }

        ~Process()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // public methods

        public static Process GetCurrentProcess()
        {
            var handle = Kernel32.GetCurrentProcess();

            return new Process {
                _processHandle = handle,
                _processID     = Kernel32.GetCurrentProcessId(),
                _processName   = ProcessUtility.GetProcessName(handle)
            };
        }

        public static Process Start(string filename)
        {
            var process = new Process {
                StartInfo = new() {
                    FileName         = filename,
                    WorkingDirectory = Path.GetDirectoryName(filename),
                },
            };

            try
            {
                _ = process.Start();
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        public bool Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Process));

            var startupInfo           = new Kernel32.STARTUPINFO();
            startupInfo.cb            = Marshal.SizeOf(startupInfo);
            var outputWritePipeHandle = default(SafeFileHandle);
            var errorWritePipeHandle  = default(SafeFileHandle);

            CreatePipes(ref startupInfo, ref outputWritePipeHandle, ref errorWritePipeHandle);

            var filenameIsQuoted = _startInfo.FileName.StartsWith('"') && _startInfo.FileName.EndsWith('"');
            var commandLine      = filenameIsQuoted ? _startInfo.FileName : $"\"{_startInfo.FileName}\"";

            if (string.IsNullOrWhiteSpace(_startInfo.Arguments) == false)
            {
                commandLine = $"{commandLine} {_startInfo.Arguments}";
            }

            try
            {
                var success = Kernel32.CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    _startInfo.CreateNoWindow ? Kernel32.CREATE_NO_WINDOW : 0,
                    IntPtr.Zero,
                    _startInfo.WorkingDirectory,
                    ref startupInfo,
                    out var pi
                );

                if (success == false)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                _processHandle = pi.hProcess;
                _              = Kernel32.CloseHandle(pi.hThread); // Close thread handle immediately
                _processID     = (int)pi.dwProcessId;
                _processName   = ProcessUtility.GetProcessName(_processHandle);

                if (EnableRaisingEvents)
                {
                    StartExitMonitor();
                }
            }
            catch
            {
                ObjectUtility.Dispose(ref _outputPipeHandle);
                ObjectUtility.Dispose(ref _errorPipeHandle);
                throw;
            }
            finally
            {
                ObjectUtility.Dispose(ref outputWritePipeHandle);
                ObjectUtility.Dispose(ref errorWritePipeHandle);
            }

            return true;
        }
        public void BeginOutputReadLine()
        {
            if (_startInfo.RedirectStandardOutput == false)
                throw new InvalidOperationException("StandardOutput has not been redirected");

            BeginReadFile(
                _outputPipeHandle,
                ProcessUtility.GetEncoding(_startInfo.StandardOutputEncoding),
                OutputDataReceived,
                out _outputTask,
                ref _outputCancellationTokenSource
            );
        }

        public void CancelOutputRead() => ProcessUtility.StopTask(ref _outputTask, ref _outputCancellationTokenSource);

        public void BeginErrorReadLine()
        {
            if (_startInfo.RedirectStandardError == false)
                throw new InvalidOperationException("StandardError has not been redirected");

            BeginReadFile(
                _errorPipeHandle,
                ProcessUtility.GetEncoding(_startInfo.StandardErrorEncoding),
                ErrorDataReceived,
                out _errorTask,
                ref _errorCancellationTokenSource
            );
        }

        public void CancelErrorRead() => ProcessUtility.StopTask(ref _errorTask, ref _errorCancellationTokenSource);

        public async Task<bool> WaitForExitAsync(int milliseconds = -1)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Process));
            if (milliseconds < -1)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));

            try
            {
                if (milliseconds == -1)
                {   // Wait indefinitely
                    while (_hasExited == false && _disposed == false)
                    {
                        var result = await Task.Run(() => Kernel32.WaitForSingleObject(_processHandle, 100));
                        if (result == 0)
                        {
                            ProcessExited();
                            break;
                        }

                        await Task.Delay(100);
                    }
                }
                else
                {   // Wait with timeout
                    var timeoutTask = Task.Delay(milliseconds);
                    var waitTask    = Task.Run(async () => {
                        while (_hasExited == false && _disposed == false)
                        {
                            var result = Kernel32.WaitForSingleObject(_processHandle, 100);
                            if (result == 0)
                            {
                                ProcessExited();
                                break;
                            }

                            await Task.Delay(100);
                        }

                        return _hasExited;
                    });

                    var completedTask = await Task.WhenAny(waitTask, timeoutTask);
                    if (completedTask == timeoutTask)
                        return false; // Timeout

                    return await waitTask;
                }
            }
            catch (Exception) when (_disposed)
            {
                throw new ObjectDisposedException(nameof(Process));
            }

            return _hasExited;
        }

        public bool WaitForExit(int milliseconds)
        {
            return Task.Run(() => WaitForExitAsync(milliseconds)).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public bool CloseMainWindow()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Process));

            return User32.PostMessage(ProcessUtility.FindMainWindow(_processID), Kernel32.WM_CLOSE, 0, 0);
        }

        public void Close()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Process));

            try
            {
                // First try to close gracefully
                if (_hasExited == false)
                {
                    if (CloseMainWindow())
                    {
                        if (WaitForExit(5000) == false)
                        {
                            Kill();                // Force terminate if didn't exit gracefully
                            _ = WaitForExit(1000); // Give it a second to terminate
                        }
                    }
                    else
                    {
                        Kill();
                        _ = WaitForExit(1000);
                    }
                }
            }
            finally
            {
                Dispose();
            }
        }
        
        public void Kill()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Process));

            if (Kernel32.TerminateProcess(_processHandle, 0) == false)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
        }

        // private methods

        private void StartExitMonitor()
        {
            if (_hasExited)
                return;
            if (_disposed)
                return;

            _exitMonitorCancellationTokenSource = new();
            _exitMonitorTask                    = MonitorProcessExitAsync(_exitMonitorCancellationTokenSource.Token);
        }

        private void StopExitMonitor() => ProcessUtility.StopTask(ref _exitMonitorTask, ref _exitMonitorCancellationTokenSource);

        private async Task MonitorProcessExitAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (_hasExited == false && _disposed == false && cancellationToken.IsCancellationRequested == false)
                {
                    // Check process status every 100ms
                    var result = await Task.Run(() => Kernel32.WaitForSingleObject(_processHandle, 100), cancellationToken);

                    if (result == 0) // Process has exited
                    {
                        ProcessExited();
                        break;
                    }

                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) when (_disposed) { }
        }

        private void ProcessExited()
        {
            if (_hasExited)
                return;

            _hasExited  = true;
            var handler = Exited;

            if (_disposed == false && handler != null)
            {
                try { handler(this, EventArgs.Empty); } catch { }
            }

            Dispose();
        }

        private void BeginReadFile(SafeFileHandle pipeHandle, Encoding encoding, DataReceivedEventHandler handler,
                                   out Task task, ref CancellationTokenSource cancellationTokenSource)
        {
            if (cancellationTokenSource != null)
                throw new InvalidOperationException("Reading already started");

            cancellationTokenSource = new();
            task                    = ReadFileAsync(pipeHandle, encoding, handler, cancellationTokenSource.Token);
        }

        private async Task ReadFileAsync(SafeFileHandle pipeHandle, Encoding encoding, DataReceivedEventHandler handler,
                                         CancellationToken cancellationToken)
        {
            var buffer = new byte[BUFFER_SIZE];

            while (_disposed == false  && cancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    if (pipeHandle == null)
                        break;
                    if (pipeHandle.IsInvalid)
                        break;
                    if (pipeHandle.IsClosed)
                        break;

                    // Wait for read with cancellation
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Timeout for each read

                    var bytesRead = 0u;
                    var readTask  = Task.Run(
                        () => Kernel32.ReadFile(
                            pipeHandle.DangerousGetHandle(),
                            buffer,
                            BUFFER_SIZE,
                            out bytesRead,
                            IntPtr.Zero
                        ),
                        cts.Token
                    );

                    bool success;
                    try
                    {
                        success = await readTask;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        continue; // Timeout, try again
                    }

                    if (success == false || bytesRead == 0)
                    {
                        var error = Marshal.GetLastWin32Error();
                        switch (error)
                        {
                        case 0:
                        case Kernel32.ERROR_BROKEN_PIPE:
                        case Kernel32.ERROR_NO_DATA:
                        case Kernel32.ERROR_HANDLE_EOF:
                            break;
                        default:
                            throw new Win32Exception(error);
                        }
                    }

                    if (_disposed == false)
                    {
                        var data = encoding.GetString(buffer, 0, (int)bytesRead);

                        handler?.Invoke(this, new DataReceivedEventArgs(data));
                    }
                }
                catch (Exception) when (_disposed || cancellationToken.IsCancellationRequested) 
                { 
                    break;
                }

                await Task.Yield(); // Allow other tasks to run
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            // Immediately mark as disposed to prevent new operations
            _disposed = true;

            if (disposing)
            {
                // Cancel and cleanup read operations
                StopExitMonitor();
                CancelOutputRead();
                CancelErrorRead();

                // Dispose streams
                var stream    = _outputStream;
                _outputStream = null;
                stream?.Close();

                stream       = _errorStream;
                _errorStream = null;
                stream?.Close();

                // Cleanup pipes and handles
                _outputPipeHandle = null;
                _errorStream      = null;

                // Clear all event handlers
                OutputDataReceived = null;
                ErrorDataReceived  = null;
                Exited             = null;
            }

            // Cleanup process handle
            IntPtr handleToClose = _processHandle;
            _processHandle       = IntPtr.Zero;  // Clear reference before closing

            if (handleToClose != IntPtr.Zero)
            {
                try { _ = Kernel32.CloseHandle(handleToClose); } catch { }
            }

            try
            {   // Raise disposed event last
                var handler = Disposed;
                Disposed    = null;

                handler?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        private void CreatePipes(ref Kernel32.STARTUPINFO startupInfo, ref SafeFileHandle outputWritePipeHandle, ref SafeFileHandle errorWritePipeHandle)
        {
            if (_startInfo.RedirectStandardOutput == false && _startInfo.RedirectStandardError == false)
                return;

            var securityAttributes    = ProcessUtility.GetSecurityAttributesForPipe();
            var securityAttributesPtr = Marshal.AllocHGlobal(Marshal.SizeOf(securityAttributes));

            try
            {
                Marshal.StructureToPtr(securityAttributes, securityAttributesPtr, false);

                if (_startInfo.RedirectStandardOutput)
                {
                    CreatePipe(out _outputPipeHandle, out outputWritePipeHandle, securityAttributesPtr);

                    startupInfo.hStdOutput = outputWritePipeHandle.DangerousGetHandle();
                }

                if (_startInfo.RedirectStandardError)
                {
                    CreatePipe(out _errorPipeHandle, out errorWritePipeHandle, securityAttributesPtr);

                    startupInfo.hStdError = errorWritePipeHandle.DangerousGetHandle();
                }

                startupInfo.dwFlags |= Kernel32.STARTF_USESTDHANDLES;
            }
            finally
            {
                Marshal.FreeHGlobal(securityAttributesPtr);
            }
        }

        private void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, IntPtr saPtr)
        {
            SafeFileHandle hTmp = null;

            try
            {
                var result = Kernel32.CreatePipe(out hTmp, out childHandle, saPtr, 0);
                if (result == false || hTmp.IsInvalid || childHandle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs#L840
                // Duplicate the parent handle to be non-inheritable so that the child process
                // doesn't have access. This is done for correctness sake, exact reason is unclear.
                // One potential theory is that child process can do something brain dead like
                // closing the parent end of the pipe and there by getting into a blocking situation
                // as parent will not be draining the pipe at the other end anymore.
                var currentProcHandle = Kernel32.GetCurrentProcess();
                result                = Kernel32.DuplicateHandle(currentProcHandle, hTmp.DangerousGetHandle(), currentProcHandle,
                                                                 out var duplicatedHandle, 0, false, Kernel32.DUPLICATE_SAME_ACCESS);
                if (result == false)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                parentHandle = new SafeFileHandle(duplicatedHandle, true);
            }
            finally
            {
                if (hTmp != null && hTmp.IsInvalid == false)
                {
                    hTmp.Dispose();
                }
            }
        }
    }
}
