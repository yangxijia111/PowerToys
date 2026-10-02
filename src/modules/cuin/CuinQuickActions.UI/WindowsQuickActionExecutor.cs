// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] WindowsQuickActionExecutor —— 动作执行器的 Windows API 实现。
// 安全设计：只启动目录中固定的 URI / 可执行文件；进程终止用托管 Process API；
// 结束前台应用前经 ForegroundProcessGuard 保护名单二次校验。
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CuinQuickActions.Common.Actions;
using ManagedCommon;
using Windows.Win32.Foundation;

namespace CuinQuickActions.UI
{
    public sealed class WindowsQuickActionExecutor : IQuickActionExecutor
    {
        private readonly Func<IntPtr> _lastForegroundWindowProvider;

        public WindowsQuickActionExecutor(Func<IntPtr> lastForegroundWindowProvider)
        {
            _lastForegroundWindowProvider = lastForegroundWindowProvider ?? throw new ArgumentNullException(nameof(lastForegroundWindowProvider));
        }

        public async Task<bool> ExecuteAsync(QuickActionDefinition action)
        {
            try
            {
                return action.Kind switch
                {
                    QuickActionKind.LaunchUri => await LaunchUriAsync(action.Uri!),
                    QuickActionKind.LaunchShellTarget => LaunchShellTarget(action.ShellTarget!),
                    QuickActionKind.LockWorkStation => LockWorkStation(),
                    QuickActionKind.ClearClipboard => ClearClipboard(),
                    QuickActionKind.CloseForegroundApplication => CloseForegroundApplication(),
                    QuickActionKind.RestartExplorer => RestartExplorer(),
                    QuickActionKind.OpenCuinSettings => OpenCuinSettings(),
                    _ => UnknownKind(action),
                };
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to execute quick action '{action.Id}'", ex);
                return false;
            }
        }

        private static bool UnknownKind(QuickActionDefinition action)
        {
            Logger.LogError($"Unknown quick action kind: {action.Kind}");
            return false;
        }

        private static async Task<bool> LaunchUriAsync(string uri)
        {
            return await global::Windows.System.Launcher.LaunchUriAsync(new global::System.Uri(uri));
        }

        private static bool LaunchShellTarget(ShellTarget target)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = target.Executable,
                Arguments = target.ExpandArguments(),
                UseShellExecute = true,
            });
            return process != null;
        }

        private static bool LockWorkStation()
        {
            return Windows.Win32.PInvoke_CuinQuickActions.LockWorkStation();
        }

        private static bool ClearClipboard()
        {
            if (!Windows.Win32.PInvoke_CuinQuickActions.OpenClipboard(HWND.Null))
            {
                Logger.LogWarning("OpenClipboard failed; another window may hold the clipboard.");
                return false;
            }

            try
            {
                return Windows.Win32.PInvoke_CuinQuickActions.EmptyClipboard();
            }
            finally
            {
                Windows.Win32.PInvoke_CuinQuickActions.CloseClipboard();
            }
        }

        private bool CloseForegroundApplication()
        {
            var hwnd = _lastForegroundWindowProvider();
            if (hwnd == IntPtr.Zero)
            {
                Logger.LogWarning("No foreground window recorded; cannot close application.");
                return false;
            }

            Windows.Win32.PInvoke_CuinQuickActions.GetWindowThreadProcessId((HWND)hwnd, out uint pid);
            if (pid == 0)
            {
                return false;
            }

            using var process = Process.GetProcessById((int)pid);
            if (ForegroundProcessGuard.IsProtectedProcess(process.ProcessName))
            {
                Logger.LogWarning($"Refusing to terminate protected process '{process.ProcessName}'.");
                return false;
            }

            process.Kill(entireProcessTree: true);
            return true;
        }

        private static bool RestartExplorer()
        {
            foreach (var explorer in Process.GetProcessesByName("explorer"))
            {
                using (explorer)
                {
                    try
                    {
                        explorer.Kill();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to terminate an explorer.exe instance: {ex.Message}");
                    }
                }
            }

            // explorer 通常会自行重启；显式启动一次，确保桌面与任务栏恢复。
            using var restarted = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
            });
            return true;
        }

        private static bool OpenCuinSettings()
        {
            var settingsExe = Path.Combine(AppContext.BaseDirectory, "PowerToys.Settings.exe");
            if (!File.Exists(settingsExe))
            {
                Logger.LogError($"PowerToys.Settings.exe not found at '{settingsExe}'.");
                return false;
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = settingsExe,
                UseShellExecute = true,
            });
            return process != null;
        }
    }
}
