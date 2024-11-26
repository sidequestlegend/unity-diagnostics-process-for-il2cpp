# Win32 Process Implementation for Unity/IL2CPP

A robust implementation of the `Process` class compatible with Unity's IL2CPP backend, designed to replace the standard .NET `System.Diagnostics.Process` class.

## Features

- Full compatibility with Unity's IL2CPP
- Asynchronous process management
- Proper pipe handling for standard output/error redirection
- Event-based process monitoring
- Encoding support for output/error streams
- Clean process termination handling
- Resource cleanup with proper disposal patterns

## Usage

```csharp
// Start a process
var process = new Process {
    StartInfo = new() {
        FileName               = @"c:\Windows\notepad.exe",
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = false
    }
};

// Handle output
process.OutputDataReceived += (sender, args) => Debug.LogFormat("Output: {0}", args.Data);
process.ErrorDataReceived  += (sender, args) => Debug.LogErrorFormat("Output: {0}", args.Data);

// Enable exit monitoring
process.EnableRaisingEvents = true;
process.Exited             += (sender, args) => Debug.Log("Process exited");

// Start process
if (process.Start() == false)
{
    Debug.LogErrorFormat("Process start failed");
    return;
}

// Begin reading
process.BeginOutputReadLine();
process.BeginErrorReadLine();

// Wait for exit asynchronously
_ = await process.WaitForExitAsync();
```

## Key Features

### Process Management
- Process creation with customizable startup options
- Handle inheritance control
- Working directory support
- Command line argument handling

### I/O Handling
- Asynchronous output/error reading
- Configurable encoding for output streams
- Event-based data reception
- Proper pipe cleanup

### Process Control
- Clean process termination
- Window message sending
- Exit monitoring
- Resource cleanup

### Safety Features
- Thread-safe implementation
- Proper handle management
- Exception handling
- Memory leak prevention

## Implementation Details

The implementation uses Win32 API calls to manage processes and handles, making it compatible with IL2CPP while maintaining similar functionality to the standard .NET Process class.

Key improvements over the standard implementation:
- Non-inheritable pipe handles for better process isolation
- Async/await pattern for modern C# compatibility
- Proper cancellation support
- Robust cleanup of resources

## Requirements

- Unity 2021.3 or later
- Windows platform
- IL2CPP backend

## Limitations

- Windows-only implementation
- Some standard .NET Process features may not be available
- Requires proper disposal management

## Contributing

Feel free to submit issues and pull requests for improvements or bug fixes.

## License

This project is licensed under the MIT License - see the LICENSE file for details. Use of this software requires attribution to the original author and project, as detailed in the license.

## Credits

Based on the .NET Foundation's Process implementation and adapted for Unity/IL2CPP compatibility.