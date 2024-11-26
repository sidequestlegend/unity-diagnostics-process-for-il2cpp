using GoodAI.Core.Diagnostics;
using UnityEngine;

sealed class Example : MonoBehaviour
{
    async void Start()
    {
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

        // Quit
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
